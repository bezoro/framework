using Bezoro.ECS.Abstractions;
using Bezoro.ECS.Options;
using Bezoro.ECS.Services;

namespace Bezoro.ECS.Internal;

internal sealed class WorldSnapshotCoordinator(
	WorldEntityStore      entityStore,
	WorldLifecycleService lifecycleService,
	WorldQueryEngine      queryEngine,
	WorldSnapshotService  snapshotService)
{
	private readonly WorldEntityStore _entityStore = entityStore;
	private readonly WorldLifecycleService _lifecycleService = lifecycleService;
	private readonly WorldQueryEngine _queryEngine = queryEngine;
	private readonly WorldSnapshotService _snapshotService = snapshotService;

	public void Capture<TSnapshotWriter>(ref TSnapshotWriter writer)
		where TSnapshotWriter : struct, IWorldSnapshotWriter
	{
		EnsureNoActiveCursors("Snapshot capture cannot run while a query cursor is active.");
		_snapshotService.Capture(ref writer, _entityStore.AliveCount, _entityStore.NextEntityId);
	}

	public void Restore<TSnapshotReader>(
		ref TSnapshotReader             reader,
		SnapshotDeserializationOptions? options,
		int                             typeCount)
		where TSnapshotReader : struct, IWorldSnapshotReader
	{
		EnsureNoActiveCursors("Snapshot restore cannot run while a query cursor is active.");

		options ??= SnapshotDeserializationOptions.Default;
		var snapshot = reader.Read() ?? throw new InvalidOperationException("Snapshot reader returned null payload.");
		var restorePlan = _snapshotService.ValidateRestorePlan(snapshot, options, typeCount);
		try
		{
			_lifecycleService.Clear();
			_snapshotService.ApplyRestorePlan(restorePlan);
		}
		catch
		{
			_lifecycleService.Clear();
			throw;
		}
	}

	private void EnsureNoActiveCursors(string message)
	{
		if (_queryEngine.HasActiveCursors)
			throw new InvalidOperationException(message);
	}
}
