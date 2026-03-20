using Bezoro.ECS.Internal.Fixed;
using Bezoro.ECS.Services;
using Bezoro.ECS.Types;

namespace Bezoro.ECS.Internal;

internal sealed class QueryExecutionLease
{
	private bool               _usesSharedScratch;
	private Entity[]?          _entities;
	private int                _disposed;
	private QueryChunkMatch[]? _chunkMatches;
	private World?             _world;

	internal void Initialize(
		World             world,
		QueryChunkMatch[] chunkMatches,
		Entity[]          entities,
		bool              usesSharedScratch)
	{
		_world             = world;
		_chunkMatches      = chunkMatches;
		_entities          = entities;
		_usesSharedScratch = usesSharedScratch;
		_disposed          = 0;
	}

	internal void Dispose()
	{
		if (Interlocked.Exchange(ref _disposed, 1) != 0)
			return;

		var  world        = _world;
		var  chunkMatches = _chunkMatches;
		var  entities     = _entities;
		bool usesShared   = _usesSharedScratch;

		_world             = null;
		_chunkMatches      = null;
		_entities          = null;
		_usesSharedScratch = false;

		if (world is null || chunkMatches is null || entities is null)
			return;

		world.ReleaseCursorQueryExecution(chunkMatches, entities, usesShared, this);
	}
}
