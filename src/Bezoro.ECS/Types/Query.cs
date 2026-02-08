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

	public delegate void RefAction<T1, T2>(ref T1 component1, ref T2 component2)
		where T1 : struct
		where T2 : struct;

	public delegate void RefAction<T1>(ref T1 component1)
		where T1 : struct;

	public delegate void RefInAction<T1, T2, T3, T4>(
		ref T1 component1,
		in  T2 component2,
		in  T3 component3,
		in  T4 component4)
		where T1 : struct
		where T2 : struct
		where T3 : struct
		where T4 : struct;

	public delegate void RefInAction<T1, T2, T3>(ref T1 component1, in T2 component2, in T3 component3)
		where T1 : struct
		where T2 : struct
		where T3 : struct;

	public delegate void RefInAction<T1, T2>(ref T1 component1, in T2 component2)
		where T1 : struct
		where T2 : struct;

	internal Query(World world, Archetype? archetype, QuerySpec spec)
	{
		_world     = world;
		_archetype = archetype;
		_spec      = spec;
	}

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

	public Query ForArchetype(Archetype archetype)
	{
		if (archetype is null) throw new ArgumentNullException(nameof(archetype));

		_world.EnsureOwnedArchetype(archetype);
		return new(_world, archetype, _spec);
	}

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

		int relationTypeId = _world.GetOrCreateRelationshipTypeId(typeof(TRelation), target);
		var ids            = InsertSorted(relationTypeId, _spec.AllTypeIds);
		if (ids is null) return this;

		return new(_world, _archetype, new(
			ids, _spec.NoneTypeIds, _spec.AnyTypeIds, _spec.OptionalTypeIds, _spec.ChangedTypeIds,
			typeof(TRelation), target
		));
	}

	public QueryEnumerator GetEnumerator() => new(_world, _archetype, _spec);

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

	public void ForEach<T1>(RefAction<T1> action)
		where T1 : struct
	{
		if (action is null) throw new ArgumentNullException(nameof(action));

		foreach (var chunk in this)
		{
			var components1 = chunk.Components<T1>();
			for (var i = 0; i < chunk.Count; i++)
				action(ref components1[i]);
		}
	}

	public void ForEach<T1, T2>(RefInAction<T1, T2> action)
		where T1 : struct
		where T2 : struct
	{
		if (action is null) throw new ArgumentNullException(nameof(action));

		foreach (var chunk in this)
		{
			var components1 = chunk.Components<T1>();
			var components2 = chunk.ReadOnlyComponents<T2>();
			for (var i = 0; i < chunk.Count; i++)
				action(ref components1[i], in components2[i]);
		}
	}

	public void ForEach<T1, T2, T3>(RefInAction<T1, T2, T3> action)
		where T1 : struct
		where T2 : struct
		where T3 : struct
	{
		if (action is null) throw new ArgumentNullException(nameof(action));

		foreach (var chunk in this)
		{
			var components1 = chunk.Components<T1>();
			var components2 = chunk.ReadOnlyComponents<T2>();
			var components3 = chunk.ReadOnlyComponents<T3>();
			for (var i = 0; i < chunk.Count; i++)
				action(ref components1[i], in components2[i], in components3[i]);
		}
	}

	public void ForEach<T1, T2, T3, T4>(RefInAction<T1, T2, T3, T4> action)
		where T1 : struct
		where T2 : struct
		where T3 : struct
		where T4 : struct
	{
		if (action is null) throw new ArgumentNullException(nameof(action));

		foreach (var chunk in this)
		{
			var components1 = chunk.Components<T1>();
			var components2 = chunk.ReadOnlyComponents<T2>();
			var components3 = chunk.ReadOnlyComponents<T3>();
			var components4 = chunk.ReadOnlyComponents<T4>();
			for (var i = 0; i < chunk.Count; i++)
				action(ref components1[i], in components2[i], in components3[i], in components4[i]);
		}
	}

	public void Run<TJob, T1>(TJob job)
		where TJob : struct, IForEach<T1>
		where T1 : struct
	{
		foreach (var chunk in this)
		{
			var components1 = chunk.Components<T1>();
			for (var i = 0; i < chunk.Count; i++)
				job.Execute(ref components1[i]);
		}
	}

	public void Run<TJob, T1, T2>(TJob job)
		where TJob : struct, IForEach<T1, T2>
		where T1 : struct
		where T2 : struct
	{
		foreach (var chunk in this)
		{
			var components1 = chunk.Components<T1>();
			var components2 = chunk.ReadOnlyComponents<T2>();
			for (var i = 0; i < chunk.Count; i++)
				job.Execute(ref components1[i], in components2[i]);
		}
	}

	public void Run<TJob, T1, T2, T3>(TJob job)
		where TJob : struct, IForEach<T1, T2, T3>
		where T1 : struct
		where T2 : struct
		where T3 : struct
	{
		foreach (var chunk in this)
		{
			var components1 = chunk.Components<T1>();
			var components2 = chunk.ReadOnlyComponents<T2>();
			var components3 = chunk.ReadOnlyComponents<T3>();
			for (var i = 0; i < chunk.Count; i++)
				job.Execute(ref components1[i], in components2[i], in components3[i]);
		}
	}

	public void Run<TJob, T1, T2, T3, T4>(TJob job)
		where TJob : struct, IForEach<T1, T2, T3, T4>
		where T1 : struct
		where T2 : struct
		where T3 : struct
		where T4 : struct
	{
		foreach (var chunk in this)
		{
			var components1 = chunk.Components<T1>();
			var components2 = chunk.ReadOnlyComponents<T2>();
			var components3 = chunk.ReadOnlyComponents<T3>();
			var components4 = chunk.ReadOnlyComponents<T4>();
			for (var i = 0; i < chunk.Count; i++)
				job.Execute(ref components1[i], in components2[i], in components3[i], in components4[i]);
		}
	}

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

		var workItems = new List<(Archetype Archetype, int ChunkIndex)>();
		for (var a = 0; a < matches.Count; a++)
		{
			var archetype = matches[a];
			for (var c = 0; c < archetype.Chunks.Count; c++)
			{
				var chunk = archetype.Chunks[c];
				if (chunk.Count == 0) continue;

				if (_spec.ChangedTypeIds.Length > 0)
				{
					bool matchesChanged = true;
					for (var i = 0; i < _spec.ChangedTypeIds.Length; i++)
					{
						int typeId         = _spec.ChangedTypeIds[i];
						int componentIndex = archetype.GetTypeIndex(typeId);
						if (componentIndex < 0 ||
							chunk.ComponentVersions[componentIndex] != _world.ChangeVersion)
						{
							matchesChanged = false;
							break;
						}
					}

					if (!matchesChanged) continue;
				}

				workItems.Add((archetype, c));
			}
		}

		if (workItems.Count == 0) return;

		_world.EnterQueryIteration();
		try
		{
			ParallelWorkScheduler.Execute(workItems.Count, parallelism, i =>
			{
				var (archetype, chunkIndex) = workItems[i];
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
		}
	}

	public void ForEachRW<T1, T2>(RefAction<T1, T2> action)
		where T1 : struct
		where T2 : struct
	{
		if (action is null) throw new ArgumentNullException(nameof(action));

		foreach (var chunk in this)
		{
			var components1 = chunk.Components<T1>();
			var components2 = chunk.Components<T2>();
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
}
