using System.Runtime.ExceptionServices;
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
	private readonly Dictionary<Type, int>                                               _resourceTypeToAccessId = [];
	private readonly Dictionary<Type, bool>                                              _setEnabledByType = [];
	private readonly Dictionary<Type, ISystemRunCondition>                               _setRunConditionsByType = [];
	private readonly List<SystemState>                                                   _systems     = [];
	private          bool                                                                _isPlanDirty = true;
	private          int                                                                 _nextResourceAccessId = -1;

	public SystemManager() : this(Environment.ProcessorCount) { }

	public SystemManager(int maxDegreeOfParallelism)
	{
		if (maxDegreeOfParallelism <= 0)
			throw new ArgumentOutOfRangeException(nameof(maxDegreeOfParallelism), "Parallelism must be positive.");

		_maxDegreeOfParallelism = maxDegreeOfParallelism;
	}

	internal int PlanBuildCount { get; private set; }

	public ScheduleDiagnostics GetDiagnostics()
	{
		if (_isPlanDirty && _systems.Count > 0)
			RebuildExecutionPlan();

		var phases = new List<SchedulePhaseDiagnostics>();
		for (var phaseIndex = 0; phaseIndex < LoopPhaseOrder.Length; phaseIndex++)
		{
			var loopPhase = LoopPhaseOrder[phaseIndex];
			if (!_phaseStagePlans.TryGetValue(loopPhase, out var stagePlans))
				continue;

			var stages = new List<ScheduleStageDiagnostics>();
			for (var stageIndex = 0; stageIndex < StageOrder.Length; stageIndex++)
			{
				var stage = StageOrder[stageIndex];
				if (!stagePlans.TryGetValue(stage, out var stageBatches) || stageBatches.Count == 0)
					continue;

				var batches = new ScheduleBatchDiagnostics[stageBatches.Count];
				for (var batchIndex = 0; batchIndex < stageBatches.Count; batchIndex++)
				{
					var batchStates = stageBatches[batchIndex];
					var systemTypes = new Type[batchStates.Length];
					bool containsExclusiveSystem = false;
					for (var stateIndex = 0; stateIndex < batchStates.Length; stateIndex++)
					{
						var state = batchStates[stateIndex];
						systemTypes[stateIndex] = state.System.GetType();
						if (state.IsExclusive)
							containsExclusiveSystem = true;
					}

					batches[batchIndex] = new(batchIndex, systemTypes, containsExclusiveSystem);
				}

				stages.Add(new(stage, batches));
			}

			if (stages.Count > 0)
				phases.Add(new(loopPhase, [.. stages]));
		}

		return new(_systems.Count, PlanBuildCount, [.. phases]);
	}

	public bool IsSystemSetEnabled(Type setType)
	{
		if (setType is null)
			throw new ArgumentNullException(nameof(setType));

		return !_setEnabledByType.TryGetValue(setType, out bool enabled) || enabled;
	}

	public void SetSystemSetEnabled(Type setType, bool enabled)
	{
		if (setType is null)
			throw new ArgumentNullException(nameof(setType));

		_setEnabledByType[setType] = enabled;
	}

	public void SetSystemSetRunCondition(Type setType, ISystemRunCondition runCondition)
	{
		if (setType is null)
			throw new ArgumentNullException(nameof(setType));
		if (runCondition is null)
			throw new ArgumentNullException(nameof(runCondition));

		_setRunConditionsByType[setType] = runCondition;
	}

	public void ClearSystemSetRunCondition(Type setType)
	{
		if (setType is null)
			throw new ArgumentNullException(nameof(setType));

		_setRunConditionsByType.Remove(setType);
	}

	public void RegisterSystem(World world, ISystem system, Stage? explicitStage = null)
	{
		if (world is null) throw new ArgumentNullException(nameof(world));
		if (system is null) throw new ArgumentNullException(nameof(system));

		for (var i = 0; i < _systems.Count; i++)
		{
			if (ReferenceEquals(_systems[i].System, system))
				throw new InvalidOperationException("System is already registered.");
		}

		var readSet                   = new HashSet<int>();
		var writeSet                  = new HashSet<int>();
		var systemSetTypes            = new HashSet<Type>();
		List<ISystemRunCondition>? runConditions = null;
		var hasDeclaredAccessMetadata = false;

		var  type = system.GetType();
		bool isExclusive = false;
		if (_metadataResolver.TryGet(type, out var metadata))
		{
			if (metadata.Reads.Length > 0 ||
				metadata.Writes.Length > 0 ||
				metadata.ReadResources.Length > 0 ||
				metadata.WriteResources.Length > 0 ||
				metadata.IsExclusive)
				hasDeclaredAccessMetadata = true;

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

			for (var i = 0; i < metadata.ReadResources.Length; i++)
			{
				var resourceType = metadata.ReadResources[i];
				if (resourceType is null) continue;

				AddReadResourceType(readSet, writeSet, resourceType, GetOrCreateResourceAccessTypeId);
			}

			for (var i = 0; i < metadata.WriteResources.Length; i++)
			{
				var resourceType = metadata.WriteResources[i];
				if (resourceType is null) continue;

				AddWriteResourceType(readSet, writeSet, resourceType, GetOrCreateResourceAccessTypeId);
			}

			isExclusive = metadata.IsExclusive;
		}

		var hasAttributeAccessMetadata = false;
		foreach (object? attribute in type.GetCustomAttributes(true))
		{
			if (attribute is null)
				continue;

			if (attribute is ExclusiveAttribute)
			{
				isExclusive = true;
				continue;
			}

			var attributeType = attribute.GetType();
			if (!attributeType.IsGenericType)
				continue;

			var generic = attributeType.GetGenericTypeDefinition();
			if (generic == typeof(ReadsAttribute<>))
			{
				var componentType = attributeType.GetGenericArguments()[0];
				AddReadType(world, readSet, writeSet, componentType);
				hasAttributeAccessMetadata = true;
			}
			else if (generic == typeof(WritesAttribute<>))
			{
				var componentType = attributeType.GetGenericArguments()[0];
				AddWriteType(world, readSet, writeSet, componentType);
				hasAttributeAccessMetadata = true;
			}
			else if (generic == typeof(ReadsResourceAttribute<>))
			{
				var resourceType = attributeType.GetGenericArguments()[0];
				AddReadResourceType(readSet, writeSet, resourceType, GetOrCreateResourceAccessTypeId);
				hasAttributeAccessMetadata = true;
			}
			else if (generic == typeof(WritesResourceAttribute<>))
			{
				var resourceType = attributeType.GetGenericArguments()[0];
				AddWriteResourceType(readSet, writeSet, resourceType, GetOrCreateResourceAccessTypeId);
				hasAttributeAccessMetadata = true;
			}
			else if (generic == typeof(SystemSetAttribute<>))
			{
				var setType = attributeType.GetGenericArguments()[0];
				systemSetTypes.Add(setType);
				EnsureSystemSetKnown(setType);
			}
			else if (generic == typeof(RunIfAttribute<>))
			{
				var runConditionType = attributeType.GetGenericArguments()[0];
				(runConditions ??= []).Add(CreateRunCondition(runConditionType));
			}
		}

		if (isExclusive || hasAttributeAccessMetadata)
			hasDeclaredAccessMetadata = true;

		if (!hasDeclaredAccessMetadata)
			isExclusive = false;

		var beforeSystemTypes = new HashSet<Type>();
		var afterSystemTypes  = new HashSet<Type>();
		CollectOrderingConstraints(type, beforeSystemTypes, afterSystemTypes);
		var stage = explicitStage ?? system.Stage;
		var state = new SystemState(
			system,
			stage,
			ToArray(readSet),
			ToArray(writeSet),
			isExclusive,
			ToOrderedTypeArray(beforeSystemTypes),
			ToOrderedTypeArray(afterSystemTypes),
			ToOrderedTypeArray(systemSetTypes),
			runConditions?.ToArray() ?? []
		);
		_systems.Add(state);
		_isPlanDirty = true;
		system.OnCreate(world);
	}

	public void Shutdown(World world)
	{
		if (world is null) throw new ArgumentNullException(nameof(world));

		List<Exception>? exceptions = null;
		try
		{
			for (int i = _systems.Count - 1; i >= 0; i--)
			{
				try
				{
					_systems[i].System.OnDestroy(world);
				}
				catch (Exception ex)
				{
					(exceptions ??= []).Add(ex);
				}
			}
		}
		finally
		{
			_systems.Clear();
			_phaseStagePlans.Clear();
			_resourceTypeToAccessId.Clear();
			_setEnabledByType.Clear();
			_setRunConditionsByType.Clear();
			_nextResourceAccessId = -1;
			_isPlanDirty = true;
		}

		if (exceptions is null)
			return;

		if (exceptions.Count == 1)
			ExceptionDispatchInfo.Capture(exceptions[0]).Throw();

		throw new AggregateException("One or more systems failed during shutdown.", exceptions);
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
				var batch = BuildExecutionBatch(world, stageBatches[i], deltaTime);
				if (batch.Systems.Count == 0)
					continue;

				var streams = ExecuteBatch(batch, world);
				FlushStreams(world, streams);
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

	private static Type[] ToOrderedTypeArray(HashSet<Type> types) =>
		types.OrderBy(static type => type.FullName, StringComparer.Ordinal)
			 .ToArray();

	private bool ShouldRun(World world, SystemState state, float deltaTime, out float effectiveDeltaTime)
	{
		if (!AreSystemSetsEnabled(state.SystemSetTypes))
		{
			effectiveDeltaTime = 0f;
			return false;
		}

		if (!EvaluateRunConditions(world, state, deltaTime))
		{
			effectiveDeltaTime = 0f;
			return false;
		}

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

	private bool AreSystemSetsEnabled(Type[] systemSetTypes)
	{
		for (var i = 0; i < systemSetTypes.Length; i++)
		{
			var setType = systemSetTypes[i];
			if (_setEnabledByType.TryGetValue(setType, out bool enabled) && !enabled)
				return false;
		}

		return true;
	}

	private bool EvaluateRunConditions(World world, SystemState state, float deltaTime)
	{
		if (state.RunConditions.Length == 0 && state.SystemSetTypes.Length == 0)
			return true;

		var context = new SystemRunConditionContext(
			world,
			state.System,
			state.LoopPhase,
			state.Stage,
			deltaTime
		);

		for (var i = 0; i < state.RunConditions.Length; i++)
		{
			if (!state.RunConditions[i].ShouldRun(in context))
				return false;
		}

		for (var i = 0; i < state.SystemSetTypes.Length; i++)
		{
			var setType = state.SystemSetTypes[i];
			if (!_setRunConditionsByType.TryGetValue(setType, out var runCondition))
				continue;

			if (!runCondition.ShouldRun(in context))
				return false;
		}

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

		var indicesBySystemType = BuildSystemTypeIndexMap(stageSystems);
		for (var i = 0; i < count; i++)
		{
			AddOrderingEdges(stageSystems[i], i, indicesBySystemType, edges, indegree);

			for (int j = i + 1; j < count; j++)
			{
				if (!Conflicts(stageSystems[i], stageSystems[j])) continue;

				AddEdge(i, j, edges, indegree);
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
			{
				var stuck = new System.Text.StringBuilder();
				for (var i = 0; i < count; i++)
				{
					if (processed[i]) continue;
					if (stuck.Length > 0) stuck.Append(", ");
					stuck.Append(stageSystems[i].System.GetType().Name);
				}

				throw new InvalidOperationException(
					$"System dependency graph contains a cycle. Stuck systems: {stuck}"
				);
			}

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

	private static Dictionary<Type, List<int>> BuildSystemTypeIndexMap(List<SystemState> stageSystems)
	{
		var map = new Dictionary<Type, List<int>>();
		for (var i = 0; i < stageSystems.Count; i++)
		{
			Type systemType = stageSystems[i].System.GetType();
			if (!map.TryGetValue(systemType, out var indices))
			{
				indices = [];
				map[systemType] = indices;
			}

			indices.Add(i);
		}

		return map;
	}

	private static void AddOrderingEdges(
		SystemState                    state,
		int                            stateIndex,
		Dictionary<Type, List<int>>    indicesBySystemType,
		List<int>[]                    edges,
		int[]                          indegree)
	{
		for (var i = 0; i < state.BeforeSystemTypes.Length; i++)
		{
			var dependentSystemType = state.BeforeSystemTypes[i];
			if (!indicesBySystemType.TryGetValue(dependentSystemType, out var dependentIndices))
				continue;

			for (var j = 0; j < dependentIndices.Count; j++)
			{
				int dependentIndex = dependentIndices[j];
				if (dependentIndex == stateIndex)
					continue;

				AddEdge(stateIndex, dependentIndex, edges, indegree);
			}
		}

		for (var i = 0; i < state.AfterSystemTypes.Length; i++)
		{
			var prerequisiteSystemType = state.AfterSystemTypes[i];
			if (!indicesBySystemType.TryGetValue(prerequisiteSystemType, out var prerequisiteIndices))
				continue;

			for (var j = 0; j < prerequisiteIndices.Count; j++)
			{
				int prerequisiteIndex = prerequisiteIndices[j];
				if (prerequisiteIndex == stateIndex)
					continue;

				AddEdge(prerequisiteIndex, stateIndex, edges, indegree);
			}
		}
	}

	private static void AddEdge(int from, int to, List<int>[] edges, int[] indegree)
	{
		var targets = edges[from];
		for (var i = 0; i < targets.Count; i++)
		{
			if (targets[i] != to)
				continue;

			return;
		}

		targets.Add(to);
		indegree[to]++;
	}

	private static void AddReadType(World world, HashSet<int> readSet, HashSet<int> writeSet, Type componentType)
	{
		int typeId = world.GetOrCreateComponentTypeId(componentType);
		if (!writeSet.Contains(typeId))
			readSet.Add(typeId);
	}

	private static void AddReadResourceType(
		HashSet<int>    readSet,
		HashSet<int>    writeSet,
		Type            resourceType,
		Func<Type, int> resourceAccessIdResolver)
	{
		int accessId = resourceAccessIdResolver(resourceType);
		if (!writeSet.Contains(accessId))
			readSet.Add(accessId);
	}

	private static void AddWriteType(World world, HashSet<int> readSet, HashSet<int> writeSet, Type componentType)
	{
		int typeId = world.GetOrCreateComponentTypeId(componentType);
		writeSet.Add(typeId);
		readSet.Remove(typeId);
	}

	private static void AddWriteResourceType(
		HashSet<int>    readSet,
		HashSet<int>    writeSet,
		Type            resourceType,
		Func<Type, int> resourceAccessIdResolver)
	{
		int accessId = resourceAccessIdResolver(resourceType);
		writeSet.Add(accessId);
		readSet.Remove(accessId);
	}

	private static void CollectOrderingConstraints(Type type, HashSet<Type> beforeSet, HashSet<Type> afterSet)
	{
		foreach (object? attribute in type.GetCustomAttributes(true))
		{
			if (attribute is null)
				continue;

			var attributeType = attribute.GetType();
			if (!attributeType.IsGenericType)
				continue;

			var generic = attributeType.GetGenericTypeDefinition();
			if (generic == typeof(BeforeAttribute<>))
			{
				var dependencyType = attributeType.GetGenericArguments()[0];
				beforeSet.Add(dependencyType);
			}
			else if (generic == typeof(AfterAttribute<>))
			{
				var dependencyType = attributeType.GetGenericArguments()[0];
				afterSet.Add(dependencyType);
			}
		}
	}

	private static ISystemRunCondition CreateRunCondition(Type runConditionType)
	{
		if (runConditionType is null)
			throw new ArgumentNullException(nameof(runConditionType));

		if (!typeof(ISystemRunCondition).IsAssignableFrom(runConditionType))
			throw new InvalidOperationException(
				$"Run condition type '{runConditionType.FullName}' must implement '{typeof(ISystemRunCondition).FullName}'."
			);

		try
		{
			return (ISystemRunCondition)(Activator.CreateInstance(runConditionType) ??
										 throw new InvalidOperationException(
											 $"Run condition type '{runConditionType.FullName}' could not be created."
										 ));
		}
		catch (MissingMethodException ex)
		{
			throw new InvalidOperationException(
				$"Run condition type '{runConditionType.FullName}' must declare a public parameterless constructor.",
				ex
			);
		}
	}

	private void EnsureSystemSetKnown(Type setType)
	{
		if (setType is null)
			throw new ArgumentNullException(nameof(setType));

		_setEnabledByType.TryAdd(setType, true);
	}

	private int GetOrCreateResourceAccessTypeId(Type resourceType)
	{
		if (resourceType is null)
			throw new ArgumentNullException(nameof(resourceType));

		if (_resourceTypeToAccessId.TryGetValue(resourceType, out int existing))
			return existing;

		int id = _nextResourceAccessId--;
		_resourceTypeToAccessId[resourceType] = id;
		return id;
	}

	private CommandStream[] ExecuteBatch(SystemBatch batch, World world)
	{
		if (batch.Systems.Count == 0) return [];

		var streams = new CommandStream[batch.Systems.Count];

		if (_maxDegreeOfParallelism == 1 || batch.Systems.Count == 1)
			for (var i = 0; i < batch.Systems.Count; i++)
				ExecuteSystem(batch.Systems[i], world, streams, i);
		else
			ParallelWorkScheduler.Execute(
				batch.Systems.Count,
				_maxDegreeOfParallelism,
				i => ExecuteSystem(batch.Systems[i], world, streams, i)
			);

		return streams;
	}

	private SystemBatch BuildExecutionBatch(World world, SystemState[] batchStates, float deltaTime)
	{
		var batch = new SystemBatch();
		for (var i = 0; i < batchStates.Length; i++)
		{
			var state = batchStates[i];
			if (!ShouldRun(world, state, deltaTime, out float effectiveDeltaTime))
				continue;

			batch.Add(new(state, effectiveDeltaTime));
		}

		return batch;
	}

	private void ExecuteSystem(SystemExecution execution, World world, CommandStream[] streams, int index)
	{
		var stream = world.CreateCommandStream();
		streams[index] = stream;
		var context = new SystemContext(execution.DeltaTime, execution.State.Stage, world, new(stream));
		execution.State.System.Update(in context);
	}

	private void FlushStreams(World world, CommandStream[] streams)
	{
		for (var i = 0; i < streams.Length; i++)
		{
			var stream = streams[i];
			if (stream is null)
				continue;

			try
			{
				if (!stream.HasCommands)
					continue;

				world.Playback(stream);
			}
			finally
			{
				stream.Dispose();
			}
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
