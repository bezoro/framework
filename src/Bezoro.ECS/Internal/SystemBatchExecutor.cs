using Bezoro.ECS.Abstractions;
using Bezoro.ECS.Services;
using Bezoro.ECS.Types;

namespace Bezoro.ECS.Internal;

internal sealed class SystemBatchExecutor(int maxDegreeOfParallelism)
{
	private const int MaxCatchUpTicks = 3;
	private readonly int _maxDegreeOfParallelism = maxDegreeOfParallelism;

	public void UpdatePhase(
		World                              world,
		SystemLoopPhase                    loopPhase,
		Dictionary<Stage, List<SystemState[]>> stagePlans,
		IReadOnlyDictionary<Type, bool>    setEnabledByType,
		IReadOnlyDictionary<Type, ISystemRunCondition> setRunConditionsByType,
		float                              deltaTime)
	{
		foreach (Stage stage in StageOrder.Stages)
		{
			if (!stagePlans.TryGetValue(stage, out var stageBatches) || stageBatches.Count == 0)
				continue;

			for (var i = 0; i < stageBatches.Count; i++)
			{
				var batch = BuildExecutionBatch(world, loopPhase, stageBatches[i], setEnabledByType, setRunConditionsByType, deltaTime);
				if (batch.Systems.Count == 0)
					continue;

				var streams = ExecuteBatch(batch, world);
				FlushStreams(world, streams);
			}
		}
	}

	private SystemBatch BuildExecutionBatch(
		World                                   world,
		SystemLoopPhase                         loopPhase,
		SystemState[]                           batchStates,
		IReadOnlyDictionary<Type, bool>         setEnabledByType,
		IReadOnlyDictionary<Type, ISystemRunCondition> setRunConditionsByType,
		float                                   deltaTime)
	{
		var batch = new SystemBatch();
		for (var i = 0; i < batchStates.Length; i++)
		{
			var state = batchStates[i];
			if (!ShouldRun(world, loopPhase, state, setEnabledByType, setRunConditionsByType, deltaTime, out float effectiveDeltaTime))
				continue;

			batch.Add(new(state, effectiveDeltaTime));
		}

		return batch;
	}

	private bool ShouldRun(
		World                                   world,
		SystemLoopPhase                         loopPhase,
		SystemState                             state,
		IReadOnlyDictionary<Type, bool>         setEnabledByType,
		IReadOnlyDictionary<Type, ISystemRunCondition> setRunConditionsByType,
		float                                   deltaTime,
		out float                               effectiveDeltaTime)
	{
		if (!AreSystemSetsEnabled(state.SystemSetTypes, setEnabledByType) ||
			!EvaluateRunConditions(world, loopPhase, state, setRunConditionsByType, deltaTime))
		{
			effectiveDeltaTime = 0f;
			return false;
		}

		var settings = state.System.UpdateSettings;
		if (settings.IntervalSeconds <= 0f)
		{
			effectiveDeltaTime = deltaTime;
			return true;
		}

		state.Accumulator += deltaTime;
		float maxAccumulator = settings.IntervalSeconds * MaxCatchUpTicks;
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

	private static bool AreSystemSetsEnabled(Type[] systemSetTypes, IReadOnlyDictionary<Type, bool> setEnabledByType)
	{
		for (var i = 0; i < systemSetTypes.Length; i++)
		{
			if (setEnabledByType.TryGetValue(systemSetTypes[i], out bool enabled) && !enabled)
				return false;
		}

		return true;
	}

	private static bool EvaluateRunConditions(
		World                                   world,
		SystemLoopPhase                         loopPhase,
		SystemState                             state,
		IReadOnlyDictionary<Type, ISystemRunCondition> setRunConditionsByType,
		float                                   deltaTime)
	{
		if (state.RunConditions.Length == 0 && state.SystemSetTypes.Length == 0)
			return true;

		var context = new SystemRunConditionContext(world, state.System, loopPhase, state.Stage, deltaTime);
		for (var i = 0; i < state.RunConditions.Length; i++)
		{
			if (!state.RunConditions[i].ShouldRun(in context))
				return false;
		}

		for (var i = 0; i < state.SystemSetTypes.Length; i++)
		{
			if (setRunConditionsByType.TryGetValue(state.SystemSetTypes[i], out var runCondition) &&
				!runCondition.ShouldRun(in context))
				return false;
		}

		return true;
	}

	private CommandStream[] ExecuteBatch(SystemBatch batch, World world)
	{
		if (batch.Systems.Count == 0) return [];

		var streams = new CommandStream[batch.Systems.Count];
		if (_maxDegreeOfParallelism == 1 || batch.Systems.Count == 1)
		{
			for (var i = 0; i < batch.Systems.Count; i++)
				ExecuteSystem(batch.Systems[i], world, streams, i);
		}
		else
		{
			ParallelWorkScheduler.Execute(
				batch.Systems.Count,
				_maxDegreeOfParallelism,
				i => ExecuteSystem(batch.Systems[i], world, streams, i)
			);
		}

		return streams;
	}

	private static void ExecuteSystem(SystemExecution execution, World world, CommandStream[] streams, int index)
	{
		var stream = world.CreateCommandStream();
		streams[index] = stream;
		var context = new SystemContext(execution.DeltaTime, execution.State.Stage, world, new(stream));
		execution.State.System.Update(in context);
	}

	private static void FlushStreams(World world, CommandStream[] streams)
	{
		for (var i = 0; i < streams.Length; i++)
		{
			var stream = streams[i];
			if (stream is null)
				continue;

			try
			{
				if (stream.HasCommands)
					world.Playback(stream);
			}
			finally
			{
				stream.Dispose();
			}
		}
	}
}
