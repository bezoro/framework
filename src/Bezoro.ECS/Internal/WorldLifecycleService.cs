using Bezoro.ECS.Services;

namespace Bezoro.ECS.Internal;

internal sealed class WorldLifecycleService(
	WorldChangeTracker changeTracker,
	WorldEntityStore   entityStore,
	WorldQueryEngine   queryEngine,
	WorldResourceStore resourceStore,
	WorldSystemRuntime systemRuntime)
{
	private readonly WorldChangeTracker _changeTracker = changeTracker;
	private readonly WorldEntityStore _entityStore = entityStore;
	private readonly WorldQueryEngine _queryEngine = queryEngine;
	private readonly WorldResourceStore _resourceStore = resourceStore;
	private readonly WorldSystemRuntime _systemRuntime = systemRuntime;

	public void Clear()
	{
		Reset();
		_resourceStore.Clear();
	}

	public void Dispose(World world)
	{
		_systemRuntime.Shutdown(world);
		_entityStore.Dispose();
		_queryEngine.ClearCompiledPlans();
		_resourceStore.Dispose();
	}

	public void Reset()
	{
		_entityStore.Reset();
		_queryEngine.ExitCursors();
		_changeTracker.Clear();
	}
}
