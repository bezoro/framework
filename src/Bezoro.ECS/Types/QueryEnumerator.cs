using Bezoro.ECS.Services;

namespace Bezoro.ECS.Types;

/// <summary>
///     Enumerates chunk views for a query.
/// </summary>
public struct QueryEnumerator
{
	private readonly Archetype? _archetype;
	private readonly int[]      _excludeTypeIds;
	private readonly int[]      _typeIds;
	private readonly World      _world;
	private          Archetype? _currentArchetype;
	private          int        _archetypeIndex;
	private          int        _chunkIndex;

	internal QueryEnumerator(World world, Archetype? archetype, int[] typeIds, int[] excludeTypeIds)
	{
		_world            = world;
		_archetype        = archetype;
		_typeIds          = typeIds;
		_excludeTypeIds   = excludeTypeIds;
		_archetypeIndex   = 0;
		_chunkIndex       = 0;
		_currentArchetype = null;
		Current           = default;
	}

	/// <summary>
	///     Gets the current chunk view.
	/// </summary>
	public ChunkView Current { get; private set; }

	/// <summary>
	///     Advances to the next chunk view.
	/// </summary>
	public bool MoveNext()
	{
		if (_archetype is { })
			return MoveNextInArchetype(_archetype);

		var archetypes = _world.Archetypes;
		while (true)
		{
			if (_currentArchetype is null)
			{
				if (_archetypeIndex >= archetypes.Count) return false;

				_currentArchetype = archetypes[_archetypeIndex++];
				_chunkIndex       = 0;

				if (!MatchesArchetype(_currentArchetype))
				{
					_currentArchetype = null;
					continue;
				}
			}

			if (MoveNextInArchetype(_currentArchetype))
				return true;

			_currentArchetype = null;
		}
	}

	private bool MatchesArchetype(Archetype archetype)
	{
		if (_typeIds.Length > 0 && !archetype.ContainsAll(_typeIds)) return false;
		if (_excludeTypeIds.Length > 0 && archetype.ContainsAny(_excludeTypeIds)) return false;

		return true;
	}

	private bool MoveNextInArchetype(Archetype archetype)
	{
		if (!MatchesArchetype(archetype))
			return false;

		var chunks = archetype.Chunks;
		while (_chunkIndex < chunks.Count)
		{
			var chunk = chunks[_chunkIndex++];
			if (chunk.Count == 0) continue;

			Current = new(chunk.Entities, chunk.Components, chunk.Count, archetype.TypeIndexById);
			return true;
		}

		return false;
	}
}
