using System.Buffers;
using Bezoro.ECS.Abstractions;
using Bezoro.ECS.Internal;
using Bezoro.ECS.Services;

namespace Bezoro.ECS.Types;

/// <summary>
///     Represents a cached archetype query.
/// </summary>
public sealed class Query
{
	private readonly Archetype? _archetype;
	private readonly QuerySpec  _spec;
	private readonly World      _world;

	/// <summary>
	///     A delegate that receives two components by mutable reference.
	/// </summary>
	public delegate void RefAction<T1, T2>(ref T1 component1, ref T2 component2)
		where T1 : struct
		where T2 : struct;

	/// <summary>
	///     A delegate that receives one component by mutable reference.
	/// </summary>
	public delegate void RefAction<T1>(ref T1 component1)
		where T1 : struct;

	/// <summary>
	///     A delegate that receives one component by mutable reference and three by read-only reference.
	/// </summary>
	public delegate void RefInAction<T1, T2, T3, T4>(
		ref T1 component1,
		in  T2 component2,
		in  T3 component3,
		in  T4 component4)
		where T1 : struct
		where T2 : struct
		where T3 : struct
		where T4 : struct;

	/// <summary>
	///     A delegate that receives one component by mutable reference and two by read-only reference.
	/// </summary>
	public delegate void RefInAction<T1, T2, T3>(ref T1 component1, in T2 component2, in T3 component3)
		where T1 : struct
		where T2 : struct
		where T3 : struct;

	/// <summary>
	///     A delegate that receives one component by mutable reference and one by read-only reference.
	/// </summary>
	public delegate void RefInAction<T1, T2>(ref T1 component1, in T2 component2)
		where T1 : struct
		where T2 : struct;

	internal Query(World world, Archetype? archetype, QuerySpec spec)
	{
		_world     = world;
		_archetype = archetype;
		_spec      = spec;
	}

	/// <summary>
	///     Adds a required component type to this query. Matching archetypes must contain this component.
	/// </summary>
	/// <typeparam name="T">The component type to require.</typeparam>
	/// <returns>A new query with the additional constraint, or this instance if the type was already present.</returns>
	public Query All<T>() where T : struct
	{
		int typeId = _world.GetOrCreateComponentTypeId<T>();
		var ids    = InsertSorted(typeId, _spec.AllTypeIds);
		if (ids is null) return this;

		return new(_world, _archetype, new(
			ids, _spec.NoneTypeIds, _spec.AnyTypeIds, _spec.OptionalTypeIds, _spec.ChangedTypeIds,
			_spec.RelatedRelationType, _spec.RelatedTarget
		));
	}

	/// <summary>
	///     Adds required component types to this query. Matching archetypes must contain all specified components.
	/// </summary>
	/// <param name="componentTypes">The component types to require.</param>
	/// <returns>A new query with the additional constraints.</returns>
	public Query All(params Type[] componentTypes)
	{
		if (componentTypes is null) throw new ArgumentNullException(nameof(componentTypes));

		var result = this;
		for (var i = 0; i < componentTypes.Length; i++)
		{
			var type   = componentTypes[i] ?? throw new ArgumentNullException(nameof(componentTypes));
			int typeId = result._world.GetOrCreateComponentTypeId(type);
			var ids    = InsertSorted(typeId, result._spec.AllTypeIds);
			if (ids is null) continue;

			result = new(result._world, result._archetype, new(
				ids, result._spec.NoneTypeIds, result._spec.AnyTypeIds, result._spec.OptionalTypeIds,
				result._spec.ChangedTypeIds, result._spec.RelatedRelationType, result._spec.RelatedTarget
			));
		}

		return result;
	}

	/// <summary>
	///     Adds two component types as an "any" constraint. Matching archetypes must contain at least one.
	/// </summary>
	/// <typeparam name="T1">The first component type.</typeparam>
	/// <typeparam name="T2">The second component type.</typeparam>
	/// <returns>A new query with the additional constraint.</returns>
	public Query Any<T1, T2>()
		where T1 : struct
		where T2 : struct
	{
		int typeId1 = _world.GetOrCreateComponentTypeId<T1>();
		int typeId2 = _world.GetOrCreateComponentTypeId<T2>();
		var result  = this;

		var ids1 = InsertSorted(typeId1, result._spec.AnyTypeIds);
		if (ids1 is not null)
			result = new(result._world, result._archetype, new(
				result._spec.AllTypeIds, result._spec.NoneTypeIds, ids1, result._spec.OptionalTypeIds,
				result._spec.ChangedTypeIds, result._spec.RelatedRelationType, result._spec.RelatedTarget
			));

		var ids2 = InsertSorted(typeId2, result._spec.AnyTypeIds);
		if (ids2 is not null)
			result = new(result._world, result._archetype, new(
				result._spec.AllTypeIds, result._spec.NoneTypeIds, ids2, result._spec.OptionalTypeIds,
				result._spec.ChangedTypeIds, result._spec.RelatedRelationType, result._spec.RelatedTarget
			));

		return result;
	}

	/// <summary>
	///     Adds component types as an "any" constraint. Matching archetypes must contain at least one.
	/// </summary>
	/// <param name="componentTypes">The component types, at least one of which must be present.</param>
	/// <returns>A new query with the additional constraint.</returns>
	public Query Any(params Type[] componentTypes)
	{
		if (componentTypes is null) throw new ArgumentNullException(nameof(componentTypes));

		var result = this;
		for (var i = 0; i < componentTypes.Length; i++)
		{
			var type   = componentTypes[i] ?? throw new ArgumentNullException(nameof(componentTypes));
			int typeId = result._world.GetOrCreateComponentTypeId(type);
			var ids    = InsertSorted(typeId, result._spec.AnyTypeIds);
			if (ids is null) continue;

			result = new(result._world, result._archetype, new(
				result._spec.AllTypeIds, result._spec.NoneTypeIds, ids, result._spec.OptionalTypeIds,
				result._spec.ChangedTypeIds, result._spec.RelatedRelationType, result._spec.RelatedTarget
			));
		}

		return result;
	}

	/// <summary>
	///     Adds a changed-component filter. Only chunks where the specified component was modified this tick are matched.
	/// </summary>
	/// <typeparam name="T">The component type to filter by change status.</typeparam>
	/// <returns>A new query with the change filter applied.</returns>
	public Query Changed<T>() where T : struct
	{
		int typeId = _world.GetOrCreateComponentTypeId<T>();
		var ids    = InsertSorted(typeId, _spec.ChangedTypeIds);
		if (ids is null) return this;

		return new(_world, _archetype, new(
			_spec.AllTypeIds, _spec.NoneTypeIds, _spec.AnyTypeIds, _spec.OptionalTypeIds, ids,
			_spec.RelatedRelationType, _spec.RelatedTarget
		));
	}

	/// <summary>
	///     Restricts this query to a single archetype instead of searching all archetypes in the world.
	/// </summary>
	/// <param name="archetype">The archetype to query against.</param>
	/// <returns>A new query scoped to the specified archetype.</returns>
	public Query ForArchetype(Archetype archetype)
	{
		if (archetype is null) throw new ArgumentNullException(nameof(archetype));

		_world.EnsureOwnedArchetype(archetype);
		return new(_world, archetype, _spec);
	}

	/// <summary>
	///     Excludes archetypes that contain the specified component type.
	/// </summary>
	/// <typeparam name="T">The component type to exclude.</typeparam>
	/// <returns>A new query with the exclusion constraint.</returns>
	public Query None<T>() where T : struct
	{
		int typeId = _world.GetOrCreateComponentTypeId<T>();
		var ids    = InsertSorted(typeId, _spec.NoneTypeIds);
		if (ids is null) return this;

		return new(_world, _archetype, new(
			_spec.AllTypeIds, ids, _spec.AnyTypeIds, _spec.OptionalTypeIds, _spec.ChangedTypeIds,
			_spec.RelatedRelationType, _spec.RelatedTarget
		));
	}

	/// <summary>
	///     Excludes archetypes that contain any of the specified component types.
	/// </summary>
	/// <param name="componentTypes">The component types to exclude.</param>
	/// <returns>A new query with the exclusion constraints.</returns>
	public Query None(params Type[] componentTypes)
	{
		if (componentTypes is null) throw new ArgumentNullException(nameof(componentTypes));

		var result = this;
		for (var i = 0; i < componentTypes.Length; i++)
		{
			var type   = componentTypes[i] ?? throw new ArgumentNullException(nameof(componentTypes));
			int typeId = result._world.GetOrCreateComponentTypeId(type);
			var ids    = InsertSorted(typeId, result._spec.NoneTypeIds);
			if (ids is null) continue;

			result = new(result._world, result._archetype, new(
				result._spec.AllTypeIds, ids, result._spec.AnyTypeIds, result._spec.OptionalTypeIds,
				result._spec.ChangedTypeIds, result._spec.RelatedRelationType, result._spec.RelatedTarget
			));
		}

		return result;
	}

	/// <summary>
	///     Marks a component type as optional. The component will be included if present but will not filter archetypes.
	/// </summary>
	/// <typeparam name="T">The optional component type.</typeparam>
	/// <returns>A new query with the optional component.</returns>
	public Query Optional<T>() where T : struct
	{
		int typeId = _world.GetOrCreateComponentTypeId<T>();
		var ids    = InsertSorted(typeId, _spec.OptionalTypeIds);
		if (ids is null) return this;

		return new(_world, _archetype, new(
			_spec.AllTypeIds, _spec.NoneTypeIds, _spec.AnyTypeIds, ids, _spec.ChangedTypeIds,
			_spec.RelatedRelationType, _spec.RelatedTarget
		));
	}

	/// <summary>
	///     Filters for entities that have a relationship of the specified type targeting the given entity.
	/// </summary>
	/// <typeparam name="TRelation">The relationship type.</typeparam>
	/// <param name="target">The target entity of the relationship, or <see cref="Entity.Wildcard" /> for any target.</param>
	/// <returns>A new query with the relationship constraint.</returns>
	public Query Related<TRelation>(Entity target)
	{
		if (target == Entity.Wildcard)
			return new(
				_world, _archetype,
				new(
					_spec.AllTypeIds, _spec.NoneTypeIds, _spec.AnyTypeIds, _spec.OptionalTypeIds, _spec.ChangedTypeIds,
					typeof(TRelation), Entity.Wildcard
				)
			);

		_world.EnsureEntityAlive(target);
		return new(_world, _archetype, new(
			_spec.AllTypeIds, _spec.NoneTypeIds, _spec.AnyTypeIds, _spec.OptionalTypeIds, _spec.ChangedTypeIds,
			typeof(TRelation), target
		));
	}

	/// <summary>
	///     Returns an enumerator that iterates over matching <see cref="ChunkView" /> instances.
	/// </summary>
	/// <returns>A <see cref="QueryEnumerator" /> for this query.</returns>
	public QueryEnumerator GetEnumerator() => new(_world, _archetype, _spec);

	/// <summary>
	///     Iterates over all matching chunks and invokes the specified action for each.
	/// </summary>
	/// <param name="action">The action to invoke for each matching chunk.</param>
	public void ForEach(Action<ChunkView> action)
	{
		if (action is null) throw new ArgumentNullException(nameof(action));

		var enumerator = GetEnumerator();
		try
		{
			while (enumerator.MoveNext())
				action(enumerator.Current);
		}
		finally
		{
			enumerator.Dispose();
		}
	}

	/// <summary>
	///     Iterates over all matching entities and invokes the action with a mutable reference to the component.
	/// </summary>
	/// <typeparam name="T1">The component type to access (read-write).</typeparam>
	/// <param name="action">The action to invoke per entity.</param>
	public void ForEach<T1>(RefAction<T1> action)
		where T1 : struct
	{
		if (action is null) throw new ArgumentNullException(nameof(action));
		int typeId1 = _world.GetOrCreateComponentTypeId<T1>();

		foreach (var chunk in this)
		{
			var components1 = chunk.ComponentsByTypeId<T1>(typeId1);
			for (var i = 0; i < chunk.Count; i++)
				action(ref components1[i]);
		}
	}

	/// <summary>
	///     Iterates over all matching entities with one read-write and one read-only component.
	/// </summary>
	/// <typeparam name="T1">The component type to access (read-write).</typeparam>
	/// <typeparam name="T2">The component type to access (read-only).</typeparam>
	/// <param name="action">The action to invoke per entity.</param>
	public void ForEach<T1, T2>(RefInAction<T1, T2> action)
		where T1 : struct
		where T2 : struct
	{
		if (action is null) throw new ArgumentNullException(nameof(action));
		int typeId1 = _world.GetOrCreateComponentTypeId<T1>();
		int typeId2 = _world.GetOrCreateComponentTypeId<T2>();

		foreach (var chunk in this)
		{
			var components1 = chunk.ComponentsByTypeId<T1>(typeId1);
			var components2 = chunk.ReadOnlyComponentsByTypeId<T2>(typeId2);
			for (var i = 0; i < chunk.Count; i++)
				action(ref components1[i], in components2[i]);
		}
	}

	/// <summary>
	///     Iterates over all matching entities with one read-write and two read-only components.
	/// </summary>
	/// <typeparam name="T1">The component type to access (read-write).</typeparam>
	/// <typeparam name="T2">The first component type to access (read-only).</typeparam>
	/// <typeparam name="T3">The second component type to access (read-only).</typeparam>
	/// <param name="action">The action to invoke per entity.</param>
	public void ForEach<T1, T2, T3>(RefInAction<T1, T2, T3> action)
		where T1 : struct
		where T2 : struct
		where T3 : struct
	{
		if (action is null) throw new ArgumentNullException(nameof(action));
		int typeId1 = _world.GetOrCreateComponentTypeId<T1>();
		int typeId2 = _world.GetOrCreateComponentTypeId<T2>();
		int typeId3 = _world.GetOrCreateComponentTypeId<T3>();

		foreach (var chunk in this)
		{
			var components1 = chunk.ComponentsByTypeId<T1>(typeId1);
			var components2 = chunk.ReadOnlyComponentsByTypeId<T2>(typeId2);
			var components3 = chunk.ReadOnlyComponentsByTypeId<T3>(typeId3);
			for (var i = 0; i < chunk.Count; i++)
				action(ref components1[i], in components2[i], in components3[i]);
		}
	}

	/// <summary>
	///     Iterates over all matching entities with one read-write and three read-only components.
	/// </summary>
	/// <typeparam name="T1">The component type to access (read-write).</typeparam>
	/// <typeparam name="T2">The first component type to access (read-only).</typeparam>
	/// <typeparam name="T3">The second component type to access (read-only).</typeparam>
	/// <typeparam name="T4">The third component type to access (read-only).</typeparam>
	/// <param name="action">The action to invoke per entity.</param>
	public void ForEach<T1, T2, T3, T4>(RefInAction<T1, T2, T3, T4> action)
		where T1 : struct
		where T2 : struct
		where T3 : struct
		where T4 : struct
	{
		if (action is null) throw new ArgumentNullException(nameof(action));
		int typeId1 = _world.GetOrCreateComponentTypeId<T1>();
		int typeId2 = _world.GetOrCreateComponentTypeId<T2>();
		int typeId3 = _world.GetOrCreateComponentTypeId<T3>();
		int typeId4 = _world.GetOrCreateComponentTypeId<T4>();

		foreach (var chunk in this)
		{
			var components1 = chunk.ComponentsByTypeId<T1>(typeId1);
			var components2 = chunk.ReadOnlyComponentsByTypeId<T2>(typeId2);
			var components3 = chunk.ReadOnlyComponentsByTypeId<T3>(typeId3);
			var components4 = chunk.ReadOnlyComponentsByTypeId<T4>(typeId4);
			for (var i = 0; i < chunk.Count; i++)
				action(ref components1[i], in components2[i], in components3[i], in components4[i]);
		}
	}

	/// <summary>
	///     Runs a struct-based job over all matching entities with one read-write component.
	/// </summary>
	/// <typeparam name="TJob">The job type implementing <see cref="IForEach{T1}" />.</typeparam>
	/// <typeparam name="T1">The component type to access (read-write).</typeparam>
	/// <param name="job">The job instance to execute per entity.</param>
	public void Run<TJob, T1>(TJob job)
		where TJob : struct, IForEach<T1>
		where T1 : struct
	{
		int typeId1 = _world.GetOrCreateComponentTypeId<T1>();
		foreach (var chunk in this)
		{
			var components1 = chunk.ComponentsByTypeId<T1>(typeId1);
			for (var i = 0; i < chunk.Count; i++)
				job.Execute(ref components1[i]);
		}
	}

	/// <summary>
	///     Runs a struct-based job over all matching entities with one read-write and one read-only component.
	/// </summary>
	/// <typeparam name="TJob">The job type implementing <see cref="IForEach{T1, T2}" />.</typeparam>
	/// <typeparam name="T1">The component type to access (read-write).</typeparam>
	/// <typeparam name="T2">The component type to access (read-only).</typeparam>
	/// <param name="job">The job instance to execute per entity.</param>
	public void Run<TJob, T1, T2>(TJob job)
		where TJob : struct, IForEach<T1, T2>
		where T1 : struct
		where T2 : struct
	{
		int typeId1 = _world.GetOrCreateComponentTypeId<T1>();
		int typeId2 = _world.GetOrCreateComponentTypeId<T2>();
		foreach (var chunk in this)
		{
			var components1 = chunk.ComponentsByTypeId<T1>(typeId1);
			var components2 = chunk.ReadOnlyComponentsByTypeId<T2>(typeId2);
			for (var i = 0; i < chunk.Count; i++)
				job.Execute(ref components1[i], in components2[i]);
		}
	}

	/// <summary>
	///     Runs a struct-based job over all matching entities with one read-write and two read-only components.
	/// </summary>
	/// <typeparam name="TJob">The job type implementing <see cref="IForEach{T1, T2, T3}" />.</typeparam>
	/// <typeparam name="T1">The component type to access (read-write).</typeparam>
	/// <typeparam name="T2">The first component type to access (read-only).</typeparam>
	/// <typeparam name="T3">The second component type to access (read-only).</typeparam>
	/// <param name="job">The job instance to execute per entity.</param>
	public void Run<TJob, T1, T2, T3>(TJob job)
		where TJob : struct, IForEach<T1, T2, T3>
		where T1 : struct
		where T2 : struct
		where T3 : struct
	{
		int typeId1 = _world.GetOrCreateComponentTypeId<T1>();
		int typeId2 = _world.GetOrCreateComponentTypeId<T2>();
		int typeId3 = _world.GetOrCreateComponentTypeId<T3>();
		foreach (var chunk in this)
		{
			var components1 = chunk.ComponentsByTypeId<T1>(typeId1);
			var components2 = chunk.ReadOnlyComponentsByTypeId<T2>(typeId2);
			var components3 = chunk.ReadOnlyComponentsByTypeId<T3>(typeId3);
			for (var i = 0; i < chunk.Count; i++)
				job.Execute(ref components1[i], in components2[i], in components3[i]);
		}
	}

	/// <summary>
	///     Runs a struct-based job over all matching entities with one read-write and three read-only components.
	/// </summary>
	/// <typeparam name="TJob">The job type implementing <see cref="IForEach{T1, T2, T3, T4}" />.</typeparam>
	/// <typeparam name="T1">The component type to access (read-write).</typeparam>
	/// <typeparam name="T2">The first component type to access (read-only).</typeparam>
	/// <typeparam name="T3">The second component type to access (read-only).</typeparam>
	/// <typeparam name="T4">The third component type to access (read-only).</typeparam>
	/// <param name="job">The job instance to execute per entity.</param>
	public void Run<TJob, T1, T2, T3, T4>(TJob job)
		where TJob : struct, IForEach<T1, T2, T3, T4>
		where T1 : struct
		where T2 : struct
		where T3 : struct
		where T4 : struct
	{
		int typeId1 = _world.GetOrCreateComponentTypeId<T1>();
		int typeId2 = _world.GetOrCreateComponentTypeId<T2>();
		int typeId3 = _world.GetOrCreateComponentTypeId<T3>();
		int typeId4 = _world.GetOrCreateComponentTypeId<T4>();
		foreach (var chunk in this)
		{
			var components1 = chunk.ComponentsByTypeId<T1>(typeId1);
			var components2 = chunk.ReadOnlyComponentsByTypeId<T2>(typeId2);
			var components3 = chunk.ReadOnlyComponentsByTypeId<T3>(typeId3);
			var components4 = chunk.ReadOnlyComponentsByTypeId<T4>(typeId4);
			for (var i = 0; i < chunk.Count; i++)
				job.Execute(ref components1[i], in components2[i], in components3[i], in components4[i]);
		}
	}

	/// <summary>
	///     Iterates over all matching chunks in parallel using the world's thread pool.
	/// </summary>
	/// <param name="action">The action to invoke for each matching chunk.</param>
	/// <param name="maxDegreeOfParallelism">
	///     The maximum number of concurrent threads. Defaults to <see cref="World.MaxDegreeOfParallelism" />.
	/// </param>
	public void ForEachParallel(Action<ChunkView> action, int? maxDegreeOfParallelism = null)
	{
		if (action is null) throw new ArgumentNullException(nameof(action));

		int parallelism = maxDegreeOfParallelism ?? _world.MaxDegreeOfParallelism;
		if (parallelism <= 1)
		{
			ForEach(action);
			return;
		}

		var matches = _archetype is not null
						  ? (IReadOnlyList<Archetype>)[_archetype]
						  : _world.GetOrCreateQueryMatches(_spec);

		var currentVersion = _world.ChangeVersion;
		var workItemCount  = 0;
		for (var a = 0; a < matches.Count; a++)
		{
			var archetype = matches[a];
			for (var c = 0; c < archetype.Chunks.Count; c++)
			{
				var chunk = archetype.Chunks[c];
				if (chunk.Count == 0) continue;

				if (!MatchesChangedChunk(_spec, archetype, chunk, currentVersion))
					continue;

				workItemCount++;
			}
		}

		if (workItemCount == 0) return;

		var workItems = ArrayPool<(int ArchetypeIndex, int ChunkIndex)>.Shared.Rent(workItemCount);
		workItemCount = 0;
		for (var a = 0; a < matches.Count; a++)
		{
			var archetype = matches[a];
			for (var c = 0; c < archetype.Chunks.Count; c++)
			{
				var chunk = archetype.Chunks[c];
				if (chunk.Count == 0) continue;

				if (!MatchesChangedChunk(_spec, archetype, chunk, currentVersion))
					continue;

				workItems[workItemCount++] = (a, c);
			}
		}

		_world.EnterQueryIteration();
		try
		{
			ParallelWorkScheduler.Execute(workItemCount, parallelism, i =>
			{
				var (archetypeIndex, chunkIndex) = workItems[i];
				var archetype = matches[archetypeIndex];
				var chunk = archetype.Chunks[chunkIndex];
				var view = new ChunkView(
					chunk.Entities, chunk.Columns, chunk.Count,
					archetype.TypeIndexById, chunk.ComponentVersions,
					_world.ChangeVersion, true, chunk, _world
				);
				action(view);
			});
		}
		finally
		{
			_world.ExitQueryIteration();
			ArrayPool<(int ArchetypeIndex, int ChunkIndex)>.Shared.Return(workItems);
		}
	}

	/// <summary>
	///     Iterates over all matching entities with two read-write components.
	/// </summary>
	/// <typeparam name="T1">The first component type to access (read-write).</typeparam>
	/// <typeparam name="T2">The second component type to access (read-write).</typeparam>
	/// <param name="action">The action to invoke per entity.</param>
	public void ForEachRW<T1, T2>(RefAction<T1, T2> action)
		where T1 : struct
		where T2 : struct
	{
		if (action is null) throw new ArgumentNullException(nameof(action));
		int typeId1 = _world.GetOrCreateComponentTypeId<T1>();
		int typeId2 = _world.GetOrCreateComponentTypeId<T2>();

		foreach (var chunk in this)
		{
			var components1 = chunk.ComponentsByTypeId<T1>(typeId1);
			var components2 = chunk.ComponentsByTypeId<T2>(typeId2);
			for (var i = 0; i < chunk.Count; i++)
				action(ref components1[i], ref components2[i]);
		}
	}

	private static int[]? InsertSorted(int typeId, int[] source)
	{
		for (var i = 0; i < source.Length; i++)
		{
			if (source[i] == typeId)
				return null;
		}

		var updated = new int[source.Length + 1];
		var index   = 0;
		var added   = false;

		for (var i = 0; i < source.Length; i++)
		{
			int current = source[i];
			if (!added && typeId < current)
			{
				updated[index++] = typeId;
				added            = true;
			}

			updated[index++] = current;
		}

		if (!added)
			updated[index] = typeId;

		return updated;
	}

	private static bool MatchesChangedChunk(QuerySpec spec, Archetype archetype, Chunk chunk, uint currentVersion)
	{
		if (spec.ChangedTypeIds.Length == 0) return true;

		for (var i = 0; i < spec.ChangedTypeIds.Length; i++)
		{
			int typeId         = spec.ChangedTypeIds[i];
			int componentIndex = archetype.GetTypeIndex(typeId);
			if (componentIndex < 0 ||
				chunk.ComponentVersions[componentIndex] != currentVersion)
				return false;
		}

		return true;
	}
}
