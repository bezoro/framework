using System.Runtime.CompilerServices;
using Bezoro.ECS.Abstractions;
using Bezoro.ECS.Services;

namespace Bezoro.ECS.Types;

/// <summary>
///     Ergonomic query wrapper that hides the single-batch cursor ceremony.
/// </summary>
/// <typeparam name="TQuery">Compiled query specification type.</typeparam>
public readonly struct QueryView<TQuery>(World world, QueryHandle<TQuery> handle)
	where TQuery : struct, ICompiledQuerySpec
{
	/// <summary>
	///     Delegate invoked for each matching entity.
	/// </summary>
	public delegate void EntityAction(Entity entity);

	/// <summary>
	///     Delegate invoked for each matching entity with one read-only component.
	/// </summary>
	public delegate void EntityInAction<T1>(Entity entity, in T1 component1)
		where T1 : struct;

	/// <summary>
	///     Delegate invoked for each matching entity with one mutable unmanaged component.
	/// </summary>
	public delegate void EntityRefAction<T1>(Entity entity, ref T1 component1)
		where T1 : struct;

	/// <summary>
	///     Delegate invoked for each matching entity with one mutable and one read-only unmanaged component.
	/// </summary>
	public delegate void EntityRefInAction<T1, T2>(Entity entity, ref T1 component1, in T2 component2)
		where T1 : struct
		where T2 : struct;

	/// <summary>
	///     Delegate invoked for each matching entity with one mutable and two read-only unmanaged components.
	/// </summary>
	public delegate void EntityRefInAction<T1, T2, T3>(Entity entity, ref T1 component1, in T2 component2, in T3 component3)
		where T1 : struct
		where T2 : struct
		where T3 : struct;

	/// <summary>
	///     Delegate invoked for each matching entity with one mutable and three read-only unmanaged components.
	/// </summary>
	public delegate void EntityRefInAction<T1, T2, T3, T4>(
		Entity entity,
		ref T1 component1,
		in T2 component2,
		in T3 component3,
		in T4 component4)
		where T1 : struct
		where T2 : struct
		where T3 : struct
		where T4 : struct;

	private readonly QueryHandle<TQuery> _handle = handle;
	private readonly World               _world  = world ?? throw new ArgumentNullException(nameof(world));

	private readonly struct ReadOnlyEntityAction<T1>(EntityInAction<T1> action) : QueryCursor.IEntityAction<T1>
		where T1 : struct
	{
		public void Invoke(Entity entity, ref T1 component1) => action(entity, in component1);
	}

	private readonly struct EntityAction<T1>(EntityRefAction<T1> action) : QueryCursor.IEntityAction<T1>
		where T1 : struct
	{
		public void Invoke(Entity entity, ref T1 component1) => action(entity, ref component1);
	}

	private readonly struct EntityAction<T1, T2>(EntityRefInAction<T1, T2> action) : QueryCursor.IEntityAction<T1, T2>
		where T1 : struct
		where T2 : struct
	{
		public void Invoke(Entity entity, ref T1 component1, in T2 component2) => action(entity, ref component1, in component2);
	}

	private readonly struct EntityAction<T1, T2, T3>(EntityRefInAction<T1, T2, T3> action) : QueryCursor.IEntityAction<T1, T2, T3>
		where T1 : struct
		where T2 : struct
		where T3 : struct
	{
		public void Invoke(Entity entity, ref T1 component1, in T2 component2, in T3 component3) => action(entity, ref component1, in component2, in component3);
	}

	private readonly struct EntityAction<T1, T2, T3, T4>(EntityRefInAction<T1, T2, T3, T4> action) : QueryCursor.IEntityAction<T1, T2, T3, T4>
		where T1 : struct
		where T2 : struct
		where T3 : struct
		where T4 : struct
	{
		public void Invoke(Entity entity, ref T1 component1, in T2 component2, in T3 component3, in T4 component4) => action(entity, ref component1, in component2, in component3, in component4);
	}

	/// <summary>
	///     Returns <c>true</c> when any entity matches the query.
	/// </summary>
	public bool Any()
	{
		using var cursor = _world.Execute(_handle);
		return cursor.MoveNext() && cursor.Current.Length > 0;
	}

	/// <summary>
	///     Counts matching entities.
	/// </summary>
	public int Count()
	{
		using var cursor = _world.Execute(_handle);
		return cursor.MoveNext() ? cursor.Current.Length : 0;
	}

	/// <summary>
	///     Executes an entity-only loop over the current query results.
	/// </summary>
	public void ForEach(EntityAction action)
	{
		if (action is null) throw new ArgumentNullException(nameof(action));

		using var cursor = _world.Execute(_handle);
		if (!cursor.MoveNext())
			return;

		var entities = cursor.Current;
		for (var i = 0; i < entities.Length; i++)
			action(entities[i]);
	}

	/// <summary>
	///     Executes an entity-aware read-only loop over one component.
	/// </summary>
	public void ForEachRead<T1>(EntityInAction<T1> action)
		where T1 : struct
	{
		if (action is null) throw new ArgumentNullException(nameof(action));

		using var cursor = _world.Execute(_handle);
		if (!cursor.MoveNext())
			return;

		if (!RuntimeHelpers.IsReferenceOrContainsReferences<T1>())
		{
			cursor.ExecuteEntityAction<ReadOnlyEntityAction<T1>, T1>(new(action));
			return;
		}

		var entities = cursor.Current;
		for (var i = 0; i < entities.Length; i++)
		{
			ref readonly var component1 = ref _world.Read<T1>(entities[i]);
			action(entities[i], in component1);
		}
	}

	/// <summary>
	///     Executes an entity-aware loop over one mutable unmanaged component.
	/// </summary>
	public void ForEach<T1>(EntityRefAction<T1> action)
		where T1 : struct
	{
		if (action is null) throw new ArgumentNullException(nameof(action));

		using var cursor = _world.Execute(_handle);
		if (!cursor.MoveNext())
			return;

		if (!RuntimeHelpers.IsReferenceOrContainsReferences<T1>())
		{
			cursor.ExecuteEntityAction<EntityAction<T1>, T1>(new(action));
			return;
		}

		var entities = cursor.Current;
		for (var i = 0; i < entities.Length; i++)
		{
			ref var component1 = ref _world.Write<T1>(entities[i]);
			action(entities[i], ref component1);
		}
	}

	/// <summary>
	///     Executes an entity-aware loop over one mutable and one read-only unmanaged component.
	/// </summary>
	public void ForEach<T1, T2>(EntityRefInAction<T1, T2> action)
		where T1 : struct
		where T2 : struct
	{
		if (action is null) throw new ArgumentNullException(nameof(action));

		using var cursor = _world.Execute(_handle);
		if (!cursor.MoveNext())
			return;

		if (!RuntimeHelpers.IsReferenceOrContainsReferences<T1>() &&
			!RuntimeHelpers.IsReferenceOrContainsReferences<T2>())
		{
			cursor.ExecuteEntityAction<EntityAction<T1, T2>, T1, T2>(new(action));
			return;
		}

		var entities = cursor.Current;
		for (var i = 0; i < entities.Length; i++)
		{
			ref var component1 = ref _world.Write<T1>(entities[i]);
			ref readonly var component2 = ref _world.Read<T2>(entities[i]);
			action(entities[i], ref component1, in component2);
		}
	}

	/// <summary>
	///     Executes an entity-aware loop over one mutable and two read-only unmanaged components.
	/// </summary>
	public void ForEach<T1, T2, T3>(EntityRefInAction<T1, T2, T3> action)
		where T1 : struct
		where T2 : struct
		where T3 : struct
	{
		if (action is null) throw new ArgumentNullException(nameof(action));

		using var cursor = _world.Execute(_handle);
		if (!cursor.MoveNext())
			return;

		if (!RuntimeHelpers.IsReferenceOrContainsReferences<T1>() &&
			!RuntimeHelpers.IsReferenceOrContainsReferences<T2>() &&
			!RuntimeHelpers.IsReferenceOrContainsReferences<T3>())
		{
			cursor.ExecuteEntityAction<EntityAction<T1, T2, T3>, T1, T2, T3>(new(action));
			return;
		}

		var entities = cursor.Current;
		for (var i = 0; i < entities.Length; i++)
		{
			ref var component1 = ref _world.Write<T1>(entities[i]);
			ref readonly var component2 = ref _world.Read<T2>(entities[i]);
			ref readonly var component3 = ref _world.Read<T3>(entities[i]);
			action(entities[i], ref component1, in component2, in component3);
		}
	}

	/// <summary>
	///     Executes an entity-aware loop over one mutable and three read-only unmanaged components.
	/// </summary>
	public void ForEach<T1, T2, T3, T4>(EntityRefInAction<T1, T2, T3, T4> action)
		where T1 : struct
		where T2 : struct
		where T3 : struct
		where T4 : struct
	{
		if (action is null) throw new ArgumentNullException(nameof(action));

		using var cursor = _world.Execute(_handle);
		if (!cursor.MoveNext())
			return;

		if (!RuntimeHelpers.IsReferenceOrContainsReferences<T1>() &&
			!RuntimeHelpers.IsReferenceOrContainsReferences<T2>() &&
			!RuntimeHelpers.IsReferenceOrContainsReferences<T3>() &&
			!RuntimeHelpers.IsReferenceOrContainsReferences<T4>())
		{
			cursor.ExecuteEntityAction<EntityAction<T1, T2, T3, T4>, T1, T2, T3, T4>(new(action));
			return;
		}

		var entities = cursor.Current;
		for (var i = 0; i < entities.Length; i++)
		{
			ref var component1 = ref _world.Write<T1>(entities[i]);
			ref readonly var component2 = ref _world.Read<T2>(entities[i]);
			ref readonly var component3 = ref _world.Read<T3>(entities[i]);
			ref readonly var component4 = ref _world.Read<T4>(entities[i]);
			action(entities[i], ref component1, in component2, in component3, in component4);
		}
	}

	/// <summary>
	///     Executes a struct job over the matching entities.
	/// </summary>
	public void Run<TJob, T1>(TJob job)
		where TJob : struct, IForEach<T1>
		where T1 : unmanaged =>
		_world.Run<TQuery, TJob, T1>(_handle, job);

	/// <summary>
	///     Executes an entity-aware struct job over the matching entities.
	/// </summary>
	public void RunEntity<TJob, T1>(TJob job)
		where TJob : struct, IForEachEntity<T1>
		where T1 : unmanaged =>
		_world.RunEntity<TQuery, TJob, T1>(_handle, job);

	/// <summary>
	///     Executes a struct job over the matching entities.
	/// </summary>
	public void Run<TJob, T1, T2>(TJob job)
		where TJob : struct, IForEach<T1, T2>
		where T1 : unmanaged
		where T2 : unmanaged =>
		_world.Run<TQuery, TJob, T1, T2>(_handle, job);

	/// <summary>
	///     Executes an entity-aware struct job over the matching entities.
	/// </summary>
	public void RunEntity<TJob, T1, T2>(TJob job)
		where TJob : struct, IForEachEntity<T1, T2>
		where T1 : unmanaged
		where T2 : unmanaged =>
		_world.RunEntity<TQuery, TJob, T1, T2>(_handle, job);

	/// <summary>
	///     Executes a struct job over the matching entities.
	/// </summary>
	public void Run<TJob, T1, T2, T3>(TJob job)
		where TJob : struct, IForEach<T1, T2, T3>
		where T1 : unmanaged
		where T2 : unmanaged
		where T3 : unmanaged =>
		_world.Run<TQuery, TJob, T1, T2, T3>(_handle, job);

	/// <summary>
	///     Executes an entity-aware struct job over the matching entities.
	/// </summary>
	public void RunEntity<TJob, T1, T2, T3>(TJob job)
		where TJob : struct, IForEachEntity<T1, T2, T3>
		where T1 : unmanaged
		where T2 : unmanaged
		where T3 : unmanaged =>
		_world.RunEntity<TQuery, TJob, T1, T2, T3>(_handle, job);

	/// <summary>
	///     Executes a struct job over the matching entities.
	/// </summary>
	public void Run<TJob, T1, T2, T3, T4>(TJob job)
		where TJob : struct, IForEach<T1, T2, T3, T4>
		where T1 : unmanaged
		where T2 : unmanaged
		where T3 : unmanaged
		where T4 : unmanaged =>
		_world.Run<TQuery, TJob, T1, T2, T3, T4>(_handle, job);

	/// <summary>
	///     Executes an entity-aware struct job over the matching entities.
	/// </summary>
	public void RunEntity<TJob, T1, T2, T3, T4>(TJob job)
		where TJob : struct, IForEachEntity<T1, T2, T3, T4>
		where T1 : unmanaged
		where T2 : unmanaged
		where T3 : unmanaged
		where T4 : unmanaged =>
		_world.RunEntity<TQuery, TJob, T1, T2, T3, T4>(_handle, job);

	/// <summary>
	///     Executes a struct job in parallel over the matching entities.
	/// </summary>
	public void RunParallel<TJob, T1>(TJob job, int? degreeOfParallelism = null)
		where TJob : struct, IForEach<T1>
		where T1 : unmanaged =>
		_world.RunParallel<TQuery, TJob, T1>(_handle, job, degreeOfParallelism);

	/// <summary>
	///     Executes an entity-aware struct job in parallel over the matching entities.
	/// </summary>
	public void RunParallelEntity<TJob, T1>(TJob job, int? degreeOfParallelism = null)
		where TJob : struct, IForEachEntity<T1>
		where T1 : unmanaged =>
		_world.RunParallelEntity<TQuery, TJob, T1>(_handle, job, degreeOfParallelism);

	/// <summary>
	///     Executes a struct job in parallel over the matching entities.
	/// </summary>
	public void RunParallel<TJob, T1, T2>(TJob job, int? degreeOfParallelism = null)
		where TJob : struct, IForEach<T1, T2>
		where T1 : unmanaged
		where T2 : unmanaged =>
		_world.RunParallel<TQuery, TJob, T1, T2>(_handle, job, degreeOfParallelism);

	/// <summary>
	///     Executes an entity-aware struct job in parallel over the matching entities.
	/// </summary>
	public void RunParallelEntity<TJob, T1, T2>(TJob job, int? degreeOfParallelism = null)
		where TJob : struct, IForEachEntity<T1, T2>
		where T1 : unmanaged
		where T2 : unmanaged =>
		_world.RunParallelEntity<TQuery, TJob, T1, T2>(_handle, job, degreeOfParallelism);

	/// <summary>
	///     Executes a struct job in parallel over the matching entities.
	/// </summary>
	public void RunParallel<TJob, T1, T2, T3>(TJob job, int? degreeOfParallelism = null)
		where TJob : struct, IForEach<T1, T2, T3>
		where T1 : unmanaged
		where T2 : unmanaged
		where T3 : unmanaged =>
		_world.RunParallel<TQuery, TJob, T1, T2, T3>(_handle, job, degreeOfParallelism);

	/// <summary>
	///     Executes an entity-aware struct job in parallel over the matching entities.
	/// </summary>
	public void RunParallelEntity<TJob, T1, T2, T3>(TJob job, int? degreeOfParallelism = null)
		where TJob : struct, IForEachEntity<T1, T2, T3>
		where T1 : unmanaged
		where T2 : unmanaged
		where T3 : unmanaged =>
		_world.RunParallelEntity<TQuery, TJob, T1, T2, T3>(_handle, job, degreeOfParallelism);

	/// <summary>
	///     Executes a struct job in parallel over the matching entities.
	/// </summary>
	public void RunParallel<TJob, T1, T2, T3, T4>(TJob job, int? degreeOfParallelism = null)
		where TJob : struct, IForEach<T1, T2, T3, T4>
		where T1 : unmanaged
		where T2 : unmanaged
		where T3 : unmanaged
		where T4 : unmanaged =>
		_world.RunParallel<TQuery, TJob, T1, T2, T3, T4>(_handle, job, degreeOfParallelism);

	/// <summary>
	///     Executes an entity-aware struct job in parallel over the matching entities.
	/// </summary>
	public void RunParallelEntity<TJob, T1, T2, T3, T4>(TJob job, int? degreeOfParallelism = null)
		where TJob : struct, IForEachEntity<T1, T2, T3, T4>
		where T1 : unmanaged
		where T2 : unmanaged
		where T3 : unmanaged
		where T4 : unmanaged =>
		_world.RunParallelEntity<TQuery, TJob, T1, T2, T3, T4>(_handle, job, degreeOfParallelism);
}
