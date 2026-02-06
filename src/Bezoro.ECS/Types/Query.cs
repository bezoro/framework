using Bezoro.ECS.Abstractions;
using Bezoro.ECS.Internal;
using Bezoro.ECS.Services;

namespace Bezoro.ECS.Types;

/// <summary>
/// Represents a cached archetype query.
/// </summary>
public sealed class Query
{
	public delegate void RefAction<T1>(ref T1 component1)
		where T1 : struct, IComponent;

	public delegate void RefAction<T1, T2>(ref T1 component1, ref T2 component2)
		where T1 : struct, IComponent
		where T2 : struct, IComponent;

	public delegate void RefInAction<T1, T2>(ref T1 component1, in T2 component2)
		where T1 : struct, IComponent
		where T2 : struct, IComponent;

	private readonly Archetype? _archetype;
	private readonly QuerySpec _spec;
	private readonly World _world;

	internal Query(World world, Archetype? archetype, QuerySpec spec)
	{
		_world = world;
		_archetype = archetype;
		_spec = spec;
	}

	public Query ForArchetype(Archetype archetype)
	{
		if (archetype is null) throw new ArgumentNullException(nameof(archetype));
		_world.EnsureOwnedArchetype(archetype);
		return new(_world, archetype, _spec);
	}

	public Query All<T>() where T : struct, IComponent
	{
		int typeId = ComponentTypeRegistry.GetOrCreate<T>();
		return WithSorted(typeId, _spec.AllTypeIds, ids => new QuerySpec(ids, _spec.NoneTypeIds, _spec.AnyTypeIds, _spec.OptionalTypeIds, _spec.ChangedTypeIds, _spec.RelatedRelationType, _spec.RelatedTarget));
	}

	public Query All(params Type[] componentTypes)
	{
		if (componentTypes is null) throw new ArgumentNullException(nameof(componentTypes));
		var result = this;
		for (var i = 0; i < componentTypes.Length; i++)
		{
			var type = componentTypes[i] ?? throw new ArgumentNullException(nameof(componentTypes));
			int typeId = ComponentTypeRegistry.GetOrCreate(type);
			result = result.WithSorted(typeId, result._spec.AllTypeIds, ids => new QuerySpec(ids, result._spec.NoneTypeIds, result._spec.AnyTypeIds, result._spec.OptionalTypeIds, result._spec.ChangedTypeIds, result._spec.RelatedRelationType, result._spec.RelatedTarget));
		}

		return result;
	}

	public Query None<T>() where T : struct, IComponent
	{
		int typeId = ComponentTypeRegistry.GetOrCreate<T>();
		return WithSorted(typeId, _spec.NoneTypeIds, ids => new QuerySpec(_spec.AllTypeIds, ids, _spec.AnyTypeIds, _spec.OptionalTypeIds, _spec.ChangedTypeIds, _spec.RelatedRelationType, _spec.RelatedTarget));
	}

	public Query None(params Type[] componentTypes)
	{
		if (componentTypes is null) throw new ArgumentNullException(nameof(componentTypes));
		var result = this;
		for (var i = 0; i < componentTypes.Length; i++)
		{
			var type = componentTypes[i] ?? throw new ArgumentNullException(nameof(componentTypes));
			int typeId = ComponentTypeRegistry.GetOrCreate(type);
			result = result.WithSorted(typeId, result._spec.NoneTypeIds, ids => new QuerySpec(result._spec.AllTypeIds, ids, result._spec.AnyTypeIds, result._spec.OptionalTypeIds, result._spec.ChangedTypeIds, result._spec.RelatedRelationType, result._spec.RelatedTarget));
		}

		return result;
	}

	public Query Any<T1, T2>()
		where T1 : struct, IComponent
		where T2 : struct, IComponent
	{
		int typeId1 = ComponentTypeRegistry.GetOrCreate<T1>();
		int typeId2 = ComponentTypeRegistry.GetOrCreate<T2>();
		var result = this;
		result = result.WithSorted(typeId1, result._spec.AnyTypeIds, ids => new QuerySpec(result._spec.AllTypeIds, result._spec.NoneTypeIds, ids, result._spec.OptionalTypeIds, result._spec.ChangedTypeIds, result._spec.RelatedRelationType, result._spec.RelatedTarget));
		result = result.WithSorted(typeId2, result._spec.AnyTypeIds, ids => new QuerySpec(result._spec.AllTypeIds, result._spec.NoneTypeIds, ids, result._spec.OptionalTypeIds, result._spec.ChangedTypeIds, result._spec.RelatedRelationType, result._spec.RelatedTarget));
		return result;
	}

	public Query Optional<T>() where T : struct, IComponent
	{
		int typeId = ComponentTypeRegistry.GetOrCreate<T>();
		return WithSorted(typeId, _spec.OptionalTypeIds, ids => new QuerySpec(_spec.AllTypeIds, _spec.NoneTypeIds, _spec.AnyTypeIds, ids, _spec.ChangedTypeIds, _spec.RelatedRelationType, _spec.RelatedTarget));
	}

	public Query Changed<T>() where T : struct, IComponent
	{
		int typeId = ComponentTypeRegistry.GetOrCreate<T>();
		return WithSorted(typeId, _spec.ChangedTypeIds, ids => new QuerySpec(_spec.AllTypeIds, _spec.NoneTypeIds, _spec.AnyTypeIds, _spec.OptionalTypeIds, ids, _spec.RelatedRelationType, _spec.RelatedTarget));
	}

	public Query Related<TRelation>(Entity target)
	{
		if (target == Entity.Wildcard)
			return new(_world, _archetype, new QuerySpec(_spec.AllTypeIds, _spec.NoneTypeIds, _spec.AnyTypeIds, _spec.OptionalTypeIds, _spec.ChangedTypeIds, typeof(TRelation), Entity.Wildcard));

		int relationTypeId = ComponentTypeRegistry.GetOrCreateRelationship(typeof(TRelation), target);
		var withTarget = WithSorted(relationTypeId, _spec.AllTypeIds, ids => new QuerySpec(ids, _spec.NoneTypeIds, _spec.AnyTypeIds, _spec.OptionalTypeIds, _spec.ChangedTypeIds, typeof(TRelation), target));
		return withTarget;
	}

	public Query With<T>() where T : struct, IComponent => All<T>();

	public Query With(params Type[] componentTypes) => All(componentTypes);

	public Query Without<T>() where T : struct, IComponent => None<T>();

	public Query Without(params Type[] componentTypes) => None(componentTypes);

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
		where T1 : struct, IComponent
	{
		if (action is null) throw new ArgumentNullException(nameof(action));

		foreach (var chunk in this)
		{
			var components1 = chunk.Components<T1>();
			for (var i = 0; i < chunk.Count; i++)
				action(ref components1[i]);
		}
	}

	public void ForEachRW<T1, T2>(RefAction<T1, T2> action)
		where T1 : struct, IComponent
		where T2 : struct, IComponent
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

	public void ForEach<T1, T2>(RefInAction<T1, T2> action)
		where T1 : struct, IComponent
		where T2 : struct, IComponent
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

	public void ForEach<TJob, T1, T2>(TJob job)
		where TJob : struct, IForEach<T1, T2>
		where T1 : struct, IComponent
		where T2 : struct, IComponent
	{
		foreach (var chunk in this)
		{
			var components1 = chunk.Components<T1>();
			var components2 = chunk.ReadOnlyComponents<T2>();
			for (var i = 0; i < chunk.Count; i++)
				job.Execute(ref components1[i], in components2[i]);
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

		var views = new List<ChunkView>();
		foreach (var chunk in this)
			views.Add(chunk);

		if (views.Count == 0)
			return;

		// Work-stealing loop: each worker atomically claims the next chunk index.
		int nextChunk = -1;
		var options = new ParallelOptions { MaxDegreeOfParallelism = parallelism };
		Parallel.For(0, parallelism, options, _ =>
		{
			while (true)
			{
				int chunkIndex = Interlocked.Increment(ref nextChunk);
				if (chunkIndex >= views.Count)
					break;

				action(views[chunkIndex]);
			}
		});
	}

	private Query WithSorted(int typeId, int[] source, Func<int[], QuerySpec> specFactory)
	{
		for (var i = 0; i < source.Length; i++)
		{
			if (source[i] == typeId)
				return this;
		}

		var updated = new int[source.Length + 1];
		var index = 0;
		var added = false;

		for (var i = 0; i < source.Length; i++)
		{
			int current = source[i];
			if (!added && typeId < current)
			{
				updated[index++] = typeId;
				added = true;
			}

			updated[index++] = current;
		}

		if (!added)
			updated[index] = typeId;

		return new(_world, _archetype, specFactory(updated));
	}
}
