using Bezoro.ECS.Internal;
using Bezoro.ECS.Services;

namespace Bezoro.ECS.Types;

/// <summary>
/// Enumerates chunk views for a query.
/// </summary>
public struct QueryEnumerator : IDisposable
{
	private readonly Archetype? _archetype;
	private readonly QuerySpec _spec;
	private readonly World _world;
	private readonly IReadOnlyList<Archetype> _matches;
	private int _archetypeIndex;
	private int _chunkIndex;
	private bool _entered;

	internal QueryEnumerator(World world, Archetype? archetype, QuerySpec spec)
	{
		_world = world;
		_archetype = archetype;
		_spec = spec;
		_archetypeIndex = 0;
		_chunkIndex = 0;
		Current = default;
		_entered = true;
		_world.EnterQueryIteration();
		_matches = archetype is null ? _world.GetOrCreateQueryMatches(spec) : [archetype];
	}

	public ChunkView Current { get; private set; }

	public bool MoveNext()
	{
		while (_archetypeIndex < _matches.Count)
		{
			var archetype = _matches[_archetypeIndex];
			var chunks = archetype.Chunks;
			while (_chunkIndex < chunks.Count)
			{
				var chunk = chunks[_chunkIndex++];
				if (chunk.Count == 0) continue;

				var view = new ChunkView(
					chunk.Entities,
					chunk.Columns,
					chunk.Count,
					archetype.TypeIndexById,
					chunk.ComponentVersions,
					_world.ChangeVersion,
					trackWrites: true,
					chunk);

				if (!MatchesChanged(view)) continue;

				Current = view;
				return true;
			}

			_chunkIndex = 0;
			_archetypeIndex++;
		}

		Dispose();
		return false;
	}

	public void Dispose()
	{
		if (!_entered) return;
		_entered = false;
		_world.ExitQueryIteration();
	}

	private bool MatchesChanged(ChunkView view)
	{
		if (_spec.ChangedTypeIds.Length == 0) return true;

		for (var i = 0; i < _spec.ChangedTypeIds.Length; i++)
		{
			if (!view.IsChanged(_spec.ChangedTypeIds[i]))
				return false;
		}

		return true;
	}
}
