using Bezoro.ECS.Abstractions;
using Bezoro.ECS.Attributes;
using Bezoro.ECS.Internal;
using Bezoro.ECS.Types;

namespace Bezoro.ECS.Services;

/// <summary>
///     Manages system lifecycle and staged execution.
/// </summary>
internal sealed class SystemManager
{
	private static readonly Stage[] StageOrder =
		[Stage.Input, Stage.PreTick, Stage.Tick, Stage.PostTick, Stage.Render];
	private static readonly SystemLoopPhase[] LoopPhaseOrder =
		[SystemLoopPhase.Tick, SystemLoopPhase.FixedTick, SystemLoopPhase.LateTick];
	private readonly Dictionary<SystemLoopPhase, Dictionary<Stage, List<SystemState[]>>> _phaseStagePlans  = new();
	private readonly GeneratedSystemMetadataResolver                                     _metadataResolver = new();
	private readonly int                                                                 _maxDegreeOfParallelism;
	private readonly Stack<CommandBuffer>                                                _commandBufferPool = new();
	private readonly object                                                              _commandBufferPoolSync = new();
	private readonly List<SystemState>                                                   _systems     = [];
	private          bool                                                                _isPlanDirty = true;

	public SystemManager() : this(Environment.ProcessorCount) { }

	public SystemManager(int maxDegreeOfParallelism)
	{
		if (maxDegreeOfParallelism <= 0)
			throw new ArgumentOutOfRangeException(nameof(maxDegreeOfParallelism), "Parallelism must be positive.");

		_maxDegreeOfParallelism = maxDegreeOfParallelism;
	}

	internal int PlanBuildCount { get; private set; }

	public void RegisterSystem(World world, ISystem system, Stage? explicitStage = null)
	{
		if (world is null) throw new ArgumentNullException(nameof(world));
		if (system is null) throw new ArgumentNullException(nameof(system));

		for (var i = 0; i < _systems.Count; i++)
		{
			if (ReferenceEquals(_systems[i].System, system))
				throw new InvalidOperationException("System is already registered.");
		}

		var readSet  = new HashSet<int>();
		var writeSet = new HashSet<int>();

		var accesses = system.Accesses ?? [];
		for (var i = 0; i < accesses.Length; i++)
		{
			var access = accesses[i];
			if (access.Mode == ComponentAccessMode.ReadWrite)
				AddWriteType(world, readSet, writeSet, access.ComponentType);
			else
				AddReadType(world, readSet, writeSet, access.ComponentType);
		}

		var  type = system.GetType();
		bool isExclusive;
		if (_metadataResolver.TryGet(type, out var metadata))
		{
			for (var i = 0; i < metadata.Reads.Length; i++)
			{
				var componentType = metadata.Reads[i];
				if (componentType is null) continue;

				AddReadType(world, readSet, writeSet, componentType);
			}

			for (var i = 0; i < metadata.Writes.Length; i++)
			{
				var componentType = metadata.Writes[i];
				if (componentType is null) continue;

				AddWriteType(world, readSet, writeSet, componentType);
			}

			isExclusive = metadata.IsExclusive;
		}
		else
		{
			foreach (object? attribute in type.GetCustomAttributes(true))
			{
				if (attribute is null) continue;

				var attributeType = attribute.GetType();
				if (!attributeType.IsGenericType) continue;

				var generic = attributeType.GetGenericTypeDefinition();
				if (generic == typeof(ReadsAttribute<>))
				{
					var componentType = attributeType.GetGenericArguments()[0];
					AddReadType(world, readSet, writeSet, componentType);
				}
				else if (generic == typeof(WritesAttribute<>))
				{
					var componentType = attributeType.GetGenericArguments()[0];
					AddWriteType(world, readSet, writeSet, componentType);
				}
			}

			isExclusive = type.IsDefined(typeof(ExclusiveAttribute), true);
		}

		var stage = explicitStage ?? system.Stage;
		var state = new SystemState(system, stage, ToArray(readSet), ToArray(writeSet), isExclusive);
		_systems.Add(state);
		_isPlanDirty = true;
		system.OnCreate(world);
	}

	public void Shutdown(World world)
	{
		for (int i = _systems.Count - 1; i >= 0; i--)
			_systems[i].System.OnDestroy(world);

		lock (_commandBufferPoolSync)
		{
			while (_commandBufferPool.Count > 0)
				_commandBufferPool.Pop().Dispose();
		}
	}

	public void UpdatePhase(World world, SystemLoopPhase loopPhase, float deltaTime)
	{
		if (world is null) throw new ArgumentNullException(nameof(world));

		if (_systems.Count == 0) return;

		if (_isPlanDirty)
			RebuildExecutionPlan();

		if (!_phaseStagePlans.TryGetValue(loopPhase, out var stagePlans))
			return;

		for (var s = 0; s < StageOrder.Length; s++)
		{
			var stage = StageOrder[s];
			if (!stagePlans.TryGetValue(stage, out var stageBatches) || stageBatches.Count == 0)
				continue;

			for (var i = 0; i < stageBatches.Count; i++)
			{
				var batch = BuildExecutionBatch(stageBatches[i], deltaTime);
				if (batch.Systems.Count == 0)
					continue;

				var buffers = ExecuteBatch(batch, world);
				FlushBuffers(buffers);
			}
		}
	}

	private static bool Conflicts(SystemState first, SystemState second)
	{
		if (first.IsExclusive || second.IsExclusive)
			return true;

		return Overlaps(first.WriteIds, second.ReadIds) ||
			   Overlaps(first.WriteIds, second.WriteIds) ||
			   Overlaps(first.ReadIds,  second.WriteIds);
	}

	private static bool Overlaps(int[] left, int[] right)
	{
		int i = 0, j = 0;
		while (i < left.Length && j < right.Length)
		{
			if (left[i] == right[j]) return true;

			if (left[i] < right[j]) i++;
			else j++;
		}

		return false;
	}

	private static bool ShouldRun(SystemState state, float deltaTime, out float effectiveDeltaTime)
	{
		const int MAX_CATCH_UP_TICKS = 3;
		var       settings           = state.System.UpdateSettings;
		if (settings.IntervalSeconds <= 0f)
		{
			effectiveDeltaTime = deltaTime;
			return true;
		}

		state.Accumulator += deltaTime;
		float maxAccumulator = settings.IntervalSeconds * MAX_CATCH_UP_TICKS;
		if (state.Accumulator > maxAccumulator)
			state.Accumulator = maxAccumulator;

		if (state.Accumulator < settings.IntervalSeconds)
		{
			effectiveDeltaTime = 0f;
			return false;
		}

		state.Accumulator  -= settings.IntervalSeconds;
		effectiveDeltaTime =  settings.IntervalSeconds;
		return true;
	}

	private static int[] ToArray(HashSet<int> set)
	{
		var array = new int[set.Count];
		var index = 0;
		foreach (int value in set)
			array[index++] = value;

		Array.Sort(array);
		return array;
	}

	private static List<SystemState[]> BuildStateBatches(List<SystemState> stageSystems)
	{
		int count = stageSystems.Count;
		if (count == 0) return [];

		var indegree = new int[count];
		var edges    = new List<int>[count];
		for (var i = 0; i < count; i++)
			edges[i] = [];

		for (var i = 0; i < count; i++)
		{
			for (int j = i + 1; j < count; j++)
			{
				if (!Conflicts(stageSystems[i], stageSystems[j])) continue;

				edges[i].Add(j);
				indegree[j]++;
			}
		}

		var batches   = new List<SystemState[]>();
		var processed = new bool[count];
		int remaining = count;

		while (remaining > 0)
		{
			var selectedIndices = new List<int>();
			var selectedStates  = new List<SystemState>();

			for (var i = 0; i < count; i++)
			{
				if (processed[i] || indegree[i] != 0) continue;

				if (stageSystems[i].IsExclusive)
				{
					if (selectedIndices.Count > 0)
						continue;

					processed[i] = true;
					selectedIndices.Add(i);
					selectedStates.Add(stageSystems[i]);
					break;
				}

				processed[i] = true;
				selectedIndices.Add(i);
				selectedStates.Add(stageSystems[i]);
			}

			if (selectedIndices.Count == 0)
				throw new InvalidOperationException("System dependency graph contains a cycle.");

			batches.Add([.. selectedStates]);
			remaining -= selectedIndices.Count;

			for (var i = 0; i < selectedIndices.Count; i++)
			{
				int from = selectedIndices[i];
				var next = edges[from];
				for (var j = 0; j < next.Count; j++)
					indegree[next[j]]--;
			}
		}

		return batches;
	}

	private static void AddReadType(World world, HashSet<int> readSet, HashSet<int> writeSet, Type componentType)
	{
		int typeId = world.GetOrCreateComponentTypeId(componentType);
		if (!writeSet.Contains(typeId))
			readSet.Add(typeId);
	}

	private static void AddWriteType(World world, HashSet<int> readSet, HashSet<int> writeSet, Type componentType)
	{
		int typeId = world.GetOrCreateComponentTypeId(componentType);
		writeSet.Add(typeId);
		readSet.Remove(typeId);
	}

	private void ExecuteSystem(SystemExecution execution, World world, CommandBuffer[] buffers, int index)
	{
		var buffer = RentCommandBuffer(world);
		buffers[index] = buffer;
		var context = new SystemContext(execution.DeltaTime, execution.State.Stage, buffer);
		execution.State.System.Update(world, in context);
	}

	private void FlushBuffers(CommandBuffer[] buffers)
	{
		for (var i = 0; i < buffers.Length; i++)
		{
			var buffer = buffers[i];
			if (buffer is null)
				continue;

			try
			{
				if (!buffer.HasCommands)
					continue;

				buffer.PlaybackInternal(true);
			}
			finally
			{
				ReturnCommandBuffer(buffer);
			}
		}
	}

	private CommandBuffer[] ExecuteBatch(SystemBatch batch, World world)
	{
		if (batch.Systems.Count == 0) return [];

		var buffers = new CommandBuffer[batch.Systems.Count];

		if (_maxDegreeOfParallelism == 1 || batch.Systems.Count == 1)
			for (var i = 0; i < batch.Systems.Count; i++)
				ExecuteSystem(batch.Systems[i], world, buffers, i);
		else
			ParallelWorkScheduler.Execute(
				batch.Systems.Count,
				_maxDegreeOfParallelism,
				i => ExecuteSystem(batch.Systems[i], world, buffers, i)
			);

		return buffers;
	}

	private SystemBatch BuildExecutionBatch(SystemState[] batchStates, float deltaTime)
	{
		var batch = new SystemBatch();
		for (var i = 0; i < batchStates.Length; i++)
		{
			var state = batchStates[i];
			if (!ShouldRun(state, deltaTime, out float effectiveDeltaTime))
				continue;

			batch.Add(new(state, effectiveDeltaTime));
		}

		return batch;
	}

	private CommandBuffer RentCommandBuffer(World world)
	{
		lock (_commandBufferPoolSync)
		{
			if (_commandBufferPool.Count == 0)
				return new(world);

			var pooled = _commandBufferPool.Pop();
			pooled.ActivateForReuse();
			return pooled;
		}
	}

	private void ReturnCommandBuffer(CommandBuffer buffer)
	{
		buffer.DeactivateForPool();
		lock (_commandBufferPoolSync)
		{
			_commandBufferPool.Push(buffer);
		}
	}

	private void RebuildExecutionPlan()
	{
		_phaseStagePlans.Clear();
		for (var phaseIndex = 0; phaseIndex < LoopPhaseOrder.Length; phaseIndex++)
		{
			var loopPhase  = LoopPhaseOrder[phaseIndex];
			var stagePlans = new Dictionary<Stage, List<SystemState[]>>();

			for (var stageIndex = 0; stageIndex < StageOrder.Length; stageIndex++)
			{
				var stage        = StageOrder[stageIndex];
				var stageSystems = new List<SystemState>();
				for (var systemIndex = 0; systemIndex < _systems.Count; systemIndex++)
				{
					var state = _systems[systemIndex];
					if (state.LoopPhase == loopPhase && state.Stage == stage)
						stageSystems.Add(state);
				}

				if (stageSystems.Count == 0)
					continue;

				stagePlans[stage] = BuildStateBatches(stageSystems);
			}

			if (stagePlans.Count == 0)
				continue;

			_phaseStagePlans[loopPhase] = stagePlans;
		}

		_isPlanDirty = false;
		PlanBuildCount++;
	}
}
