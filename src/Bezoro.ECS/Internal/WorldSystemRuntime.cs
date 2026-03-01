using Bezoro.ECS.Abstractions;
using Bezoro.ECS.Services;
using Bezoro.ECS.Types;

namespace Bezoro.ECS.Internal;

internal sealed class WorldSystemRuntime
{
	private readonly SystemManager _systemManager;
	private int _updateDepth;

	public WorldSystemRuntime(int maxDegreeOfParallelism)
	{
		_systemManager = new(maxDegreeOfParallelism);
	}

	public int PlanBuildCount => _systemManager.PlanBuildCount;

	public void AddSystem(World world, ISystem system, Stage stage) =>
		_systemManager.RegisterSystem(world, system, stage);

	public void ClearSystemSetRunCondition(Type setType) =>
		_systemManager.ClearSystemSetRunCondition(setType);

	public ScheduleDiagnostics GetDiagnostics() => _systemManager.GetDiagnostics();

	public bool IsSystemSetEnabled(Type setType) => _systemManager.IsSystemSetEnabled(setType);

	public void RunPhase(World world, SystemLoopPhase loopPhase, float deltaTime)
	{
		if (Interlocked.Increment(ref _updateDepth) != 1)
		{
			Interlocked.Decrement(ref _updateDepth);
			throw new InvalidOperationException("Re-entrant world updates are not supported.");
		}

		try
		{
			_systemManager.UpdatePhase(world, loopPhase, deltaTime);
		}
		finally
		{
			Interlocked.Decrement(ref _updateDepth);
		}
	}

	public void SetSystemSetEnabled(Type setType, bool enabled) =>
		_systemManager.SetSystemSetEnabled(setType, enabled);

	public void SetSystemSetRunCondition(Type setType, ISystemRunCondition runCondition) =>
		_systemManager.SetSystemSetRunCondition(setType, runCondition);

	public void Shutdown(World world) => _systemManager.Shutdown(world);
}
