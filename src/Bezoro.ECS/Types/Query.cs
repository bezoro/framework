using Bezoro.ECS.Abstractions;
using Bezoro.ECS.Internal;
using Bezoro.ECS.Services;

namespace Bezoro.ECS.Types;

/// <summary>
///     Represents a query over entities with a configurable component set.
/// </summary>
public readonly struct Query
{
	private readonly Archetype? _archetype;
	private readonly int[]      _typeIds;
	private readonly World      _world;

	internal Query(World world, Archetype? archetype, int[] typeIds)
	{
		_world     = world;
		_archetype = archetype;
		_typeIds   = typeIds;
	}

	/// <summary>
	///     Restricts this query to a specific archetype.
	/// </summary>
	/// <param name="archetype">The archetype to query.</param>
	/// <returns>A new query restricted to the archetype.</returns>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="archetype" /> is null.</exception>
	/// <exception cref="InvalidOperationException">Thrown when the archetype belongs to a different world.</exception>
	public Query ForArchetype(Archetype archetype)
	{
		if (archetype is null) throw new ArgumentNullException(nameof(archetype));

		_world.EnsureOwnedArchetype(archetype);
		return new(_world, archetype, _typeIds);
	}

	/// <summary>
	///     Adds a component type requirement to this query.
	/// </summary>
	/// <typeparam name="T">The component type.</typeparam>
	/// <returns>A new query with the additional requirement.</returns>
	public Query With<T>() where T : struct, IComponent
	{
		int typeId = ComponentTypeRegistry.GetOrCreate<T>();
		return WithTypeId(typeId);
	}

	/// <summary>
	///     Adds component type requirements to this query.
	/// </summary>
	/// <param name="componentTypes">The component types.</param>
	/// <returns>A new query with the additional requirements.</returns>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="componentTypes" /> is null.</exception>
	/// <exception cref="ArgumentException">Thrown when a component type is invalid.</exception>
	public Query With(params Type[] componentTypes)
	{
		if (componentTypes is null) throw new ArgumentNullException(nameof(componentTypes));

		var query = this;
		for (var i = 0; i < componentTypes.Length; i++)
		{
			var type   = componentTypes[i] ?? throw new ArgumentNullException(nameof(componentTypes));
			int typeId = ComponentTypeRegistry.GetOrCreate(type);
			query = query.WithTypeId(typeId);
		}

		return query;
	}

	/// <summary>
	///     Returns an enumerator for foreach iteration.
	/// </summary>
	public QueryEnumerator GetEnumerator() => new(_world, _archetype, _typeIds);

	/// <summary>
	///     Executes an action for each matching chunk.
	/// </summary>
	/// <param name="action">The action to execute.</param>
	public void ForEach(Action<ChunkView> action)
	{
		if (action is null) throw new ArgumentNullException(nameof(action));

		for (var enumerator = GetEnumerator(); enumerator.MoveNext();)
			action(enumerator.Current);
	}

	/// <summary>
	///     Executes an action for each matching chunk in parallel.
	/// </summary>
	/// <param name="action">The action to execute.</param>
	/// <param name="maxDegreeOfParallelism">Optional maximum degree of parallelism.</param>
	public void ForEachParallel(Action<ChunkView> action, int? maxDegreeOfParallelism = null)
	{
		if (action is null) throw new ArgumentNullException(nameof(action));

		int parallelism = maxDegreeOfParallelism ?? _world.MaxDegreeOfParallelism;
		if (parallelism <= 1)
		{
			ForEach(action);
			return;
		}

		if (_archetype is { })
		{
			ExecuteParallelForArchetype(_archetype, _typeIds, action, parallelism);
			return;
		}

		var archetypes = _world.Archetypes;
		for (var i = 0; i < archetypes.Count; i++)
			ExecuteParallelForArchetype(archetypes[i], _typeIds, action, parallelism);
	}

	private static void ExecuteParallelForArchetype(
		Archetype         archetype,
		int[]             typeIds,
		Action<ChunkView> action,
		int               parallelism)
	{
		if (typeIds.Length > 0 && !archetype.ContainsAll(typeIds)) return;

		var options = new ParallelOptions { MaxDegreeOfParallelism = parallelism };
		var chunks  = archetype.Chunks;

		Parallel.For(
			0, chunks.Count, options, i =>
			{
				var chunk = chunks[i];
				if (chunk.Count == 0) return;

				action(new(chunk.Entities, chunk.Components, chunk.Count, archetype.TypeIndexById));
			}
		);
	}

	private Query WithTypeId(int typeId)
	{
		if (_typeIds.Length == 0)
			return new(_world, _archetype, [typeId]);

		for (var i = 0; i < _typeIds.Length; i++)
		{
			if (_typeIds[i] == typeId)
				return this;
		}

		var updated = new int[_typeIds.Length + 1];
		var index   = 0;
		var added   = false;

		for (var i = 0; i < _typeIds.Length; i++)
		{
			int current = _typeIds[i];
			if (!added && typeId < current)
			{
				updated[index++] = typeId;
				added            = true;
			}

			updated[index++] = current;
		}

		if (!added)
			updated[index] = typeId;

		return new(_world, _archetype, updated);
	}
}
