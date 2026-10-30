using Bezoro.ECS.Services;
using System.Runtime.ExceptionServices;

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
		List<Exception>? errors = null;
		Attempt(() => _systemRuntime.Shutdown(world), ref errors);
		Attempt(_entityStore.Dispose, ref errors);
		Attempt(_queryEngine.ClearCompiledPlans, ref errors);
		Attempt(_resourceStore.Dispose, ref errors);
		if (errors is { Count: 1 })
			ExceptionDispatchInfo.Capture(errors[0]).Throw();
		if (errors is not null)
			throw new AggregateException("One or more world services failed to dispose.", errors);
	}

	private static void Attempt(Action action, ref List<Exception>? errors)
	{
		try { action(); }
		catch (Exception error) { (errors ??= []).Add(error); }
	}

	public void Reset()
	{
		_entityStore.Reset();
		_changeTracker.Clear();
	}
}
