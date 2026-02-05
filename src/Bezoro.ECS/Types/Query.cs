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
	private readonly int[]      _excludeTypeIds;
	private readonly int[]      _typeIds;
	private readonly World      _world;

	internal Query(World world, Archetype? archetype, int[] typeIds, int[] excludeTypeIds)
	{
		_world          = world;
		_archetype      = archetype;
		_typeIds        = typeIds;
		_excludeTypeIds = excludeTypeIds;
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
		return new(_world, archetype, _typeIds, _excludeTypeIds);
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
	///     Adds a component type exclusion to this query. Entities with this component will be skipped.
	/// </summary>
	/// <typeparam name="T">The component type to exclude.</typeparam>
	/// <returns>A new query with the additional exclusion.</returns>
	public Query Without<T>() where T : struct, IComponent
	{
		int typeId = ComponentTypeRegistry.GetOrCreate<T>();
		return WithoutTypeId(typeId);
	}

	/// <summary>
	///     Adds component type exclusions to this query. Entities with any of these components will be skipped.
	/// </summary>
	/// <param name="componentTypes">The component types to exclude.</param>
	/// <returns>A new query with the additional exclusions.</returns>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="componentTypes" /> is null.</exception>
	/// <exception cref="ArgumentException">Thrown when a component type is invalid.</exception>
	public Query Without(params Type[] componentTypes)
	{
		if (componentTypes is null) throw new ArgumentNullException(nameof(componentTypes));

		var query = this;
		for (var i = 0; i < componentTypes.Length; i++)
		{
			var type   = componentTypes[i] ?? throw new ArgumentNullException(nameof(componentTypes));
			int typeId = ComponentTypeRegistry.GetOrCreate(type);
			query = query.WithoutTypeId(typeId);
		}

		return query;
	}

	/// <summary>
	///     Returns an enumerator for foreach iteration.
	/// </summary>
	public QueryEnumerator GetEnumerator() => new(_world, _archetype, _typeIds, _excludeTypeIds);

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
			ExecuteParallelForArchetype(_archetype, action, parallelism);
			return;
		}

		var views      = new List<ChunkView>();
		var archetypes = _world.Archetypes;
		for (var i = 0; i < archetypes.Count; i++)
		{
			var archetype = archetypes[i];
			if (!MatchesArchetype(archetype)) continue;

			var chunks = archetype.Chunks;
			for (var c = 0; c < chunks.Count; c++)
			{
				var chunk = chunks[c];
				if (chunk.Count == 0) continue;

				views.Add(new(chunk.Entities, chunk.Components, chunk.Count, archetype.TypeIndexById));
			}
		}

		if (views.Count == 0) return;

		var options = new ParallelOptions { MaxDegreeOfParallelism = parallelism };
		Parallel.For(0, views.Count, options, i => action(views[i]));
	}

	private bool MatchesArchetype(Archetype archetype)
	{
		if (_typeIds.Length > 0 && !archetype.ContainsAll(_typeIds)) return false;
		if (_excludeTypeIds.Length > 0 && archetype.ContainsAny(_excludeTypeIds)) return false;

		return true;
	}

	private void ExecuteParallelForArchetype(
		Archetype         archetype,
		Action<ChunkView> action,
		int               parallelism)
	{
		if (!MatchesArchetype(archetype)) return;

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
			return new(_world, _archetype, [typeId], _excludeTypeIds);

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

		return new(_world, _archetype, updated, _excludeTypeIds);
	}

	private Query WithoutTypeId(int typeId)
	{
		if (_excludeTypeIds.Length == 0)
			return new(_world, _archetype, _typeIds, [typeId]);

		for (var i = 0; i < _excludeTypeIds.Length; i++)
		{
			if (_excludeTypeIds[i] == typeId)
				return this;
		}

		var updated = new int[_excludeTypeIds.Length + 1];
		var index   = 0;
		var added   = false;

		for (var i = 0; i < _excludeTypeIds.Length; i++)
		{
			int current = _excludeTypeIds[i];
			if (!added && typeId < current)
			{
				updated[index++] = typeId;
				added            = true;
			}

			updated[index++] = current;
		}

		if (!added)
			updated[index] = typeId;

		return new(_world, _archetype, _typeIds, updated);
	}
}
