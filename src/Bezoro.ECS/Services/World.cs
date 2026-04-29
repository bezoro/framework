using System.Reflection;
using System.Runtime.CompilerServices;
using Bezoro.ECS.Abstractions;
using Bezoro.ECS.Internal;
using Bezoro.ECS.Internal.Fixed;
using Bezoro.ECS.Options;
using Bezoro.ECS.Types;
using EntityLocation = Bezoro.ECS.Internal.Fixed.EntityLocation;
using QueryExecutionLease = Bezoro.ECS.Internal.QueryExecutionLease;

namespace Bezoro.ECS.Services;

// TODO: [CODE SMELL - God Class] This type owns public API surface, query orchestration, change tracking, snapshots, systems, and hot-path helpers. Fix: split query/direct-iteration concerns into dedicated collaborators and reduce World to orchestration.
public class World : IWorld, IDisposable
{
	private static readonly Dictionary<Type, bool> ContainsReferencesByType = [];
	private static readonly MethodInfo ContainsReferencesMethod = typeof(World).GetMethod(
																	  nameof(ContainsReferencesGeneric),
																	  BindingFlags.NonPublic | BindingFlags.Static
																  ) ??
																  throw new InvalidOperationException(
																	  "Missing contains-references helper."
																  );
	private static readonly object             ContainsReferencesSync = new();
	private static          int                _nextInstanceId;
	private readonly        int                _instanceId;
	private readonly        WorldChangeTracker _changeTracker;

	private readonly WorldConfig                 _config;
	private readonly WorldDirectIterationService _directIterationService;
	private readonly WorldEntityStore            _entityStore;
	private readonly WorldLifecycleService       _lifecycleService;
	private readonly WorldQueryEngine            _queryEngine;
	private readonly WorldQueryRuntime           _queryRuntime;
	private readonly WorldResourceFacade        _resources;
	private readonly WorldRelationIndex          _relationIndex = new();
	private readonly WorldResourceStore          _resourceStore = new();
	private readonly WorldSnapshotCoordinator    _snapshotCoordinator;
	private readonly WorldSnapshotService        _snapshotService;
	private readonly WorldSystemRuntime          _systemRuntime;

	private bool _disposed;

	public World() : this(new WorldConfig()) { }

	public World(WorldOptions options) : this(ToWorldConfig(options)) { }

	public World(WorldConfig config)
	{
		_instanceId = Interlocked.Increment(ref _nextInstanceId);
		_config     = config ?? throw new ArgumentNullException(nameof(config));
		_config.Validate();
		_systemRuntime          = new(_config.MaxDegreeOfParallelism);
		_entityStore            = new(this, _config, _relationIndex);
		_changeTracker          = new(_entityStore, _config);
		_queryRuntime           = new(_entityStore, _relationIndex, _changeTracker);
		_resources              = new(_resourceStore);
		_directIterationService = new(this);
		_queryEngine            = new(this, _config);
		_snapshotService = new(
			this,
			_config,
			_entityStore,
			_resourceStore,
			_relationIndex,
			_entityStore.TypeById,
			_entityStore.TypeToId
		);

		_lifecycleService    = new(_changeTracker, _entityStore, _queryEngine, _resourceStore, _systemRuntime);
		_snapshotCoordinator = new(_entityStore, _lifecycleService, _queryEngine, _snapshotService);
	}

	public int EntityCount
	{
		get
		{
			ThrowIfDisposed();
			return AliveCount;
		}
	}

	internal int ArchetypeVersionForQueryEngine => ArchetypeVersion;

	internal int EntityCapacity                        => _config.EntityCapacity;
	internal int QueryResultCapacityForDirectIteration => _config.QueryResultCapacity;
	internal int SchedulerPlanBuildCount               => _systemRuntime.PlanBuildCount;

	private bool[] AliveByEntityId => _entityStore.AliveByEntityId;

	private EntityLocation[] LocationByEntityId => _entityStore.LocationByEntityId;

	private int[] VersionByEntityId => _entityStore.VersionByEntityId;

	private List<ArchetypeStorage> Archetypes => _entityStore.Archetypes;

	private int AliveCount
	{
		get => _entityStore.AliveCount;
		set => _entityStore.AliveCount = value;
	}

	private int ArchetypeVersion
	{
		get => _entityStore.ArchetypeVersion;
		set => _entityStore.ArchetypeVersion = value;
	}

	private int ComponentTypeOverflowCount
	{
		get => _entityStore.ComponentTypeOverflowCount;
		set => _entityStore.ComponentTypeOverflowCount = value;
	}

	private int EntityHighWatermark
	{
		get => _entityStore.EntityHighWatermark;
		set => _entityStore.EntityHighWatermark = value;
	}

	private int EntityOverflowCount
	{
		get => _entityStore.EntityOverflowCount;
		set => _entityStore.EntityOverflowCount = value;
	}

	private int RegisteredTypeHighWatermark
	{
		get => _entityStore.RegisteredTypeHighWatermark;
		set => _entityStore.RegisteredTypeHighWatermark = value;
	}

	private int TypeCount
	{
		get => _entityStore.TypeCount;
		set => _entityStore.TypeCount = value;
	}

	public bool Has<T>(Entity entity) where T : struct
	{
		ThrowIfDisposed();
		if (!IsAliveUnchecked(entity))
			return false;

		int typeId   = GetOrCreateComponentTypeId<T>();
		var location = LocationByEntityId[entity.Id];
		return location.IsValid && Archetypes[location.ArchetypeId].HasType(typeId);
	}

	public bool HasRelation<TRelation>(Entity source, Entity target)
		where TRelation : struct
	{
		ThrowIfDisposed();
		if (!IsAliveUnchecked(source))
			return false;

		if (target == Entity.None)
			return false;

		var sourceLocation = LocationByEntityId[source.Id];
		if (!sourceLocation.IsValid)
			return false;

		var sourceArchetype = Archetypes[sourceLocation.ArchetypeId];
		if (target == Entity.Wildcard)
		{
			int[] relationTypeIds = _relationIndex.GetRelationTypeIds(typeof(TRelation));
			for (var i = 0; i < relationTypeIds.Length; i++)
			{
				if (sourceArchetype.HasType(relationTypeIds[i]))
					return true;
			}

			return false;
		}

		if (!IsAliveUnchecked(target))
			return false;

		return _relationIndex.TryGetRelationTypeId(typeof(TRelation), target, out int relationTypeId) &&
			   sourceArchetype.HasType(relationTypeId);
	}

	public bool HasResource<T>() where T : notnull
	{
		ThrowIfDisposed();
		return _resources.Has<T>();
	}

	public bool IsAlive(Entity entity)
	{
		ThrowIfDisposed();
		return IsAliveUnchecked(entity);
	}

	public bool IsSystemSetEnabled<TSet>()
	{
		ThrowIfDisposed();
		return _systemRuntime.IsSystemSetEnabled(typeof(TSet));
	}

	public bool RemoveRelation<TRelation>(Entity source, Entity target)
		where TRelation : struct
	{
		ThrowIfDisposed();
		if (!IsAliveUnchecked(source))
			return false;

		var sourceLocation = LocationByEntityId[source.Id];
		if (!sourceLocation.IsValid)
			return false;

		var sourceArchetype = Archetypes[sourceLocation.ArchetypeId];
		if (target == Entity.Wildcard)
		{
			var   removedAny      = false;
			int[] relationTypeIds = _relationIndex.GetRelationTypeIds(typeof(TRelation));
			for (var i = 0; i < relationTypeIds.Length; i++)
			{
				int relationTypeId = relationTypeIds[i];
				sourceLocation = LocationByEntityId[source.Id];
				if (!sourceLocation.IsValid)
					return removedAny;

				sourceArchetype = Archetypes[sourceLocation.ArchetypeId];
				if (!sourceArchetype.HasType(relationTypeId))
					continue;

				RemoveComponentFromCommand(source, relationTypeId);
				removedAny = true;
			}

			return removedAny;
		}

		if (target == Entity.None)
			return false;

		if (!_relationIndex.TryGetRelationTypeId(typeof(TRelation), target, out int relationId))
			return false;

		if (!sourceArchetype.HasType(relationId))
			return false;

		RemoveComponentFromCommand(source, relationId);
		return true;
	}

	public bool RemoveResource<T>() where T : notnull
	{
		ThrowIfDisposed();
		return _resources.Remove<T>();
	}

	public bool TryGet<T>(Entity entity, out T component) where T : struct
	{
		ThrowIfDisposed();
		component = default;
		if (!IsAliveUnchecked(entity))
			return false;

		int typeId = GetOrCreateComponentTypeId<T>();
		return _entityStore.TryGetComponentUnchecked(entity.Id, typeId, out component);
	}

	public bool TryGetManaged<T>(Entity entity, out T component) where T : struct
	{
		ThrowIfDisposed();
		component = default;
		if (!IsAliveUnchecked(entity))
			return false;

		int typeId = GetOrCreateComponentTypeId<T>();
		return _entityStore.TryGetComponentUnchecked(entity.Id, typeId, out component);
	}

	public bool TryRead<T>(Entity entity, out T component) where T : struct =>
		TryGet(entity, out component);

	public bool TryReadResource<T>(out T resource) where T : notnull
	{
		ThrowIfDisposed();
		return _resources.TryRead(out resource);
	}

	public bool TryWrite<T>(Entity entity, out ComponentRef<T> component) where T : struct
	{
		ThrowIfDisposed();
		component = default;
		if (!IsAliveUnchecked(entity))
			return false;

		int typeId   = GetOrCreateComponentTypeId<T>();
		var location = LocationByEntityId[entity.Id];
		if (!location.IsValid)
			return false;

		var archetype   = Archetypes[location.ArchetypeId];
		int columnIndex = archetype.GetColumnIndexOrNegative(typeId);
		if (columnIndex < 0)
			return false;

		ref var value = ref _entityStore.GetComponentRefUnchecked<T>(entity.Id, typeId);
		TrackPotentialSingleRefWrite(entity.Id, typeId);
		component = new(ref value);
		return true;
	}

	public CommandBuffer CreateCommandBuffer() =>
		new(CreateCommandStream());

	public CommandStream BeginCommands() => CreateCommandStream();

	public CommandStream CreateCommandStream()
	{
		ThrowIfDisposed();
		return new(
			this,
			_config.CommandCapacity,
			_config.ComponentTypeCapacity,
			_config.CommandPayloadCapacityPerType,
			_config.OverflowPolicy
		);
	}

	public ComponentAccessor<T> GetAccessor<T>() where T : unmanaged
	{
		ThrowIfDisposed();
		int typeId = GetOrCreateComponentTypeId<T>();
		return new(this, typeId);
	}

	public Entity Spawn()
	{
		ThrowIfDisposed();
		return CreateEntityInternal();
	}

	public Entity Spawn<T1>(in T1 component1) where T1 : struct
	{
		ThrowIfDisposed();
		var entity = CreateEntityInternal();
		ApplySetFromCommand(entity, in component1);
		return entity;
	}

	public Entity Spawn<T1, T2>(in T1 component1, in T2 component2)
		where T1 : struct
		where T2 : struct
	{
		ThrowIfDisposed();
		var entity = CreateEntityInternal();
		ApplySetFromCommand(entity, in component1);
		ApplySetFromCommand(entity, in component2);
		return entity;
	}

	public Entity Spawn<T1, T2, T3>(in T1 component1, in T2 component2, in T3 component3)
		where T1 : struct
		where T2 : struct
		where T3 : struct
	{
		ThrowIfDisposed();
		var entity = CreateEntityInternal();
		ApplySetFromCommand(entity, in component1);
		ApplySetFromCommand(entity, in component2);
		ApplySetFromCommand(entity, in component3);
		return entity;
	}

	public Entity Spawn<T1, T2, T3, T4>(in T1 component1, in T2 component2, in T3 component3, in T4 component4)
		where T1 : struct
		where T2 : struct
		where T3 : struct
		where T4 : struct
	{
		ThrowIfDisposed();
		var entity = CreateEntityInternal();
		ApplySetFromCommand(entity, in component1);
		ApplySetFromCommand(entity, in component2);
		ApplySetFromCommand(entity, in component3);
		ApplySetFromCommand(entity, in component4);
		return entity;
	}

	/// <summary>
	///     Executes a compiled query and returns a cursor over all matching entities.
	/// </summary>
	/// <typeparam name="TSpec">Query specification type.</typeparam>
	/// <param name="handle">Compiled query handle produced by <see cref="Compile{TSpec}" />.</param>
	/// <returns>A <see cref="QueryCursor" /> that must be disposed after use.</returns>
	/// <exception cref="InvalidOperationException">
	///     Thrown when <paramref name="handle" /> belongs to a different world.
	///     Structural world operations remain disallowed while any query cursor is active.
	/// </exception>
	public QueryCursor Execute<TSpec>(QueryHandle<TSpec> handle) where TSpec : struct, ICompiledQuerySpec
	{
		ThrowIfDisposed();
		return _queryEngine.Execute(handle);
	}

	/// <summary>
	///     Returns diagnostics for one compiled query handle.
	/// </summary>
	/// <typeparam name="TSpec">Query specification type.</typeparam>
	/// <param name="handle">Compiled query handle.</param>
	/// <returns>Query diagnostics snapshot.</returns>
	public QueryDiagnostics GetQueryDiagnostics<TSpec>(QueryHandle<TSpec> handle)
		where TSpec : struct, ICompiledQuerySpec
	{
		ThrowIfDisposed();
		ThrowIfDirectIterationUnavailable(handle);
		return _queryEngine.GetDiagnostics(handle.Plan);
	}

	public QueryHandle<TSpec> Compile<TSpec>() where TSpec : struct, ICompiledQuerySpec
	{
		ThrowIfDisposed();
		return _queryEngine.Compile<TSpec>();
	}

	public QueryView<TSpec> Query<TSpec>() where TSpec : struct, ICompiledQuerySpec =>
		new(this, Compile<TSpec>());

	public ScheduleDiagnostics GetScheduleDiagnostics()
	{
		ThrowIfDisposed();
		return _systemRuntime.GetDiagnostics();
	}

	public ref T Get<T>(Entity entity) where T : struct
	{
		ThrowIfDisposed();
		EnsureAlive(entity);
		int     typeId    = GetOrCreateComponentTypeId<T>();
		ref var component = ref _entityStore.GetComponentRefUnchecked<T>(entity.Id, typeId);
		TrackPotentialSingleRefWrite(entity.Id, typeId);
		return ref component;
	}

	public ref T GetOrCreateResource<T>() where T : notnull, new()
	{
		ThrowIfDisposed();
		return ref _resources.GetOrCreate<T>();
	}

	public ref T GetOrCreateResource<T>(Func<T> factory) where T : notnull
	{
		ThrowIfDisposed();
		return ref _resources.GetOrCreate(factory);
	}

	public ref T GetResource<T>() where T : notnull
	{
		ThrowIfDisposed();
		return ref _resources.Get<T>();
	}

	public ref readonly T Read<T>(Entity entity) where T : struct
	{
		ThrowIfDisposed();
		EnsureAlive(entity);
		int typeId = GetOrCreateComponentTypeId<T>();
		return ref _entityStore.GetComponentRefUnchecked<T>(entity.Id, typeId);
	}

	public ref readonly T ReadResource<T>() where T : notnull =>
		ref GetResource<T>();

	public ref T Write<T>(Entity entity) where T : struct =>
		ref Get<T>(entity);

	public ref T WriteResource<T>() where T : notnull =>
		ref GetResource<T>();

	public void Add<T>(Entity entity) where T : struct
	{
		ThrowIfDisposed();
		var component = default(T);
		ApplySetFromCommand(entity, in component);
	}

	public void Add<T>(Entity entity, in T component) where T : struct
	{
		ThrowIfDisposed();
		ApplySetFromCommand(entity, in component);
	}

	public void AddRelation<TRelation>(Entity source, Entity target)
		where TRelation : struct
	{
		ThrowIfDisposed();
		EnsureAlive(source);
		if (target == Entity.Wildcard)
			throw new ArgumentException("Cannot add a relation with Entity.Wildcard as the target.", nameof(target));

		EnsureAlive(target);
		int relationTypeId = _relationIndex.GetOrCreateRelationTypeId(
			typeof(TRelation),
			target,
			_entityStore.CreateRelationTypeId
		);

		_entityStore.SetRelationComponent(source, relationTypeId);
	}

	public void AddSystem(ISystem system, Stage stage = Stage.Tick)
	{
		ThrowIfDisposed();
		_systemRuntime.AddSystem(this, system, stage);
	}

	public void AddSystem<TSystem>(Stage stage = Stage.Tick)
		where TSystem : ISystem, new() =>
		AddSystem(new TSystem(), stage);

	/// <summary>
	///     Captures a snapshot payload and forwards it to <paramref name="writer" />.
	/// </summary>
	/// <typeparam name="TSnapshotWriter">Writer type receiving the captured payload.</typeparam>
	/// <param name="writer">Snapshot writer receiving the captured payload.</param>
	public void CaptureSnapshot<TSnapshotWriter>(ref TSnapshotWriter writer)
		where TSnapshotWriter : struct, IWorldSnapshotWriter
	{
		ThrowIfDisposed();
		_snapshotCoordinator.Capture(ref writer);
	}

	public void Clear()
	{
		ThrowIfDisposed();
		_lifecycleService.Clear();
	}

	public void ClearSystemSetRunCondition<TSet>()
	{
		ThrowIfDisposed();
		_systemRuntime.ClearSystemSetRunCondition(typeof(TSet));
	}

	public void Despawn(Entity entity)
	{
		ThrowIfDisposed();
		DestroyEntityInternal(entity);
	}

	public void Dispose()
	{
		if (_disposed)
			return;

		_lifecycleService.Dispose(this);
		_disposed = true;
	}

	public void FixedTick(float deltaTime) => RunPhase(SystemLoopPhase.FixedTick, deltaTime);

	public void ForEach<TSpec, T1>(QueryHandle<TSpec> handle, QueryCursor.RefAction<T1> action)
		where TSpec : struct, ICompiledQuerySpec
		where T1 : unmanaged
	{
		if (action is null) throw new ArgumentNullException(nameof(action));

		ThrowIfDisposed();
		using var cursor = Execute(handle);
		if (!cursor.MoveNext())
			return;

		cursor.ForEach(action);
	}

	public void ForEach<TSpec, T1, T2>(QueryHandle<TSpec> handle, QueryCursor.RefInAction<T1, T2> action)
		where TSpec : struct, ICompiledQuerySpec
		where T1 : unmanaged
		where T2 : unmanaged
	{
		if (action is null) throw new ArgumentNullException(nameof(action));

		ThrowIfDisposed();
		using var cursor = Execute(handle);
		if (!cursor.MoveNext())
			return;

		cursor.ForEach(action);
	}

	public void ForEach<TSpec, T1, T2, T3>(QueryHandle<TSpec> handle, QueryCursor.RefInAction<T1, T2, T3> action)
		where TSpec : struct, ICompiledQuerySpec
		where T1 : unmanaged
		where T2 : unmanaged
		where T3 : unmanaged
	{
		if (action is null) throw new ArgumentNullException(nameof(action));

		ThrowIfDisposed();
		using var cursor = Execute(handle);
		if (!cursor.MoveNext())
			return;

		cursor.ForEach(action);
	}

	/// <summary>
	///     Executes a no-allocation sequential loop over one mutable and three read-only unmanaged components.
	/// </summary>
	/// <typeparam name="TSpec">Query specification type.</typeparam>
	/// <typeparam name="T1">Mutable unmanaged component type.</typeparam>
	/// <typeparam name="T2">First read-only unmanaged component type.</typeparam>
	/// <typeparam name="T3">Second read-only unmanaged component type.</typeparam>
	/// <typeparam name="T4">Third read-only unmanaged component type.</typeparam>
	/// <param name="handle">Compiled query handle.</param>
	/// <param name="action">Per-entity callback.</param>
	public void ForEach<TSpec, T1, T2, T3, T4>(
		QueryHandle<TSpec>                      handle,
		QueryCursor.RefInAction<T1, T2, T3, T4> action)
		where TSpec : struct, ICompiledQuerySpec
		where T1 : unmanaged
		where T2 : unmanaged
		where T3 : unmanaged
		where T4 : unmanaged
	{
		if (action is null) throw new ArgumentNullException(nameof(action));

		ThrowIfDisposed();
		using var cursor = Execute(handle);
		if (!cursor.MoveNext())
			return;

		cursor.ForEach(action);
	}

	public void LateTick(float deltaTime) => RunPhase(SystemLoopPhase.LateTick, deltaTime);

	public void Playback(CommandStream stream)
	{
		ThrowIfDisposed();
		if (stream is null) throw new ArgumentNullException(nameof(stream));
		if (!ReferenceEquals(stream.Owner, this))
			throw new InvalidOperationException("Command stream belongs to a different world.");

		if (_queryEngine.HasActiveCursors)
			throw new InvalidOperationException("Playback cannot run while a query cursor is active.");

		stream.PlaybackInternal();
	}

	public void Precompile<TSpec>() where TSpec : struct, ICompiledQuerySpec =>
		_ = Compile<TSpec>();

	public void Remove<T>(Entity entity) where T : struct
	{
		ThrowIfDisposed();
		int typeId = GetOrCreateComponentTypeId<T>();
		RemoveComponentFromCommand(entity, typeId);
	}

	public void Replace<T>(Entity entity, in T component) where T : struct =>
		Set(entity, in component);

	public void ReplaceResource<T>(T resource) where T : notnull =>
		SetResource(resource);

	public void Reset()
	{
		ThrowIfDisposed();
		_lifecycleService.Reset();
	}

	/// <summary>
	///     Restores world state from snapshot payload supplied by <paramref name="reader" />.
	/// </summary>
	/// <typeparam name="TSnapshotReader">Reader type providing the snapshot payload.</typeparam>
	/// <param name="reader">Snapshot reader providing restore payload.</param>
	/// <param name="options">Optional type-allowlist and validation options.</param>
	public void RestoreSnapshot<TSnapshotReader>(
		ref TSnapshotReader             reader,
		SnapshotDeserializationOptions? options = null)
		where TSnapshotReader : struct, IWorldSnapshotReader
	{
		ThrowIfDisposed();
		_snapshotCoordinator.Restore(ref reader, options, TypeCount);
	}

	public void Run<TSpec, TJob, T1>(QueryHandle<TSpec> handle, TJob job)
		where TSpec : struct, ICompiledQuerySpec
		where TJob : struct, IForEach<T1>
		where T1 : unmanaged
	{
		ThrowIfDisposed();
		_directIterationService.RunDirectFast<TSpec, TJob, T1>(handle, job);
	}

	public void Run<TSpec, TJob, T1, T2>(QueryHandle<TSpec> handle, TJob job)
		where TSpec : struct, ICompiledQuerySpec
		where TJob : struct, IForEach<T1, T2>
		where T1 : unmanaged
		where T2 : unmanaged
	{
		ThrowIfDisposed();
		_directIterationService.RunDirectFast<TSpec, TJob, T1, T2>(handle, job);
	}

	public void Run<TSpec, TJob, T1, T2, T3>(QueryHandle<TSpec> handle, TJob job)
		where TSpec : struct, ICompiledQuerySpec
		where TJob : struct, IForEach<T1, T2, T3>
		where T1 : unmanaged
		where T2 : unmanaged
		where T3 : unmanaged
	{
		ThrowIfDisposed();
		_directIterationService.RunDirectFast<TSpec, TJob, T1, T2, T3>(handle, job);
	}

	/// <summary>
	///     Executes a no-allocation sequential struct job over one mutable and three read-only unmanaged components.
	/// </summary>
	/// <typeparam name="TSpec">Query specification type.</typeparam>
	/// <typeparam name="TJob">Job type implementing <see cref="IForEach{T1, T2, T3, T4}" />.</typeparam>
	/// <typeparam name="T1">Mutable unmanaged component type.</typeparam>
	/// <typeparam name="T2">First read-only unmanaged component type.</typeparam>
	/// <typeparam name="T3">Second read-only unmanaged component type.</typeparam>
	/// <typeparam name="T4">Third read-only unmanaged component type.</typeparam>
	/// <param name="handle">Compiled query handle.</param>
	/// <param name="job">Job instance.</param>
	public void Run<TSpec, TJob, T1, T2, T3, T4>(QueryHandle<TSpec> handle, TJob job)
		where TSpec : struct, ICompiledQuerySpec
		where TJob : struct, IForEach<T1, T2, T3, T4>
		where T1 : unmanaged
		where T2 : unmanaged
		where T3 : unmanaged
		where T4 : unmanaged
	{
		ThrowIfDisposed();
		_directIterationService.RunDirectFast<TSpec, TJob, T1, T2, T3, T4>(handle, job);
	}

	/// <summary>
	///     Executes an entity-aware struct job over one mutable unmanaged component.
	/// </summary>
	public void RunEntity<TSpec, TJob, T1>(QueryHandle<TSpec> handle, TJob job)
		where TSpec : struct, ICompiledQuerySpec
		where TJob : struct, IForEachEntity<T1>
		where T1 : unmanaged
	{
		ThrowIfDisposed();
		_directIterationService.RunDirectFastEntity<TSpec, TJob, T1>(handle, job);
	}

	/// <summary>
	///     Executes an entity-aware struct job over one mutable and one read-only unmanaged component.
	/// </summary>
	public void RunEntity<TSpec, TJob, T1, T2>(QueryHandle<TSpec> handle, TJob job)
		where TSpec : struct, ICompiledQuerySpec
		where TJob : struct, IForEachEntity<T1, T2>
		where T1 : unmanaged
		where T2 : unmanaged
	{
		ThrowIfDisposed();
		_directIterationService.RunDirectFastEntity<TSpec, TJob, T1, T2>(handle, job);
	}

	/// <summary>
	///     Executes an entity-aware struct job over one mutable and two read-only unmanaged components.
	/// </summary>
	public void RunEntity<TSpec, TJob, T1, T2, T3>(QueryHandle<TSpec> handle, TJob job)
		where TSpec : struct, ICompiledQuerySpec
		where TJob : struct, IForEachEntity<T1, T2, T3>
		where T1 : unmanaged
		where T2 : unmanaged
		where T3 : unmanaged
	{
		ThrowIfDisposed();
		_directIterationService.RunDirectFastEntity<TSpec, TJob, T1, T2, T3>(handle, job);
	}

	/// <summary>
	///     Executes an entity-aware struct job over one mutable and three read-only unmanaged components.
	/// </summary>
	public void RunEntity<TSpec, TJob, T1, T2, T3, T4>(QueryHandle<TSpec> handle, TJob job)
		where TSpec : struct, ICompiledQuerySpec
		where TJob : struct, IForEachEntity<T1, T2, T3, T4>
		where T1 : unmanaged
		where T2 : unmanaged
		where T3 : unmanaged
		where T4 : unmanaged
	{
		ThrowIfDisposed();
		_directIterationService.RunDirectFastEntity<TSpec, TJob, T1, T2, T3, T4>(handle, job);
	}

	/// <summary>
	///     Executes a struct job in parallel over one mutable unmanaged component.
	/// </summary>
	/// <typeparam name="TSpec">Query specification type.</typeparam>
	/// <typeparam name="TJob">Job type implementing <see cref="IForEach{T1}" />.</typeparam>
	/// <typeparam name="T1">Mutable unmanaged component type.</typeparam>
	/// <param name="handle">Compiled query handle.</param>
	/// <param name="job">Job instance.</param>
	/// <param name="degreeOfParallelism">
	///     Optional worker limit. When null, <see cref="WorldConfig.MaxDegreeOfParallelism" /> is used.
	/// </param>
	public void RunParallel<TSpec, TJob, T1>(QueryHandle<TSpec> handle, TJob job, int? degreeOfParallelism = null)
		where TSpec : struct, ICompiledQuerySpec
		where TJob : struct, IForEach<T1>
		where T1 : unmanaged
	{
		ThrowIfDisposed();
		_directIterationService.RunParallel<TSpec, TJob, T1>(handle, job, degreeOfParallelism);
	}

	/// <summary>
	///     Executes a struct job in parallel over one mutable and one read-only unmanaged component.
	/// </summary>
	/// <typeparam name="TSpec">Query specification type.</typeparam>
	/// <typeparam name="TJob">Job type implementing <see cref="IForEach{T1, T2}" />.</typeparam>
	/// <typeparam name="T1">Mutable unmanaged component type.</typeparam>
	/// <typeparam name="T2">Read-only unmanaged component type.</typeparam>
	/// <param name="handle">Compiled query handle.</param>
	/// <param name="job">Job instance.</param>
	/// <param name="degreeOfParallelism">
	///     Optional worker limit. When null, <see cref="WorldConfig.MaxDegreeOfParallelism" /> is used.
	/// </param>
	public void RunParallel<TSpec, TJob, T1, T2>(
		QueryHandle<TSpec> handle,
		TJob               job,
		int?               degreeOfParallelism = null)
		where TSpec : struct, ICompiledQuerySpec
		where TJob : struct, IForEach<T1, T2>
		where T1 : unmanaged
		where T2 : unmanaged
	{
		ThrowIfDisposed();
		_directIterationService.RunParallel<TSpec, TJob, T1, T2>(handle, job, degreeOfParallelism);
	}

	/// <summary>
	///     Executes a struct job in parallel over one mutable and two read-only unmanaged components.
	/// </summary>
	/// <typeparam name="TSpec">Query specification type.</typeparam>
	/// <typeparam name="TJob">Job type implementing <see cref="IForEach{T1, T2, T3}" />.</typeparam>
	/// <typeparam name="T1">Mutable unmanaged component type.</typeparam>
	/// <typeparam name="T2">First read-only unmanaged component type.</typeparam>
	/// <typeparam name="T3">Second read-only unmanaged component type.</typeparam>
	/// <param name="handle">Compiled query handle.</param>
	/// <param name="job">Job instance.</param>
	/// <param name="degreeOfParallelism">
	///     Optional worker limit. When null, <see cref="WorldConfig.MaxDegreeOfParallelism" /> is used.
	/// </param>
	public void RunParallel<TSpec, TJob, T1, T2, T3>(
		QueryHandle<TSpec> handle,
		TJob               job,
		int?               degreeOfParallelism = null)
		where TSpec : struct, ICompiledQuerySpec
		where TJob : struct, IForEach<T1, T2, T3>
		where T1 : unmanaged
		where T2 : unmanaged
		where T3 : unmanaged
	{
		ThrowIfDisposed();
		_directIterationService.RunParallel<TSpec, TJob, T1, T2, T3>(handle, job, degreeOfParallelism);
	}

	/// <summary>
	///     Executes a struct job in parallel over one mutable and three read-only unmanaged components.
	/// </summary>
	/// <typeparam name="TSpec">Query specification type.</typeparam>
	/// <typeparam name="TJob">Job type implementing <see cref="IForEach{T1, T2, T3, T4}" />.</typeparam>
	/// <typeparam name="T1">Mutable unmanaged component type.</typeparam>
	/// <typeparam name="T2">First read-only unmanaged component type.</typeparam>
	/// <typeparam name="T3">Second read-only unmanaged component type.</typeparam>
	/// <typeparam name="T4">Third read-only unmanaged component type.</typeparam>
	/// <param name="handle">Compiled query handle.</param>
	/// <param name="job">Job instance.</param>
	/// <param name="degreeOfParallelism">
	///     Optional worker limit. When null, <see cref="WorldConfig.MaxDegreeOfParallelism" /> is used.
	/// </param>
	public void RunParallel<TSpec, TJob, T1, T2, T3, T4>(
		QueryHandle<TSpec> handle,
		TJob               job,
		int?               degreeOfParallelism = null)
		where TSpec : struct, ICompiledQuerySpec
		where TJob : struct, IForEach<T1, T2, T3, T4>
		where T1 : unmanaged
		where T2 : unmanaged
		where T3 : unmanaged
		where T4 : unmanaged
	{
		ThrowIfDisposed();
		_directIterationService.RunParallel<TSpec, TJob, T1, T2, T3, T4>(handle, job, degreeOfParallelism);
	}

	/// <summary>
	///     Executes an entity-aware struct job in parallel over one mutable unmanaged component.
	/// </summary>
	public void RunParallelEntity<TSpec, TJob, T1>(QueryHandle<TSpec> handle, TJob job, int? degreeOfParallelism = null)
		where TSpec : struct, ICompiledQuerySpec
		where TJob : struct, IForEachEntity<T1>
		where T1 : unmanaged
	{
		ThrowIfDisposed();
		_directIterationService.RunParallelEntity<TSpec, TJob, T1>(handle, job, degreeOfParallelism);
	}

	/// <summary>
	///     Executes an entity-aware struct job in parallel over one mutable and one read-only unmanaged component.
	/// </summary>
	public void RunParallelEntity<TSpec, TJob, T1, T2>(
		QueryHandle<TSpec> handle,
		TJob               job,
		int?               degreeOfParallelism = null)
		where TSpec : struct, ICompiledQuerySpec
		where TJob : struct, IForEachEntity<T1, T2>
		where T1 : unmanaged
		where T2 : unmanaged
	{
		ThrowIfDisposed();
		_directIterationService.RunParallelEntity<TSpec, TJob, T1, T2>(handle, job, degreeOfParallelism);
	}

	/// <summary>
	///     Executes an entity-aware struct job in parallel over one mutable and two read-only unmanaged components.
	/// </summary>
	public void RunParallelEntity<TSpec, TJob, T1, T2, T3>(
		QueryHandle<TSpec> handle,
		TJob               job,
		int?               degreeOfParallelism = null)
		where TSpec : struct, ICompiledQuerySpec
		where TJob : struct, IForEachEntity<T1, T2, T3>
		where T1 : unmanaged
		where T2 : unmanaged
		where T3 : unmanaged
	{
		ThrowIfDisposed();
		_directIterationService.RunParallelEntity<TSpec, TJob, T1, T2, T3>(handle, job, degreeOfParallelism);
	}

	/// <summary>
	///     Executes an entity-aware struct job in parallel over one mutable and three read-only unmanaged components.
	/// </summary>
	public void RunParallelEntity<TSpec, TJob, T1, T2, T3, T4>(
		QueryHandle<TSpec> handle,
		TJob               job,
		int?               degreeOfParallelism = null)
		where TSpec : struct, ICompiledQuerySpec
		where TJob : struct, IForEachEntity<T1, T2, T3, T4>
		where T1 : unmanaged
		where T2 : unmanaged
		where T3 : unmanaged
		where T4 : unmanaged
	{
		ThrowIfDisposed();
		_directIterationService.RunParallelEntity<TSpec, TJob, T1, T2, T3, T4>(handle, job, degreeOfParallelism);
	}

	public void RunPhase(SystemLoopPhase loopPhase, float deltaTime)
	{
		ThrowIfDisposed();
		_systemRuntime.RunPhase(this, loopPhase, deltaTime);
	}

	public void Set<T>(Entity entity, in T component) where T : struct
	{
		ThrowIfDisposed();
		ApplySetFromCommand(entity, in component);
	}

	public void SetResource<T>(T resource) where T : notnull
	{
		ThrowIfDisposed();
		_resources.Set(resource);
	}

	public void SetSystemSetEnabled<TSet>(bool enabled)
	{
		ThrowIfDisposed();
		_systemRuntime.SetSystemSetEnabled(typeof(TSet), enabled);
	}

	public void SetSystemSetRunCondition<TSet>(ISystemRunCondition runCondition)
	{
		ThrowIfDisposed();
		if (runCondition is null)
			throw new ArgumentNullException(nameof(runCondition));

		_systemRuntime.SetSystemSetRunCondition(typeof(TSet), runCondition);
	}

	public void Tick(float deltaTime) => RunPhase(SystemLoopPhase.Tick, deltaTime);

	public WorldDiagnostics GetDiagnostics()
	{
		ThrowIfDisposed();
		return new(
			new("Entities", _config.EntityCapacity, AliveCount, EntityHighWatermark, EntityOverflowCount),
			new(
				"ComponentTypes",
				_config.ComponentTypeCapacity,
				TypeCount,
				RegisteredTypeHighWatermark,
				ComponentTypeOverflowCount
			),
			new(
				"QueryResults", _config.QueryResultCapacity, 0, _changeTracker.QueryHighWatermark,
				_changeTracker.QueryOverflowCount
			)
		);
	}

	internal ArchetypeStorage GetArchetypeForCursor(int archetypeId)
	{
		if ((uint)archetypeId >= (uint)Archetypes.Count)
			throw new ArgumentOutOfRangeException(nameof(archetypeId));

		return Archetypes[archetypeId];
	}

	internal bool ContainsReferencesForEntityStore(Type componentType) => ContainsReferences(componentType);

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	internal bool HasComponentForAccessor(
		Entity  entity,
		int     typeId,
		ref int cachedArchetypeId,
		ref int cachedColumnIndex)
	{
		ThrowIfDisposed();
		if (!IsAliveUnchecked(entity))
			return false;

		var location = LocationByEntityId[entity.Id];
		if (!location.IsValid)
			return false;

		var archetype = Archetypes[location.ArchetypeId];
		return TryResolveAccessorColumnIndex(
			archetype,
			location.ArchetypeId,
			typeId,
			ref cachedArchetypeId,
			ref cachedColumnIndex
		);
	}

	internal bool MatchesRemoveTransitionSource(
		Entity entity,
		int    sourceArchetypeId,
		int    typeId,
		int    targetArchetypeId)
	{
		EnsureAlive(entity);
		var location = LocationByEntityId[entity.Id];
		if (!location.IsValid)
			throw new InvalidOperationException($"Entity '{entity.Id}' is not in a valid archetype.");

		if (location.ArchetypeId != sourceArchetypeId)
			return false;

		var sourceArchetype = Archetypes[sourceArchetypeId];
		if (targetArchetypeId == sourceArchetypeId)
			return !sourceArchetype.HasType(typeId);

		return sourceArchetype.HasType(typeId);
	}

	internal bool MatchesSetTransitionSource(
		Entity entity,
		int    sourceArchetypeId,
		int    typeId,
		int    targetArchetypeId)
	{
		EnsureAlive(entity);
		var location = LocationByEntityId[entity.Id];
		if (!location.IsValid)
			throw new InvalidOperationException($"Entity '{entity.Id}' is not in a valid archetype.");

		if (location.ArchetypeId != sourceArchetypeId)
			return false;

		var sourceArchetype = Archetypes[sourceArchetypeId];
		if (targetArchetypeId == sourceArchetypeId)
			return sourceArchetype.HasType(typeId);

		return !sourceArchetype.HasType(typeId);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	internal bool TryGetComponentForAccessor<T>(
		Entity  entity,
		int     typeId,
		ref int cachedArchetypeId,
		ref int cachedColumnIndex,
		out T   component)
		where T : unmanaged
	{
		ThrowIfDisposed();
		if (!IsAliveUnchecked(entity))
		{
			component = default;
			return false;
		}

		var location = LocationByEntityId[entity.Id];
		if (!location.IsValid)
		{
			component = default;
			return false;
		}

		var archetype = Archetypes[location.ArchetypeId];
		if (!TryResolveAccessorColumnIndex(
				archetype,
				location.ArchetypeId,
				typeId,
				ref cachedArchetypeId,
				ref cachedColumnIndex
			))
		{
			component = default;
			return false;
		}

		var chunk = archetype.GetChunkUnchecked(location.ChunkIndex);
		component = archetype.GetRefByIndex<T>(chunk, cachedColumnIndex, location.RowIndex);
		return true;
	}

	internal Entity CreateEntityInternal()
		=> _entityStore.CreateEntity();

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	internal Entity GetEntityForCursor(int entityId) =>
		new(entityId, VersionByEntityId[entityId]);

	internal int FillQueryResultsForQueryEngine(
		CompiledQueryPlan plan,
		QueryChunkMatch[] chunkMatches,
		int               entityCapacity,
		out int           chunkMatchCount,
		bool              advanceObservedChangeVersion = true) =>
		_queryRuntime.FillQueryResults(
			plan,
			chunkMatches,
			entityCapacity,
			out chunkMatchCount,
			advanceObservedChangeVersion
		);

	internal int GetArchetypeEntityCount(int archetypeId)
		=> _entityStore.GetArchetypeEntityCount(archetypeId);

	internal int GetOrCreateComponentTypeId<T>() where T : struct
	{
		if (Volatile.Read(ref ComponentTypeIdCache<T>.OwnerInstanceId) == _instanceId)
			return ComponentTypeIdCache<T>.TypeId;

		int typeId = _entityStore.GetOrCreateComponentTypeIdGeneric<T>();
		ComponentTypeIdCache<T>.TypeId = typeId;
		Volatile.Write(ref ComponentTypeIdCache<T>.OwnerInstanceId, _instanceId);
		return typeId;
	}

	internal int GetOrCreateComponentTypeId(Type componentType)
	{
		if (componentType is null) throw new ArgumentNullException(nameof(componentType));
		if (!componentType.IsValueType)
			throw new ArgumentException("Components must be value types.", nameof(componentType));

		return _entityStore.GetOrCreateComponentTypeId(componentType, ContainsReferences(componentType));
	}

	internal int GetOrRefreshMatchingArchetypesForQueryEngine(CompiledQueryPlan plan) =>
		_queryRuntime.GetOrRefreshMatchingArchetypes(plan);

	internal int ResolveDegreeOfParallelismForDirectIteration(int? degreeOfParallelism) =>
		ResolveDegreeOfParallelism(degreeOfParallelism);

	internal int[] BuildMaskWordIndices(ulong[] maskWords) => _entityStore.BuildMaskWordIndices(maskWords);

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	internal int[] GetEntityVersionsForCursor() => VersionByEntityId;

	internal ref T GetComponentRefForCursorUnchecked<T>(int entityId, int typeId) where T : struct
		=> ref _entityStore.GetComponentRefForCursorUnchecked<T>(entityId, typeId);

	internal Type[] ResolveTypesForQueryEngine(int[] typeIds) => _queryRuntime.ResolveTypes(typeIds);

	internal uint AdvanceComponentChangeVersionForEntityStore() => _changeTracker.AdvanceComponentChangeVersion();

	internal ulong[] BuildMaskWords(int[] typeIds) => _entityStore.BuildMaskWords(typeIds);

	internal void AcquireQueryChunkMatchScratchForDirectIteration(
		out QueryChunkMatch[] chunkMatches,
		out bool              usesSharedScratch) =>
		AcquireQueryChunkMatchScratch(out chunkMatches, out usesSharedScratch);

	internal void AcquireQueryExecutionScratchForDirectIteration(
		out QueryChunkMatch[] chunkMatches,
		out Entity[]          entities,
		out bool              usesSharedScratch) =>
		AcquireQueryExecutionScratch(out chunkMatches, out entities, out usesSharedScratch);

	internal void AddRelationFromSnapshot(Type relationType, Entity source, Entity target)
	{
		if (relationType is null) throw new ArgumentNullException(nameof(relationType));
		if (target == Entity.Wildcard || target == Entity.None)
			throw new ArgumentException("Snapshot relation target must be a concrete entity.", nameof(target));

		EnsureAlive(source);
		EnsureAlive(target);
		int relationTypeId = _relationIndex.GetOrCreateRelationTypeId(
			relationType,
			target,
			_entityStore.CreateRelationTypeId
		);

		_entityStore.SetRelationComponent(source, relationTypeId);
	}

	internal void ApplySetBatchFromCommandKnownTransition<T>(
		int[] entityIds,
		int   entityOffset,
		int   count,
		T[]   payloads,
		int   payloadCount,
		int[] payloadIndices,
		int   payloadOffset,
		int   typeId,
		int   sourceArchetypeId,
		int   targetArchetypeId)
		where T : struct
		=> _entityStore.ApplySetBatchFromCommandKnownTransition(
			entityIds,
			entityOffset,
			count,
			payloads,
			payloadCount,
			payloadIndices,
			payloadOffset,
			typeId,
			sourceArchetypeId,
			targetArchetypeId
		);

	internal void ApplySetBatchFromCommandKnownTransitionFast<T>(
		int[] entityIds,
		int   entityOffset,
		int   count,
		T[]   payloads,
		int[] payloadIndices,
		int   payloadOffset,
		int   typeId,
		int   sourceArchetypeId,
		int   targetArchetypeId)
		where T : struct
		=> _entityStore.ApplySetBatchFromCommandKnownTransitionFast(
			entityIds,
			entityOffset,
			count,
			payloads,
			payloadIndices,
			payloadOffset,
			typeId,
			sourceArchetypeId,
			targetArchetypeId
		);

	internal void ApplySetFromCommand<T>(Entity entity, in T component) where T : struct
		=> _entityStore.ApplySetFromCommand(entity, component);

	internal void ApplySetFromCommandKnownTransition<T>(
		Entity entity,
		in T   component,
		int    typeId,
		int    sourceArchetypeId,
		int    targetArchetypeId)
		where T : struct
		=> _entityStore.ApplySetFromCommandKnownTransition(
			entity,
			component,
			typeId,
			sourceArchetypeId,
			targetArchetypeId
		);

	internal void ClearEntityComponentVersionsForEntityStore(int entityId, int typeCount) =>
		_changeTracker.ClearEntityComponentVersions(entityId, typeCount);

	internal void DescribeRemoveTransition(
		Entity  entity,
		int     typeId,
		out int sourceArchetypeId,
		out int targetArchetypeId)
		=> _entityStore.DescribeRemoveTransition(entity, typeId, out sourceArchetypeId, out targetArchetypeId);

	internal void DescribeSetTransition(Entity entity, int typeId, out int sourceArchetypeId, out int targetArchetypeId)
		=> _entityStore.DescribeSetTransition(entity, typeId, out sourceArchetypeId, out targetArchetypeId);

	internal void DestroyEntityInternal(Entity entity)
		=> _entityStore.DestroyEntity(entity);

	internal void EnableRefWriteTrackingForQuery(CompiledQueryPlan plan)
		=> _changeTracker.EnableRefWriteTracking(plan);

	internal void ExecuteDirectEntityAction<TSpec, TAction, T1>(QueryHandle<TSpec> handle, TAction action)
		where TSpec : struct, ICompiledQuerySpec
		where TAction : struct, IEntityChunkAction<T1>
		where T1 : struct
	{
		ThrowIfDisposed();
		_directIterationService.ExecuteDirectEntityAction<TSpec, TAction, T1>(handle, action);
	}

	internal void ExecuteDirectEntityAction<TSpec, TAction, T1, T2>(QueryHandle<TSpec> handle, TAction action)
		where TSpec : struct, ICompiledQuerySpec
		where TAction : struct, IEntityChunkAction<T1, T2>
		where T1 : struct
		where T2 : struct
	{
		ThrowIfDisposed();
		_directIterationService.ExecuteDirectEntityAction<TSpec, TAction, T1, T2>(handle, action);
	}

	internal void ExecuteDirectEntityAction<TSpec, TAction, T1, T2, T3>(QueryHandle<TSpec> handle, TAction action)
		where TSpec : struct, ICompiledQuerySpec
		where TAction : struct, IEntityChunkAction<T1, T2, T3>
		where T1 : struct
		where T2 : struct
		where T3 : struct
	{
		ThrowIfDisposed();
		_directIterationService.ExecuteDirectEntityAction<TSpec, TAction, T1, T2, T3>(handle, action);
	}

	internal void ExecuteDirectEntityAction<TSpec, TAction, T1, T2, T3, T4>(QueryHandle<TSpec> handle, TAction action)
		where TSpec : struct, ICompiledQuerySpec
		where TAction : struct, IEntityChunkAction<T1, T2, T3, T4>
		where T1 : struct
		where T2 : struct
		where T3 : struct
		where T4 : struct
	{
		ThrowIfDisposed();
		_directIterationService.ExecuteDirectEntityAction<TSpec, TAction, T1, T2, T3, T4>(handle, action);
	}

	internal void ExitQueryCursor()
	{
		_queryEngine.ExitCursors();
	}

	internal void HandleQueryOverflowForDirectIteration() => _changeTracker.HandleQueryOverflow();

	internal void MarkComponentAddedForEntityStore(int entityId, int typeId, uint version) =>
		_changeTracker.MarkComponentAdded(entityId, typeId, version);

	internal void MarkComponentChangedForEntityStore(int entityId, int typeId, uint version) =>
		_changeTracker.MarkComponentChanged(entityId, typeId, version);

	internal void MaterializeQueryEntities(
		QueryChunkMatch[] chunkMatches,
		int               chunkMatchCount,
		Entity[]          destination,
		int               entityCount)
		=> _queryEngine.MaterializeQueryEntities(chunkMatches, chunkMatchCount, destination, entityCount);

	internal void ObserveDirectIterationEntityCount(int processedEntityCount)
		=> _changeTracker.ObserveDirectIterationEntityCount(processedEntityCount);

	internal void ReleaseCursorQueryExecution(
		QueryChunkMatch[]   chunkMatches,
		Entity[]            entities,
		bool                usesSharedScratch,
		QueryExecutionLease lease)
		=> _queryEngine.ReleaseCursorQueryExecution(chunkMatches, entities, usesSharedScratch, lease);

	internal void ReleaseQueryChunkMatchScratchForDirectIteration(
		QueryChunkMatch[] chunkMatches,
		bool              usesSharedScratch) =>
		ReleaseQueryChunkMatchScratch(chunkMatches, usesSharedScratch);

	internal void ReleaseQueryExecutionScratchForDirectIteration(
		QueryChunkMatch[] chunkMatches,
		Entity[]          entities,
		bool              usesSharedScratch) =>
		ReleaseQueryExecutionScratch(chunkMatches, entities, usesSharedScratch);

	internal void RemoveAllComponentsFromCommandKnownTransitionFast(int sourceArchetypeId, int targetArchetypeId)
		=> _entityStore.RemoveAllComponentsFromCommandKnownTransitionFast(sourceArchetypeId, targetArchetypeId);

	internal void RemoveComponentBatchFromCommandKnownTransition(
		int[] entityIds,
		int   entityOffset,
		int   count,
		int   typeId,
		int   sourceArchetypeId,
		int   targetArchetypeId)
		=> _entityStore.RemoveComponentBatchFromCommandKnownTransition(
			entityIds,
			entityOffset,
			count,
			typeId,
			sourceArchetypeId,
			targetArchetypeId
		);

	internal void RemoveComponentBatchFromCommandKnownTransitionFast(
		int[] entityIds,
		int   entityOffset,
		int   count,
		int   sourceArchetypeId,
		int   targetArchetypeId)
		=> _entityStore.RemoveComponentBatchFromCommandKnownTransitionFast(
			entityIds,
			entityOffset,
			count,
			sourceArchetypeId,
			targetArchetypeId
		);

	internal void RemoveComponentFromCommand(Entity entity, int typeId)
		=> _entityStore.RemoveComponentFromCommand(entity, typeId);

	internal void RemoveComponentFromCommandKnownTransition(
		Entity entity,
		int    typeId,
		int    sourceArchetypeId,
		int    targetArchetypeId)
		=> _entityStore.RemoveComponentFromCommandKnownTransition(
			entity,
			typeId,
			sourceArchetypeId,
			targetArchetypeId
		);

	internal void RemoveMarkedComponentsFromCommandKnownTransitionFast(
		uint[] batchEntityMarkerBits,
		int    sourceArchetypeId,
		int    targetArchetypeId)
		=> _entityStore.RemoveMarkedComponentsFromCommandKnownTransitionFast(
			batchEntityMarkerBits,
			sourceArchetypeId,
			targetArchetypeId
		);

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	internal void ResolveAccessorLocation(
		Entity               entity,
		int                  typeId,
		Type                 componentType,
		ref int              cachedArchetypeId,
		ref int              cachedColumnIndex,
		out ArchetypeStorage archetype,
		out int              chunkIndex,
		out int              rowIndex)
	{
		if (componentType is null) throw new ArgumentNullException(nameof(componentType));

		ThrowIfDisposed();
		if (!IsAliveUnchecked(entity))
			throw new InvalidOperationException($"Entity '{entity.Id}:{entity.Version}' is not alive.");

		var location = LocationByEntityId[entity.Id];
		if (!location.IsValid)
			throw new InvalidOperationException($"Entity '{entity.Id}' is not in a valid archetype.");

		archetype = Archetypes[location.ArchetypeId];
		if (!TryResolveAccessorColumnIndex(
				archetype,
				location.ArchetypeId,
				typeId,
				ref cachedArchetypeId,
				ref cachedColumnIndex
			))
			throw new KeyNotFoundException(
				$"Component '{componentType.Name}' was not found for entity '{entity.Id}:{entity.Version}'."
			);

		chunkIndex = location.ChunkIndex;
		rowIndex   = location.RowIndex;
	}

	internal void RunDirectFast<TSpec, TJob, T1>(QueryHandle<TSpec> handle, TJob job)
		where TSpec : struct, ICompiledQuerySpec
		where TJob : struct, IForEach<T1>
		where T1 : unmanaged
		=> _directIterationService.RunDirectFast<TSpec, TJob, T1>(handle, job);

	internal void RunDirectFast<TSpec, TJob, T1, T2>(QueryHandle<TSpec> handle, TJob job)
		where TSpec : struct, ICompiledQuerySpec
		where TJob : struct, IForEach<T1, T2>
		where T1 : unmanaged
		where T2 : unmanaged
		=> _directIterationService.RunDirectFast<TSpec, TJob, T1, T2>(handle, job);

	internal void RunDirectFast<TSpec, TJob, T1, T2, T3>(QueryHandle<TSpec> handle, TJob job)
		where TSpec : struct, ICompiledQuerySpec
		where TJob : struct, IForEach<T1, T2, T3>
		where T1 : unmanaged
		where T2 : unmanaged
		where T3 : unmanaged
		=> _directIterationService.RunDirectFast<TSpec, TJob, T1, T2, T3>(handle, job);

	internal void RunDirectFast<TSpec, TJob, T1, T2, T3, T4>(QueryHandle<TSpec> handle, TJob job)
		where TSpec : struct, ICompiledQuerySpec
		where TJob : struct, IForEach<T1, T2, T3, T4>
		where T1 : unmanaged
		where T2 : unmanaged
		where T3 : unmanaged
		where T4 : unmanaged
		=> _directIterationService.RunDirectFast<TSpec, TJob, T1, T2, T3, T4>(handle, job);

	internal void RunDirectFastEntity<TSpec, TJob, T1>(QueryHandle<TSpec> handle, TJob job)
		where TSpec : struct, ICompiledQuerySpec
		where TJob : struct, IForEachEntity<T1>
		where T1 : unmanaged
		=> _directIterationService.RunDirectFastEntity<TSpec, TJob, T1>(handle, job);

	internal void RunDirectFastEntity<TSpec, TJob, T1, T2>(QueryHandle<TSpec> handle, TJob job)
		where TSpec : struct, ICompiledQuerySpec
		where TJob : struct, IForEachEntity<T1, T2>
		where T1 : unmanaged
		where T2 : unmanaged
		=> _directIterationService.RunDirectFastEntity<TSpec, TJob, T1, T2>(handle, job);

	internal void RunDirectFastEntity<TSpec, TJob, T1, T2, T3>(QueryHandle<TSpec> handle, TJob job)
		where TSpec : struct, ICompiledQuerySpec
		where TJob : struct, IForEachEntity<T1, T2, T3>
		where T1 : unmanaged
		where T2 : unmanaged
		where T3 : unmanaged
		=> _directIterationService.RunDirectFastEntity<TSpec, TJob, T1, T2, T3>(handle, job);

	internal void RunDirectFastEntity<TSpec, TJob, T1, T2, T3, T4>(QueryHandle<TSpec> handle, TJob job)
		where TSpec : struct, ICompiledQuerySpec
		where TJob : struct, IForEachEntity<T1, T2, T3, T4>
		where T1 : unmanaged
		where T2 : unmanaged
		where T3 : unmanaged
		where T4 : unmanaged
		=> _directIterationService.RunDirectFastEntity<TSpec, TJob, T1, T2, T3, T4>(handle, job);

	internal void SetComponentFromSnapshot(Entity entity, Type componentType, object value)
		=> _entityStore.SetComponentBoxed(entity, componentType, value);

	internal void SetResourceBoxedFromSnapshot(Type resourceType, object value)
	{
		_resources.SetBoxed(resourceType, value);
	}

	internal void TrackPotentialAccessorRefWrite(
		ArchetypeStorage.Chunk chunk,
		int                    columnIndex,
		int                    typeId,
		int                    rowIndex)
		=> _changeTracker.TrackPotentialAccessorRefWrite(chunk, columnIndex, typeId, rowIndex);

	internal void TrackPotentialChunkMatchRefWrites(
		QueryChunkMatch[] chunkMatches,
		int               chunkMatchCount,
		int               typeId)
		=> _changeTracker.TrackPotentialChunkMatchRefWrites(chunkMatches, chunkMatchCount, typeId);

	internal void TrackPotentialCursorRefWrite(
		ArchetypeStorage.Chunk chunk,
		int                    columnIndex,
		int                    typeId,
		int                    rowIndex) =>
		_changeTracker.TrackPotentialCursorRefWrite(chunk, columnIndex, typeId, rowIndex);

	internal void TrackPotentialDirectFastRefWritesForDirectIteration(
		int[] matchingArchetypeIds,
		int   matchCount,
		int   typeId) =>
		TrackPotentialDirectFastRefWrites(matchingArchetypeIds, matchCount, typeId);

	internal void ValidateDirectIterationHandle<TSpec>(QueryHandle<TSpec> handle)
		where TSpec : struct, ICompiledQuerySpec
	{
		if (!ReferenceEquals(handle.Plan.Owner, this))
			throw new InvalidOperationException("Query handle belongs to a different world.");
	}

	private static bool ContainsReferences(Type type)
	{
		lock (ContainsReferencesSync)
		{
			if (ContainsReferencesByType.TryGetValue(type, out bool cached))
				return cached;

			var contains = (bool)(ContainsReferencesMethod.MakeGenericMethod(type).Invoke(null, null) ?? false);
			ContainsReferencesByType[type] = contains;
			return contains;
		}
	}

	private static bool ContainsReferencesGeneric<T>() where T : struct =>
		RuntimeHelpers.IsReferenceOrContainsReferences<T>();

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static bool TryResolveAccessorColumnIndex(
		ArchetypeStorage archetype,
		int              archetypeId,
		int              typeId,
		ref int          cachedArchetypeId,
		ref int          cachedColumnIndex)
	{
		if (cachedArchetypeId == archetypeId)
			return cachedColumnIndex >= 0;

		cachedArchetypeId = archetypeId;
		int columnIndex = archetype.GetColumnIndexOrNegative(typeId);
		if (columnIndex >= 0)
		{
			cachedColumnIndex = columnIndex;
			return true;
		}

		cachedColumnIndex = -1;
		return false;
	}

	private static WorldConfig ToWorldConfig(WorldOptions options)
	{
		if (options is null) throw new ArgumentNullException(nameof(options));

		return new()
		{
			ChunkCapacity          = options.ChunkCapacity > 0 ? options.ChunkCapacity : 256,
			MaxDegreeOfParallelism = options.MaxDegreeOfParallelism
		};
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private bool IsAliveUnchecked(Entity entity) =>
		entity.Id >= 0 &&
		entity.Id < AliveByEntityId.Length &&
		AliveByEntityId[entity.Id] &&
		VersionByEntityId[entity.Id] == entity.Version;

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private int ResolveDegreeOfParallelism(int? degreeOfParallelism)
	{
		if (degreeOfParallelism is <= 0)
			throw new ArgumentOutOfRangeException(
				nameof(degreeOfParallelism),
				"Degree of parallelism must be greater than zero."
			);

		return degreeOfParallelism ?? _config.MaxDegreeOfParallelism;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private uint AdvanceComponentChangeVersion()
		=> _changeTracker.AdvanceComponentChangeVersion();

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private void AcquireQueryChunkMatchScratch(out QueryChunkMatch[] chunkMatches, out bool usesSharedScratch) =>
		_queryEngine.AcquireQueryChunkMatchScratch(out chunkMatches, out usesSharedScratch);

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private void AcquireQueryExecutionScratch(
		out QueryChunkMatch[] chunkMatches,
		out Entity[]          entities,
		out bool              usesSharedScratch) =>
		_queryEngine.AcquireQueryExecutionScratch(out chunkMatches, out entities, out usesSharedScratch);

	private void EnsureAlive(Entity entity)
	{
		if (!IsAliveUnchecked(entity))
			throw new InvalidOperationException($"Entity '{entity.Id}:{entity.Version}' is not alive.");
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private void MarkComponentAdded(int entityId, int typeId, uint version) =>
		_changeTracker.MarkComponentAdded(entityId, typeId, version);

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private void MarkComponentChanged(int entityId, int typeId, uint version) =>
		_changeTracker.MarkComponentChanged(entityId, typeId, version);

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private void ReleaseQueryChunkMatchScratch(QueryChunkMatch[] chunkMatches, bool usesSharedScratch) =>
		_queryEngine.ReleaseQueryChunkMatchScratch(chunkMatches, usesSharedScratch);

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private void ReleaseQueryExecutionScratch(
		QueryChunkMatch[] chunkMatches,
		Entity[]          entities,
		bool              usesSharedScratch) =>
		_queryEngine.ReleaseQueryExecutionScratch(chunkMatches, entities, usesSharedScratch);

	private void ThrowIfDirectIterationUnavailable<TSpec>(QueryHandle<TSpec> handle)
		where TSpec : struct, ICompiledQuerySpec =>
		ValidateDirectIterationHandle(handle);

	private void ThrowIfDisposed()
	{
		if (_disposed)
			throw new ObjectDisposedException(nameof(World));
	}

	private void TrackPotentialDirectFastRefWrites(int[] matchingArchetypeIds, int matchCount, int typeId)
		=> _changeTracker.TrackPotentialDirectFastRefWrites(matchingArchetypeIds, matchCount, typeId);

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private void TrackPotentialQueryChunkMatchRefWrites(int chunkMatchCount, int typeId)
		=> TrackPotentialChunkMatchRefWrites(_queryEngine.SharedChunkMatches, chunkMatchCount, typeId);

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private void TrackPotentialQueryChunkMatchRefWrites(
		QueryChunkMatch[] chunkMatches,
		int               chunkMatchCount,
		int               typeId)
		=> TrackPotentialChunkMatchRefWrites(chunkMatches, chunkMatchCount, typeId);

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private void TrackPotentialSingleRefWrite(int entityId, int typeId)
	{
		var location = LocationByEntityId[entityId];
		if (!location.IsValid)
			return;

		var archetype   = Archetypes[location.ArchetypeId];
		int columnIndex = archetype.GetColumnIndexOrNegative(typeId);
		if (columnIndex < 0)
			return;

		var chunk = archetype.GetChunkUnchecked(location.ChunkIndex);
		_changeTracker.TrackPotentialAccessorRefWrite(chunk, columnIndex, typeId, location.RowIndex);
	}

	private static class ComponentTypeIdCache<T> where T : struct
	{
		public static int OwnerInstanceId;
		public static int TypeId;
	}
}
