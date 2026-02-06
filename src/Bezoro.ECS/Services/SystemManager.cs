using Bezoro.ECS.Abstractions;
using Bezoro.ECS.Attributes;
using Bezoro.ECS.Internal;
using Bezoro.ECS.Types;

namespace Bezoro.ECS.Services;

/// <summary>
/// Manages system lifecycle and staged execution.
/// </summary>
internal sealed class SystemManager
{
	private static readonly Stage[] StageOrder = [Stage.Input, Stage.PreUpdate, Stage.Update, Stage.PostUpdate, Stage.Render];
	private readonly int _maxDegreeOfParallelism;
	private readonly List<SystemState> _systems = [];
	private readonly Dictionary<Stage, List<SystemState[]>> _stagePlans = new();
	private bool _isPlanDirty = true;

	public SystemManager() : this(Environment.ProcessorCount)
	{
	}

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

		var readSet = new HashSet<int>();
		var writeSet = new HashSet<int>();

		var accesses = system.Accesses ?? [];
		for (var i = 0; i < accesses.Length; i++)
		{
			var access = accesses[i];
			if (access.Mode == ComponentAccessMode.ReadWrite)
			{
				AddWriteType(readSet, writeSet, access.ComponentType);
			}
			else
			{
				AddReadType(readSet, writeSet, access.ComponentType);
			}
		}

		var type = system.GetType();
		bool isExclusive;
		if (GeneratedSystemMetadataResolver.TryGet(type, out var metadata))
		{
			for (var i = 0; i < metadata.Reads.Length; i++)
			{
				var componentType = metadata.Reads[i];
				if (componentType is null) continue;
				AddReadType(readSet, writeSet, componentType);
			}

			for (var i = 0; i < metadata.Writes.Length; i++)
			{
				var componentType = metadata.Writes[i];
				if (componentType is null) continue;
				AddWriteType(readSet, writeSet, componentType);
			}

			isExclusive = metadata.IsExclusive;
		}
		else
		{
			foreach (var attribute in type.GetCustomAttributes(inherit: true))
			{
				if (attribute is null) continue;

				var attributeType = attribute.GetType();
				if (!attributeType.IsGenericType) continue;

				var generic = attributeType.GetGenericTypeDefinition();
				if (generic == typeof(ReadsAttribute<>))
				{
					var componentType = attributeType.GetGenericArguments()[0];
					AddReadType(readSet, writeSet, componentType);
				}
				else if (generic == typeof(WritesAttribute<>))
				{
					var componentType = attributeType.GetGenericArguments()[0];
					AddWriteType(readSet, writeSet, componentType);
				}
			}

			isExclusive = type.IsDefined(typeof(ExclusiveAttribute), inherit: true);
		}

		var stage = explicitStage ?? system.Stage;
		var state = new SystemState(system, stage, ToArray(readSet), ToArray(writeSet), isExclusive);
		_systems.Add(state);
		_isPlanDirty = true;
		system.OnCreate(world);
	}

	private static void AddReadType(HashSet<int> readSet, HashSet<int> writeSet, Type componentType)
	{
		int typeId = ComponentTypeRegistry.GetOrCreate(componentType);
		if (!writeSet.Contains(typeId))
			readSet.Add(typeId);
	}

	private static void AddWriteType(HashSet<int> readSet, HashSet<int> writeSet, Type componentType)
	{
		int typeId = ComponentTypeRegistry.GetOrCreate(componentType);
		writeSet.Add(typeId);
		readSet.Remove(typeId);
	}

	public void Shutdown(World world)
	{
		for (var i = _systems.Count - 1; i >= 0; i--)
			_systems[i].System.OnDestroy(world);
	}

	public void UpdateAll(World world, float deltaTime)
	{
		if (world is null) throw new ArgumentNullException(nameof(world));
		if (_systems.Count == 0) return;
		if (_isPlanDirty)
			RebuildExecutionPlan();

		for (var s = 0; s < StageOrder.Length; s++)
		{
			var stage = StageOrder[s];
			if (!_stagePlans.TryGetValue(stage, out var stageBatches) || stageBatches.Count == 0)
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

	private static int[] ToArray(HashSet<int> set)
	{
		var array = new int[set.Count];
		var index = 0;
		foreach (int value in set)
			array[index++] = value;

		return array;
	}

	private static bool ShouldRun(SystemState state, float deltaTime, out float effectiveDeltaTime)
	{
		const int maxCatchUpTicks = 3;
		var settings = state.System.UpdateSettings;
		if (settings.IntervalSeconds <= 0f)
		{
			effectiveDeltaTime = deltaTime;
			return true;
		}

		state.Accumulator += deltaTime;
		float maxAccumulator = settings.IntervalSeconds * maxCatchUpTicks;
		if (state.Accumulator > maxAccumulator)
			state.Accumulator = maxAccumulator;

		if (state.Accumulator < settings.IntervalSeconds)
		{
			effectiveDeltaTime = 0f;
			return false;
		}

		state.Accumulator -= settings.IntervalSeconds;
		effectiveDeltaTime = settings.IntervalSeconds;
		return true;
	}

	private static List<SystemState[]> BuildStateBatches(List<SystemState> stageSystems)
	{
		var count = stageSystems.Count;
		if (count == 0) return [];

		var indegree = new int[count];
		var edges = new List<int>[count];
		for (var i = 0; i < count; i++)
			edges[i] = [];

		for (var i = 0; i < count; i++)
		{
			for (var j = i + 1; j < count; j++)
			{
				if (!Conflicts(stageSystems[i], stageSystems[j])) continue;
				edges[i].Add(j);
				indegree[j]++;
			}
		}

		var batches = new List<SystemState[]>();
		var processed = new bool[count];
		var remaining = count;

		while (remaining > 0)
		{
			var selectedIndices = new List<int>();
			var selectedStates = new List<SystemState>();

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
				var from = selectedIndices[i];
				var next = edges[from];
				for (var j = 0; j < next.Count; j++)
					indegree[next[j]]--;
			}
		}

		return batches;
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

	private void RebuildExecutionPlan()
	{
		_stagePlans.Clear();
		for (var i = 0; i < StageOrder.Length; i++)
		{
			var stage = StageOrder[i];
			var stageSystems = new List<SystemState>();
			for (var s = 0; s < _systems.Count; s++)
			{
				var state = _systems[s];
				if (state.Stage == stage)
					stageSystems.Add(state);
			}

			if (stageSystems.Count == 0)
				continue;

			_stagePlans[stage] = BuildStateBatches(stageSystems);
		}

		_isPlanDirty = false;
		PlanBuildCount++;
	}

	private static bool Conflicts(SystemState first, SystemState second)
	{
		if (first.IsExclusive || second.IsExclusive)
			return true;

		return Overlaps(first.WriteIds, second.ReadIds) ||
		       Overlaps(first.WriteIds, second.WriteIds) ||
		       Overlaps(first.ReadIds, second.WriteIds);
	}

	private static bool Overlaps(int[] left, int[] right)
	{
		for (var i = 0; i < left.Length; i++)
		{
			int value = left[i];
			for (var j = 0; j < right.Length; j++)
			{
				if (value == right[j])
					return true;
			}
		}

		return false;
	}

	private CommandBuffer[] ExecuteBatch(SystemBatch batch, World world)
	{
		if (batch.Systems.Count == 0) return [];
		var buffers = new CommandBuffer[batch.Systems.Count];

		if (_maxDegreeOfParallelism == 1 || batch.Systems.Count == 1)
		{
			for (var i = 0; i < batch.Systems.Count; i++)
				ExecuteSystem(batch.Systems[i], world, buffers, i);
		}
		else
		{
			var options = new ParallelOptions { MaxDegreeOfParallelism = _maxDegreeOfParallelism };
			Parallel.For(0, batch.Systems.Count, options, i => ExecuteSystem(batch.Systems[i], world, buffers, i));
		}

		return buffers;
	}

	private static void ExecuteSystem(SystemExecution execution, World world, CommandBuffer[] buffers, int index)
	{
		var buffer = new CommandBuffer(world);
		buffers[index] = buffer;
		var context = new SystemContext(execution.DeltaTime, execution.State.Stage, buffer);
		execution.State.System.Update(world, in context);
	}

	private static void FlushBuffers(CommandBuffer[] buffers)
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

				buffer.PlaybackInternal(allowDuringUpdate: true);
			}
			finally
			{
				buffer.Dispose();
			}
		}
	}
}
