using System.Reflection;
using System.Runtime.CompilerServices;
using Bezoro.ECS.Abstractions;
using Bezoro.ECS.Internal.Fixed;
using Bezoro.ECS.Options;
using Bezoro.ECS.Types;
using RelationMarker = Bezoro.ECS.Internal.RelationMarker;
using RelationshipInfo = Bezoro.ECS.Internal.RelationshipInfo;

namespace Bezoro.ECS.Services;

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
	private static readonly object ContainsReferencesSync = new();
	private static          int    _nextInstanceId;
	private readonly        bool[] _aliveByEntityId;
	private readonly        bool[] _typeIsManagedLane;
	private readonly Dictionary<ArchetypeTypeSetKey, int> _archetypeByTypeSet =
		new(ArchetypeTypeSetKeyComparer.Instance);
	private readonly Dictionary<long, uint>              _addedVersionByEntityType = [];
	private readonly Dictionary<(Type relationType, int targetId, int targetVersion), int> _relationTypeIdByKey = [];
	private readonly Dictionary<(int targetId, int targetVersion), int[]> _relationTypeIdsByTarget = [];
	private readonly Dictionary<Type, int[]>           _relationTypeIdsByRelationType = [];
	private readonly Dictionary<int, RelationshipInfo> _relationInfoByTypeId = [];
	private readonly Dictionary<long, int[]>             _transitionCopyMapByPair = [];
	private readonly Dictionary<long, uint>              _changeVersionByEntityType = [];
	private readonly Dictionary<Type, CompiledQueryPlan> _compiledPlansBySpecType = [];
	private readonly Dictionary<Type, int>               _typeToId                = [];
	private readonly Dictionary<Type, object>            _resources               = [];
	private readonly Entity[]                            _queryEntities;
	private readonly EntityLocation[]                    _locationByEntityId;
	private readonly int                                 _emptyArchetypeId;
	private readonly int                                 _instanceId;
	private readonly int                                 _maskWordCount;
	private readonly int[]                               _freeEntityIds;
	private readonly int[]                               _versionByEntityId;
	private readonly List<ArchetypeStorage>              _archetypes = [];
	private readonly QueryChunkMatch[]                   _queryChunkMatches;
	private readonly SystemManager                       _systemManager;
	private readonly Type?[]                             _typeById;

	private readonly WorldConfig _config;

	private bool _disposed;
	private int  _activeQueryCursors;
	private int  _aliveCount;
	private int  _archetypeVersion;
	private int  _componentTypeOverflowCount;
	private int  _entityHighWatermark;
	private int  _entityOverflowCount;
	private int  _freeCount;
	private int  _nextEntityId;
	private int  _queryHighWatermark;
	private int  _queryOverflowCount;
	private int  _registeredTypeHighWatermark;
	private int  _typeCount;
	private int  _updateDepth;
	private uint _componentChangeVersion;
	private bool _trackRefWriteChanges;

	public World() : this(new WorldConfig()) { }

	public World(WorldOptions options) : this(ToWorldConfig(options)) { }

	public World(WorldConfig config)
	{
		_instanceId = Interlocked.Increment(ref _nextInstanceId);
		_config     = config ?? throw new ArgumentNullException(nameof(config));
		_config.Validate();
		_systemManager = new(_config.MaxDegreeOfParallelism);

		_aliveByEntityId    = new bool[_config.EntityCapacity];
		_versionByEntityId  = new int[_config.EntityCapacity];
		_freeEntityIds      = new int[_config.EntityCapacity];
		_locationByEntityId = new EntityLocation[_config.EntityCapacity];
		for (var i = 0; i < _locationByEntityId.Length; i++)
			_locationByEntityId[i] = EntityLocation.Invalid;

		_queryEntities     = new Entity[_config.QueryResultCapacity];
		_queryChunkMatches = new QueryChunkMatch[_config.QueryResultCapacity];
		_typeById          = new Type[_config.ComponentTypeCapacity];
		_typeIsManagedLane = new bool[_config.ComponentTypeCapacity];
		_maskWordCount     = GetMaskWordCount(_config.ComponentTypeCapacity);

		_emptyArchetypeId = GetOrCreateArchetype([]);
	}

	public int EntityCount
	{
		get
		{
			ThrowIfDisposed();
			return _aliveCount;
		}
	}

	internal int EntityCapacity          => _config.EntityCapacity;
	internal int SchedulerPlanBuildCount => _systemManager.PlanBuildCount;

	public bool Has<T>(Entity entity) where T : struct
	{
		ThrowIfDisposed();
		if (!IsAliveUnchecked(entity))
			return false;

		int typeId   = GetOrCreateComponentTypeId<T>();
		var location = _locationByEntityId[entity.Id];
		return location.IsValid && _archetypes[location.ArchetypeId].HasType(typeId);
	}

	public bool IsAlive(Entity entity)
	{
		ThrowIfDisposed();
		return IsAliveUnchecked(entity);
	}

	public bool TryGet<T>(Entity entity, out T component) where T : struct
	{
		ThrowIfDisposed();
		component = default;
		if (!IsAliveUnchecked(entity))
			return false;

		int typeId = GetOrCreateComponentTypeId<T>();
		return TryGetComponentUnchecked(entity.Id, typeId, out component);
	}

	public bool TryGetManaged<T>(Entity entity, out T component) where T : struct
	{
		ThrowIfDisposed();
		component = default;
		if (!IsAliveUnchecked(entity))
			return false;

		int typeId = GetOrCreateComponentTypeId<T>();
		return TryGetComponentUnchecked(entity.Id, typeId, out component);
	}

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
	///     Thrown if another cursor is already open. Only one active query cursor is supported at a time.
	///     <see cref="World" /> is not thread-safe; do not call from multiple threads concurrently.
	/// </exception>
	public QueryCursor Execute<TSpec>(QueryHandle<TSpec> handle) where TSpec : struct, ICompiledQuerySpec
	{
		ThrowIfDisposed();
		if (_activeQueryCursors > 0)
			throw new InvalidOperationException("Only one active query cursor is supported at a time.");

		if (!ReferenceEquals(handle.Plan.Owner, this))
			throw new InvalidOperationException("Query handle belongs to a different world.");

		int matchCount = FillQueryResults(handle.Plan, out int chunkMatchCount);
		_activeQueryCursors = 1;
		return new(this, _queryChunkMatches, chunkMatchCount, _queryEntities, matchCount);
	}

	public QueryHandle<TSpec> Compile<TSpec>() where TSpec : struct, ICompiledQuerySpec
	{
		ThrowIfDisposed();
		var specType = typeof(TSpec);
		if (_compiledPlansBySpecType.TryGetValue(specType, out var existing))
			return new(existing);

		var builder = new QueryBuilder(this);
		var spec    = default(TSpec);
		spec.Build(ref builder);
		var plan = builder.Build();
		if (plan.ChangedTypeIds.Length > 0 || plan.AddedTypeIds.Length > 0)
			_trackRefWriteChanges = true;

		_compiledPlansBySpecType[specType] = plan;
		return new(plan);
	}

	public ref T Get<T>(Entity entity) where T : struct
	{
		ThrowIfDisposed();
		EnsureAlive(entity);
		int typeId = GetOrCreateComponentTypeId<T>();
		ref var component = ref GetComponentRefUnchecked<T>(entity.Id, typeId);
		TrackPotentialSingleRefWrite(entity.Id, typeId);
		return ref component;
	}

	public ref T GetResource<T>() where T : notnull
	{
		ThrowIfDisposed();
		if (!_resources.TryGetValue(typeof(T), out object? boxed))
			throw new KeyNotFoundException($"Resource of type {typeof(T).Name} was not found.");

		return ref ((ResourceBox<T>)boxed).Value;
	}

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
		int relationTypeId = GetOrCreateRelationTypeId(typeof(TRelation), target);
		var marker = default(RelationMarker);
		SetComponentInternal(source.Id, relationTypeId, in marker);
	}

	public void AddSystem(ISystem system, Stage stage = Stage.Tick)
	{
		ThrowIfDisposed();
		_systemManager.RegisterSystem(this, system, stage);
	}

	public void AddSystem<TSystem>(Stage stage = Stage.Tick)
		where TSystem : ISystem, new() =>
		AddSystem(new TSystem(), stage);

	public bool HasRelation<TRelation>(Entity source, Entity target)
		where TRelation : struct
	{
		ThrowIfDisposed();
		if (!IsAliveUnchecked(source))
			return false;

		if (target == Entity.None)
			return false;

		var sourceLocation = _locationByEntityId[source.Id];
		if (!sourceLocation.IsValid)
			return false;

		var sourceArchetype = _archetypes[sourceLocation.ArchetypeId];
		if (target == Entity.Wildcard)
		{
			int[] relationTypeIds = GetRelationTypeIds(typeof(TRelation));
			for (var i = 0; i < relationTypeIds.Length; i++)
			{
				if (sourceArchetype.HasType(relationTypeIds[i]))
					return true;
			}

			return false;
		}

		if (!IsAliveUnchecked(target))
			return false;

		return TryGetRelationTypeId(typeof(TRelation), target, out int relationTypeId) &&
			   sourceArchetype.HasType(relationTypeId);
	}

	public bool IsSystemSetEnabled<TSet>()
	{
		ThrowIfDisposed();
		return _systemManager.IsSystemSetEnabled(typeof(TSet));
	}

	public void SetSystemSetEnabled<TSet>(bool enabled)
	{
		ThrowIfDisposed();
		_systemManager.SetSystemSetEnabled(typeof(TSet), enabled);
	}

	public void SetSystemSetRunCondition<TSet>(ISystemRunCondition runCondition)
	{
		ThrowIfDisposed();
		if (runCondition is null)
			throw new ArgumentNullException(nameof(runCondition));

		_systemManager.SetSystemSetRunCondition(typeof(TSet), runCondition);
	}

	public void ClearSystemSetRunCondition<TSet>()
	{
		ThrowIfDisposed();
		_systemManager.ClearSystemSetRunCondition(typeof(TSet));
	}

	public void Clear()
	{
		ThrowIfDisposed();
		Reset();
		_resources.Clear();
	}

	public void Despawn(Entity entity)
	{
		ThrowIfDisposed();
		DestroyEntityInternal(entity);
	}

	public bool RemoveRelation<TRelation>(Entity source, Entity target)
		where TRelation : struct
	{
		ThrowIfDisposed();
		if (!IsAliveUnchecked(source))
			return false;

		var sourceLocation = _locationByEntityId[source.Id];
		if (!sourceLocation.IsValid)
			return false;

		var sourceArchetype = _archetypes[sourceLocation.ArchetypeId];
		if (target == Entity.Wildcard)
		{
			var removedAny        = false;
			int[] relationTypeIds = GetRelationTypeIds(typeof(TRelation));
			for (var i = 0; i < relationTypeIds.Length; i++)
			{
				int relationTypeId = relationTypeIds[i];
				sourceLocation = _locationByEntityId[source.Id];
				if (!sourceLocation.IsValid)
					return removedAny;

				sourceArchetype = _archetypes[sourceLocation.ArchetypeId];
				if (!sourceArchetype.HasType(relationTypeId))
					continue;

				RemoveComponentFromCommand(source, relationTypeId);
				removedAny = true;
			}

			return removedAny;
		}

		if (target == Entity.None)
			return false;

		if (!TryGetRelationTypeId(typeof(TRelation), target, out int relationId))
			return false;

		if (!sourceArchetype.HasType(relationId))
			return false;

		RemoveComponentFromCommand(source, relationId);
		return true;
	}

	public void Dispose()
	{
		if (_disposed)
			return;

		_systemManager.Shutdown(this);

		for (var i = 0; i < _archetypes.Count; i++)
			_archetypes[i].Dispose();

		_typeToId.Clear();
		_compiledPlansBySpecType.Clear();
		_relationTypeIdByKey.Clear();
		_relationTypeIdsByTarget.Clear();
		_relationTypeIdsByRelationType.Clear();
		_relationInfoByTypeId.Clear();
		_archetypeByTypeSet.Clear();
		_archetypes.Clear();
		DisposeResources();
		_disposed = true;
	}

	public void FixedTick(float deltaTime) => RunPhase(SystemLoopPhase.FixedTick, deltaTime);

	public void ForEach<TSpec, T1>(QueryHandle<TSpec> handle, QueryCursor.RefAction<T1> action)
		where TSpec : struct, ICompiledQuerySpec
		where T1 : unmanaged
	{
		if (action is null) throw new ArgumentNullException(nameof(action));

		ThrowIfDisposed();
		ThrowIfDirectIterationUnavailable(handle);
		int entityCount = FillQueryResults(handle.Plan, out int chunkMatchCount);
		if (entityCount == 0)
			return;

		int               typeId1            = GetOrCreateComponentTypeId<T1>();
		TrackPotentialQueryChunkMatchRefWrites(chunkMatchCount, typeId1);
		int               cachedArchetypeId  = int.MinValue;
		ArchetypeStorage? cachedArchetype    = null;
		int               cachedColumnIndex1 = -1;
		for (var i = 0; i < chunkMatchCount; i++)
		{
			var match = _queryChunkMatches[i];
			if (match.ArchetypeId != cachedArchetypeId)
			{
				cachedArchetypeId  = match.ArchetypeId;
				cachedArchetype    = _archetypes[match.ArchetypeId];
				cachedColumnIndex1 = cachedArchetype.GetColumnIndexOrNegative(typeId1);
				if (cachedColumnIndex1 < 0)
					throw new KeyNotFoundException(
						$"Type id '{typeId1}' does not exist in archetype '{match.ArchetypeId}'."
					);
			}

			if (match.Count == 0)
				continue;

			var     chunk   = cachedArchetype!.GetChunkUnchecked(match.ChunkIndex);
			ref var c1Start = ref cachedArchetype.GetRefByIndex<T1>(chunk, cachedColumnIndex1, match.RowStart);
			for (var offset = 0; offset < match.Count; offset++)
				action(ref Unsafe.Add(ref c1Start, offset));
		}
	}

	public void ForEach<TSpec, T1, T2>(QueryHandle<TSpec> handle, QueryCursor.RefInAction<T1, T2> action)
		where TSpec : struct, ICompiledQuerySpec
		where T1 : unmanaged
		where T2 : unmanaged
	{
		if (action is null) throw new ArgumentNullException(nameof(action));

		ThrowIfDisposed();
		ThrowIfDirectIterationUnavailable(handle);
		int entityCount = FillQueryResults(handle.Plan, out int chunkMatchCount);
		if (entityCount == 0)
			return;

		int               typeId1            = GetOrCreateComponentTypeId<T1>();
		TrackPotentialQueryChunkMatchRefWrites(chunkMatchCount, typeId1);
		int               typeId2            = GetOrCreateComponentTypeId<T2>();
		int               cachedArchetypeId  = int.MinValue;
		ArchetypeStorage? cachedArchetype    = null;
		int               cachedColumnIndex1 = -1;
		int               cachedColumnIndex2 = -1;
		for (var i = 0; i < chunkMatchCount; i++)
		{
			var match = _queryChunkMatches[i];
			if (match.ArchetypeId != cachedArchetypeId)
			{
				cachedArchetypeId  = match.ArchetypeId;
				cachedArchetype    = _archetypes[match.ArchetypeId];
				cachedColumnIndex1 = cachedArchetype.GetColumnIndexOrNegative(typeId1);
				if (cachedColumnIndex1 < 0)
					throw new KeyNotFoundException(
						$"Type id '{typeId1}' does not exist in archetype '{match.ArchetypeId}'."
					);

				cachedColumnIndex2 = cachedArchetype.GetColumnIndexOrNegative(typeId2);
				if (cachedColumnIndex2 < 0)
					throw new KeyNotFoundException(
						$"Type id '{typeId2}' does not exist in archetype '{match.ArchetypeId}'."
					);
			}

			if (match.Count == 0)
				continue;

			var     chunk   = cachedArchetype!.GetChunkUnchecked(match.ChunkIndex);
			ref var c1Start = ref cachedArchetype.GetRefByIndex<T1>(chunk, cachedColumnIndex1, match.RowStart);
			ref var c2Start = ref cachedArchetype.GetRefByIndex<T2>(chunk, cachedColumnIndex2, match.RowStart);
			for (var offset = 0; offset < match.Count; offset++)
			{
				ref var c1 = ref Unsafe.Add(ref c1Start, offset);
				ref var c2 = ref Unsafe.Add(ref c2Start, offset);
				action(ref c1, in c2);
			}
		}
	}

	public void ForEach<TSpec, T1, T2, T3>(QueryHandle<TSpec> handle, QueryCursor.RefInAction<T1, T2, T3> action)
		where TSpec : struct, ICompiledQuerySpec
		where T1 : unmanaged
		where T2 : unmanaged
		where T3 : unmanaged
	{
		if (action is null) throw new ArgumentNullException(nameof(action));

		ThrowIfDisposed();
		ThrowIfDirectIterationUnavailable(handle);
		int entityCount = FillQueryResults(handle.Plan, out int chunkMatchCount);
		if (entityCount == 0)
			return;

		int               typeId1            = GetOrCreateComponentTypeId<T1>();
		TrackPotentialQueryChunkMatchRefWrites(chunkMatchCount, typeId1);
		int               typeId2            = GetOrCreateComponentTypeId<T2>();
		int               typeId3            = GetOrCreateComponentTypeId<T3>();
		int               cachedArchetypeId  = int.MinValue;
		ArchetypeStorage? cachedArchetype    = null;
		int               cachedColumnIndex1 = -1;
		int               cachedColumnIndex2 = -1;
		int               cachedColumnIndex3 = -1;
		for (var i = 0; i < chunkMatchCount; i++)
		{
			var match = _queryChunkMatches[i];
			if (match.ArchetypeId != cachedArchetypeId)
			{
				cachedArchetypeId  = match.ArchetypeId;
				cachedArchetype    = _archetypes[match.ArchetypeId];
				cachedColumnIndex1 = cachedArchetype.GetColumnIndexOrNegative(typeId1);
				if (cachedColumnIndex1 < 0)
					throw new KeyNotFoundException(
						$"Type id '{typeId1}' does not exist in archetype '{match.ArchetypeId}'."
					);

				cachedColumnIndex2 = cachedArchetype.GetColumnIndexOrNegative(typeId2);
				if (cachedColumnIndex2 < 0)
					throw new KeyNotFoundException(
						$"Type id '{typeId2}' does not exist in archetype '{match.ArchetypeId}'."
					);

				cachedColumnIndex3 = cachedArchetype.GetColumnIndexOrNegative(typeId3);
				if (cachedColumnIndex3 < 0)
					throw new KeyNotFoundException(
						$"Type id '{typeId3}' does not exist in archetype '{match.ArchetypeId}'."
					);
			}

			if (match.Count == 0)
				continue;

			var     chunk   = cachedArchetype!.GetChunkUnchecked(match.ChunkIndex);
			ref var c1Start = ref cachedArchetype.GetRefByIndex<T1>(chunk, cachedColumnIndex1, match.RowStart);
			ref var c2Start = ref cachedArchetype.GetRefByIndex<T2>(chunk, cachedColumnIndex2, match.RowStart);
			ref var c3Start = ref cachedArchetype.GetRefByIndex<T3>(chunk, cachedColumnIndex3, match.RowStart);
			for (var offset = 0; offset < match.Count; offset++)
			{
				ref var c1 = ref Unsafe.Add(ref c1Start, offset);
				ref var c2 = ref Unsafe.Add(ref c2Start, offset);
				ref var c3 = ref Unsafe.Add(ref c3Start, offset);
				action(ref c1, in c2, in c3);
			}
		}
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
		QueryHandle<TSpec>                         handle,
		QueryCursor.RefInAction<T1, T2, T3, T4> action)
		where TSpec : struct, ICompiledQuerySpec
		where T1 : unmanaged
		where T2 : unmanaged
		where T3 : unmanaged
		where T4 : unmanaged
	{
		if (action is null) throw new ArgumentNullException(nameof(action));

		ThrowIfDisposed();
		ThrowIfDirectIterationUnavailable(handle);
		int entityCount = FillQueryResults(handle.Plan, out int chunkMatchCount);
		if (entityCount == 0)
			return;

		int               typeId1            = GetOrCreateComponentTypeId<T1>();
		TrackPotentialQueryChunkMatchRefWrites(chunkMatchCount, typeId1);
		int               typeId2            = GetOrCreateComponentTypeId<T2>();
		int               typeId3            = GetOrCreateComponentTypeId<T3>();
		int               typeId4            = GetOrCreateComponentTypeId<T4>();
		int               cachedArchetypeId  = int.MinValue;
		ArchetypeStorage? cachedArchetype    = null;
		int               cachedColumnIndex1 = -1;
		int               cachedColumnIndex2 = -1;
		int               cachedColumnIndex3 = -1;
		int               cachedColumnIndex4 = -1;
		for (var i = 0; i < chunkMatchCount; i++)
		{
			var match = _queryChunkMatches[i];
			if (match.ArchetypeId != cachedArchetypeId)
			{
				cachedArchetypeId  = match.ArchetypeId;
				cachedArchetype    = _archetypes[match.ArchetypeId];
				cachedColumnIndex1 = cachedArchetype.GetColumnIndexOrNegative(typeId1);
				if (cachedColumnIndex1 < 0)
					throw new KeyNotFoundException(
						$"Type id '{typeId1}' does not exist in archetype '{match.ArchetypeId}'."
					);

				cachedColumnIndex2 = cachedArchetype.GetColumnIndexOrNegative(typeId2);
				if (cachedColumnIndex2 < 0)
					throw new KeyNotFoundException(
						$"Type id '{typeId2}' does not exist in archetype '{match.ArchetypeId}'."
					);

				cachedColumnIndex3 = cachedArchetype.GetColumnIndexOrNegative(typeId3);
				if (cachedColumnIndex3 < 0)
					throw new KeyNotFoundException(
						$"Type id '{typeId3}' does not exist in archetype '{match.ArchetypeId}'."
					);

				cachedColumnIndex4 = cachedArchetype.GetColumnIndexOrNegative(typeId4);
				if (cachedColumnIndex4 < 0)
					throw new KeyNotFoundException(
						$"Type id '{typeId4}' does not exist in archetype '{match.ArchetypeId}'."
					);
			}

			if (match.Count == 0)
				continue;

			var     chunk   = cachedArchetype!.GetChunkUnchecked(match.ChunkIndex);
			ref var c1Start = ref cachedArchetype.GetRefByIndex<T1>(chunk, cachedColumnIndex1, match.RowStart);
			ref var c2Start = ref cachedArchetype.GetRefByIndex<T2>(chunk, cachedColumnIndex2, match.RowStart);
			ref var c3Start = ref cachedArchetype.GetRefByIndex<T3>(chunk, cachedColumnIndex3, match.RowStart);
			ref var c4Start = ref cachedArchetype.GetRefByIndex<T4>(chunk, cachedColumnIndex4, match.RowStart);
			for (var offset = 0; offset < match.Count; offset++)
			{
				ref var c1 = ref Unsafe.Add(ref c1Start, offset);
				ref var c2 = ref Unsafe.Add(ref c2Start, offset);
				ref var c3 = ref Unsafe.Add(ref c3Start, offset);
				ref var c4 = ref Unsafe.Add(ref c4Start, offset);
				action(ref c1, in c2, in c3, in c4);
			}
		}
	}

	public void LateTick(float deltaTime) => RunPhase(SystemLoopPhase.LateTick, deltaTime);

	public void Playback(CommandStream stream)
	{
		ThrowIfDisposed();
		if (stream is null) throw new ArgumentNullException(nameof(stream));
		if (!ReferenceEquals(stream.Owner, this))
			throw new InvalidOperationException("Command stream belongs to a different world.");

		if (_activeQueryCursors > 0)
			throw new InvalidOperationException("Playback cannot run while a query cursor is active.");

		stream.PlaybackInternal();
	}

	public void Remove<T>(Entity entity) where T : struct
	{
		ThrowIfDisposed();
		int typeId = GetOrCreateComponentTypeId<T>();
		RemoveComponentFromCommand(entity, typeId);
	}

	public void Reset()
	{
		ThrowIfDisposed();
		for (var entityId = 0; entityId < _nextEntityId; entityId++)
		{
			_aliveByEntityId[entityId] = false;
			_versionByEntityId[entityId]++;
			_locationByEntityId[entityId] = EntityLocation.Invalid;
		}

		for (var i = 0; i < _archetypes.Count; i++)
			_archetypes[i].Clear();

		_aliveCount         = 0;
		_freeCount          = 0;
		_nextEntityId       = 0;
		_activeQueryCursors = 0;
		_addedVersionByEntityType.Clear();
		_changeVersionByEntityType.Clear();
		_relationTypeIdByKey.Clear();
		_relationTypeIdsByTarget.Clear();
		_relationTypeIdsByRelationType.Clear();
		_relationInfoByTypeId.Clear();
	}

	public void Run<TSpec, TJob, T1>(QueryHandle<TSpec> handle, TJob job)
		where TSpec : struct, ICompiledQuerySpec
		where TJob : struct, IForEach<T1>
		where T1 : unmanaged
	{
		ThrowIfDisposed();
		ThrowIfDirectIterationUnavailable(handle);
		int entityCount = FillQueryResults(handle.Plan, out int chunkMatchCount);
		if (entityCount == 0)
			return;

		int               typeId1            = GetOrCreateComponentTypeId<T1>();
		TrackPotentialQueryChunkMatchRefWrites(chunkMatchCount, typeId1);
		int               cachedArchetypeId  = int.MinValue;
		ArchetypeStorage? cachedArchetype    = null;
		int               cachedColumnIndex1 = -1;
		for (var i = 0; i < chunkMatchCount; i++)
		{
			var match = _queryChunkMatches[i];
			if (match.ArchetypeId != cachedArchetypeId)
			{
				cachedArchetypeId  = match.ArchetypeId;
				cachedArchetype    = _archetypes[match.ArchetypeId];
				cachedColumnIndex1 = cachedArchetype.GetColumnIndexOrNegative(typeId1);
				if (cachedColumnIndex1 < 0)
					throw new KeyNotFoundException(
						$"Type id '{typeId1}' does not exist in archetype '{match.ArchetypeId}'."
					);
			}

			if (match.Count == 0)
				continue;

			var     chunk   = cachedArchetype!.GetChunkUnchecked(match.ChunkIndex);
			ref var c1Start = ref cachedArchetype.GetRefByIndex<T1>(chunk, cachedColumnIndex1, match.RowStart);
			for (var offset = 0; offset < match.Count; offset++)
				job.Execute(ref Unsafe.Add(ref c1Start, offset));
		}
	}

	public void Run<TSpec, TJob, T1, T2>(QueryHandle<TSpec> handle, TJob job)
		where TSpec : struct, ICompiledQuerySpec
		where TJob : struct, IForEach<T1, T2>
		where T1 : unmanaged
		where T2 : unmanaged
	{
		ThrowIfDisposed();
		ThrowIfDirectIterationUnavailable(handle);
		int entityCount = FillQueryResults(handle.Plan, out int chunkMatchCount);
		if (entityCount == 0)
			return;

		int               typeId1            = GetOrCreateComponentTypeId<T1>();
		TrackPotentialQueryChunkMatchRefWrites(chunkMatchCount, typeId1);
		int               typeId2            = GetOrCreateComponentTypeId<T2>();
		int               cachedArchetypeId  = int.MinValue;
		ArchetypeStorage? cachedArchetype    = null;
		int               cachedColumnIndex1 = -1;
		int               cachedColumnIndex2 = -1;
		for (var i = 0; i < chunkMatchCount; i++)
		{
			var match = _queryChunkMatches[i];
			if (match.ArchetypeId != cachedArchetypeId)
			{
				cachedArchetypeId  = match.ArchetypeId;
				cachedArchetype    = _archetypes[match.ArchetypeId];
				cachedColumnIndex1 = cachedArchetype.GetColumnIndexOrNegative(typeId1);
				if (cachedColumnIndex1 < 0)
					throw new KeyNotFoundException(
						$"Type id '{typeId1}' does not exist in archetype '{match.ArchetypeId}'."
					);

				cachedColumnIndex2 = cachedArchetype.GetColumnIndexOrNegative(typeId2);
				if (cachedColumnIndex2 < 0)
					throw new KeyNotFoundException(
						$"Type id '{typeId2}' does not exist in archetype '{match.ArchetypeId}'."
					);
			}

			if (match.Count == 0)
				continue;

			var     chunk   = cachedArchetype!.GetChunkUnchecked(match.ChunkIndex);
			ref var c1Start = ref cachedArchetype.GetRefByIndex<T1>(chunk, cachedColumnIndex1, match.RowStart);
			ref var c2Start = ref cachedArchetype.GetRefByIndex<T2>(chunk, cachedColumnIndex2, match.RowStart);
			for (var offset = 0; offset < match.Count; offset++)
			{
				ref var c1 = ref Unsafe.Add(ref c1Start, offset);
				ref var c2 = ref Unsafe.Add(ref c2Start, offset);
				job.Execute(ref c1, in c2);
			}
		}
	}

	public void Run<TSpec, TJob, T1, T2, T3>(QueryHandle<TSpec> handle, TJob job)
		where TSpec : struct, ICompiledQuerySpec
		where TJob : struct, IForEach<T1, T2, T3>
		where T1 : unmanaged
		where T2 : unmanaged
		where T3 : unmanaged
	{
		ThrowIfDisposed();
		ThrowIfDirectIterationUnavailable(handle);
		int entityCount = FillQueryResults(handle.Plan, out int chunkMatchCount);
		if (entityCount == 0)
			return;

		int               typeId1            = GetOrCreateComponentTypeId<T1>();
		TrackPotentialQueryChunkMatchRefWrites(chunkMatchCount, typeId1);
		int               typeId2            = GetOrCreateComponentTypeId<T2>();
		int               typeId3            = GetOrCreateComponentTypeId<T3>();
		int               cachedArchetypeId  = int.MinValue;
		ArchetypeStorage? cachedArchetype    = null;
		int               cachedColumnIndex1 = -1;
		int               cachedColumnIndex2 = -1;
		int               cachedColumnIndex3 = -1;
		for (var i = 0; i < chunkMatchCount; i++)
		{
			var match = _queryChunkMatches[i];
			if (match.ArchetypeId != cachedArchetypeId)
			{
				cachedArchetypeId  = match.ArchetypeId;
				cachedArchetype    = _archetypes[match.ArchetypeId];
				cachedColumnIndex1 = cachedArchetype.GetColumnIndexOrNegative(typeId1);
				if (cachedColumnIndex1 < 0)
					throw new KeyNotFoundException(
						$"Type id '{typeId1}' does not exist in archetype '{match.ArchetypeId}'."
					);

				cachedColumnIndex2 = cachedArchetype.GetColumnIndexOrNegative(typeId2);
				if (cachedColumnIndex2 < 0)
					throw new KeyNotFoundException(
						$"Type id '{typeId2}' does not exist in archetype '{match.ArchetypeId}'."
					);

				cachedColumnIndex3 = cachedArchetype.GetColumnIndexOrNegative(typeId3);
				if (cachedColumnIndex3 < 0)
					throw new KeyNotFoundException(
						$"Type id '{typeId3}' does not exist in archetype '{match.ArchetypeId}'."
					);
			}

			if (match.Count == 0)
				continue;

			var     chunk   = cachedArchetype!.GetChunkUnchecked(match.ChunkIndex);
			ref var c1Start = ref cachedArchetype.GetRefByIndex<T1>(chunk, cachedColumnIndex1, match.RowStart);
			ref var c2Start = ref cachedArchetype.GetRefByIndex<T2>(chunk, cachedColumnIndex2, match.RowStart);
			ref var c3Start = ref cachedArchetype.GetRefByIndex<T3>(chunk, cachedColumnIndex3, match.RowStart);
			for (var offset = 0; offset < match.Count; offset++)
			{
				ref var c1 = ref Unsafe.Add(ref c1Start, offset);
				ref var c2 = ref Unsafe.Add(ref c2Start, offset);
				ref var c3 = ref Unsafe.Add(ref c3Start, offset);
				job.Execute(ref c1, in c2, in c3);
			}
		}
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
		ThrowIfDirectIterationUnavailable(handle);
		int entityCount = FillQueryResults(handle.Plan, out int chunkMatchCount);
		if (entityCount == 0)
			return;

		int               typeId1            = GetOrCreateComponentTypeId<T1>();
		TrackPotentialQueryChunkMatchRefWrites(chunkMatchCount, typeId1);
		int               typeId2            = GetOrCreateComponentTypeId<T2>();
		int               typeId3            = GetOrCreateComponentTypeId<T3>();
		int               typeId4            = GetOrCreateComponentTypeId<T4>();
		int               cachedArchetypeId  = int.MinValue;
		ArchetypeStorage? cachedArchetype    = null;
		int               cachedColumnIndex1 = -1;
		int               cachedColumnIndex2 = -1;
		int               cachedColumnIndex3 = -1;
		int               cachedColumnIndex4 = -1;
		for (var i = 0; i < chunkMatchCount; i++)
		{
			var match = _queryChunkMatches[i];
			if (match.ArchetypeId != cachedArchetypeId)
			{
				cachedArchetypeId  = match.ArchetypeId;
				cachedArchetype    = _archetypes[match.ArchetypeId];
				cachedColumnIndex1 = cachedArchetype.GetColumnIndexOrNegative(typeId1);
				if (cachedColumnIndex1 < 0)
					throw new KeyNotFoundException(
						$"Type id '{typeId1}' does not exist in archetype '{match.ArchetypeId}'."
					);

				cachedColumnIndex2 = cachedArchetype.GetColumnIndexOrNegative(typeId2);
				if (cachedColumnIndex2 < 0)
					throw new KeyNotFoundException(
						$"Type id '{typeId2}' does not exist in archetype '{match.ArchetypeId}'."
					);

				cachedColumnIndex3 = cachedArchetype.GetColumnIndexOrNegative(typeId3);
				if (cachedColumnIndex3 < 0)
					throw new KeyNotFoundException(
						$"Type id '{typeId3}' does not exist in archetype '{match.ArchetypeId}'."
					);

				cachedColumnIndex4 = cachedArchetype.GetColumnIndexOrNegative(typeId4);
				if (cachedColumnIndex4 < 0)
					throw new KeyNotFoundException(
						$"Type id '{typeId4}' does not exist in archetype '{match.ArchetypeId}'."
					);
			}

			if (match.Count == 0)
				continue;

			var     chunk   = cachedArchetype!.GetChunkUnchecked(match.ChunkIndex);
			ref var c1Start = ref cachedArchetype.GetRefByIndex<T1>(chunk, cachedColumnIndex1, match.RowStart);
			ref var c2Start = ref cachedArchetype.GetRefByIndex<T2>(chunk, cachedColumnIndex2, match.RowStart);
			ref var c3Start = ref cachedArchetype.GetRefByIndex<T3>(chunk, cachedColumnIndex3, match.RowStart);
			ref var c4Start = ref cachedArchetype.GetRefByIndex<T4>(chunk, cachedColumnIndex4, match.RowStart);
			for (var offset = 0; offset < match.Count; offset++)
			{
				ref var c1 = ref Unsafe.Add(ref c1Start, offset);
				ref var c2 = ref Unsafe.Add(ref c2Start, offset);
				ref var c3 = ref Unsafe.Add(ref c3Start, offset);
				ref var c4 = ref Unsafe.Add(ref c4Start, offset);
				job.Execute(ref c1, in c2, in c3, in c4);
			}
		}
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
		ThrowIfDirectIterationUnavailable(handle);
		int parallelism = ResolveDegreeOfParallelism(degreeOfParallelism);
		int entityCount = FillQueryResults(handle.Plan, out int chunkMatchCount);
		if (entityCount == 0)
			return;

		int typeId1 = GetOrCreateComponentTypeId<T1>();
		TrackPotentialQueryChunkMatchRefWrites(chunkMatchCount, typeId1);
		Bezoro.ECS.Internal.ParallelWorkScheduler.Execute(
			chunkMatchCount,
			parallelism,
			index =>
			{
				var match = _queryChunkMatches[index];
				if (match.Count == 0)
					return;

				var archetype = _archetypes[match.ArchetypeId];
				int columnIndex1 = archetype.GetColumnIndexOrNegative(typeId1);
				if (columnIndex1 < 0)
					throw new KeyNotFoundException(
						$"Type id '{typeId1}' does not exist in archetype '{match.ArchetypeId}'."
					);

				var     chunk   = archetype.GetChunkUnchecked(match.ChunkIndex);
				ref var c1Start = ref archetype.GetRefByIndex<T1>(chunk, columnIndex1, match.RowStart);
				var     local   = job;
				for (var offset = 0; offset < match.Count; offset++)
					local.Execute(ref Unsafe.Add(ref c1Start, offset));
			}
		);
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
		ThrowIfDirectIterationUnavailable(handle);
		int parallelism = ResolveDegreeOfParallelism(degreeOfParallelism);
		int entityCount = FillQueryResults(handle.Plan, out int chunkMatchCount);
		if (entityCount == 0)
			return;

		int typeId1 = GetOrCreateComponentTypeId<T1>();
		TrackPotentialQueryChunkMatchRefWrites(chunkMatchCount, typeId1);
		int typeId2 = GetOrCreateComponentTypeId<T2>();
		Bezoro.ECS.Internal.ParallelWorkScheduler.Execute(
			chunkMatchCount,
			parallelism,
			index =>
			{
				var match = _queryChunkMatches[index];
				if (match.Count == 0)
					return;

				var archetype = _archetypes[match.ArchetypeId];
				int columnIndex1 = archetype.GetColumnIndexOrNegative(typeId1);
				if (columnIndex1 < 0)
					throw new KeyNotFoundException(
						$"Type id '{typeId1}' does not exist in archetype '{match.ArchetypeId}'."
					);

				int columnIndex2 = archetype.GetColumnIndexOrNegative(typeId2);
				if (columnIndex2 < 0)
					throw new KeyNotFoundException(
						$"Type id '{typeId2}' does not exist in archetype '{match.ArchetypeId}'."
					);

				var     chunk   = archetype.GetChunkUnchecked(match.ChunkIndex);
				ref var c1Start = ref archetype.GetRefByIndex<T1>(chunk, columnIndex1, match.RowStart);
				ref var c2Start = ref archetype.GetRefByIndex<T2>(chunk, columnIndex2, match.RowStart);
				var     local   = job;
				for (var offset = 0; offset < match.Count; offset++)
				{
					ref var c1 = ref Unsafe.Add(ref c1Start, offset);
					ref var c2 = ref Unsafe.Add(ref c2Start, offset);
					local.Execute(ref c1, in c2);
				}
			}
		);
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
		ThrowIfDirectIterationUnavailable(handle);
		int parallelism = ResolveDegreeOfParallelism(degreeOfParallelism);
		int entityCount = FillQueryResults(handle.Plan, out int chunkMatchCount);
		if (entityCount == 0)
			return;

		int typeId1 = GetOrCreateComponentTypeId<T1>();
		TrackPotentialQueryChunkMatchRefWrites(chunkMatchCount, typeId1);
		int typeId2 = GetOrCreateComponentTypeId<T2>();
		int typeId3 = GetOrCreateComponentTypeId<T3>();
		Bezoro.ECS.Internal.ParallelWorkScheduler.Execute(
			chunkMatchCount,
			parallelism,
			index =>
			{
				var match = _queryChunkMatches[index];
				if (match.Count == 0)
					return;

				var archetype = _archetypes[match.ArchetypeId];
				int columnIndex1 = archetype.GetColumnIndexOrNegative(typeId1);
				if (columnIndex1 < 0)
					throw new KeyNotFoundException(
						$"Type id '{typeId1}' does not exist in archetype '{match.ArchetypeId}'."
					);

				int columnIndex2 = archetype.GetColumnIndexOrNegative(typeId2);
				if (columnIndex2 < 0)
					throw new KeyNotFoundException(
						$"Type id '{typeId2}' does not exist in archetype '{match.ArchetypeId}'."
					);

				int columnIndex3 = archetype.GetColumnIndexOrNegative(typeId3);
				if (columnIndex3 < 0)
					throw new KeyNotFoundException(
						$"Type id '{typeId3}' does not exist in archetype '{match.ArchetypeId}'."
					);

				var     chunk   = archetype.GetChunkUnchecked(match.ChunkIndex);
				ref var c1Start = ref archetype.GetRefByIndex<T1>(chunk, columnIndex1, match.RowStart);
				ref var c2Start = ref archetype.GetRefByIndex<T2>(chunk, columnIndex2, match.RowStart);
				ref var c3Start = ref archetype.GetRefByIndex<T3>(chunk, columnIndex3, match.RowStart);
				var     local   = job;
				for (var offset = 0; offset < match.Count; offset++)
				{
					ref var c1 = ref Unsafe.Add(ref c1Start, offset);
					ref var c2 = ref Unsafe.Add(ref c2Start, offset);
					ref var c3 = ref Unsafe.Add(ref c3Start, offset);
					local.Execute(ref c1, in c2, in c3);
				}
			}
		);
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
		ThrowIfDirectIterationUnavailable(handle);
		int parallelism = ResolveDegreeOfParallelism(degreeOfParallelism);
		int entityCount = FillQueryResults(handle.Plan, out int chunkMatchCount);
		if (entityCount == 0)
			return;

		int typeId1 = GetOrCreateComponentTypeId<T1>();
		TrackPotentialQueryChunkMatchRefWrites(chunkMatchCount, typeId1);
		int typeId2 = GetOrCreateComponentTypeId<T2>();
		int typeId3 = GetOrCreateComponentTypeId<T3>();
		int typeId4 = GetOrCreateComponentTypeId<T4>();
		Bezoro.ECS.Internal.ParallelWorkScheduler.Execute(
			chunkMatchCount,
			parallelism,
			index =>
			{
				var match = _queryChunkMatches[index];
				if (match.Count == 0)
					return;

				var archetype = _archetypes[match.ArchetypeId];
				int columnIndex1 = archetype.GetColumnIndexOrNegative(typeId1);
				if (columnIndex1 < 0)
					throw new KeyNotFoundException(
						$"Type id '{typeId1}' does not exist in archetype '{match.ArchetypeId}'."
					);

				int columnIndex2 = archetype.GetColumnIndexOrNegative(typeId2);
				if (columnIndex2 < 0)
					throw new KeyNotFoundException(
						$"Type id '{typeId2}' does not exist in archetype '{match.ArchetypeId}'."
					);

				int columnIndex3 = archetype.GetColumnIndexOrNegative(typeId3);
				if (columnIndex3 < 0)
					throw new KeyNotFoundException(
						$"Type id '{typeId3}' does not exist in archetype '{match.ArchetypeId}'."
					);

				int columnIndex4 = archetype.GetColumnIndexOrNegative(typeId4);
				if (columnIndex4 < 0)
					throw new KeyNotFoundException(
						$"Type id '{typeId4}' does not exist in archetype '{match.ArchetypeId}'."
					);

				var     chunk   = archetype.GetChunkUnchecked(match.ChunkIndex);
				ref var c1Start = ref archetype.GetRefByIndex<T1>(chunk, columnIndex1, match.RowStart);
				ref var c2Start = ref archetype.GetRefByIndex<T2>(chunk, columnIndex2, match.RowStart);
				ref var c3Start = ref archetype.GetRefByIndex<T3>(chunk, columnIndex3, match.RowStart);
				ref var c4Start = ref archetype.GetRefByIndex<T4>(chunk, columnIndex4, match.RowStart);
				var     local   = job;
				for (var offset = 0; offset < match.Count; offset++)
				{
					ref var c1 = ref Unsafe.Add(ref c1Start, offset);
					ref var c2 = ref Unsafe.Add(ref c2Start, offset);
					ref var c3 = ref Unsafe.Add(ref c3Start, offset);
					ref var c4 = ref Unsafe.Add(ref c4Start, offset);
					local.Execute(ref c1, in c2, in c3, in c4);
				}
			}
		);
	}

	/// <summary>
	///     Captures a snapshot payload and forwards it to <paramref name="writer" />.
	/// </summary>
	/// <typeparam name="TSnapshotWriter">Writer type receiving the captured payload.</typeparam>
	/// <param name="writer">Snapshot writer receiving the captured payload.</param>
	public void CaptureSnapshot<TSnapshotWriter>(ref TSnapshotWriter writer)
		where TSnapshotWriter : struct, IWorldSnapshotWriter
	{
		ThrowIfDisposed();
		if (_activeQueryCursors > 0)
			throw new InvalidOperationException("Snapshot capture cannot run while a query cursor is active.");

		var resources = new List<SnapshotResourceRecord>(_resources.Count);
		foreach (object resourceBox in _resources.Values)
		{
			if (resourceBox is not IResourceBox box)
				continue;

			resources.Add(new(box.ResourceType, box.BoxedValue));
		}

		var entities = new List<SnapshotEntityRecord>(_aliveCount);
		var relations = new List<SnapshotRelationRecord>();
		for (var entityId = 0; entityId < _nextEntityId; entityId++)
		{
			if (!_aliveByEntityId[entityId])
				continue;

			var entity = new Entity(entityId, _versionByEntityId[entityId]);
			var location = _locationByEntityId[entityId];
			if (!location.IsValid)
				continue;

			var archetype = _archetypes[location.ArchetypeId];
			var chunk = archetype.GetChunkUnchecked(location.ChunkIndex);
			var components = new List<SnapshotComponentRecord>(archetype.TypeIds.Length);
			for (var typeIndex = 0; typeIndex < archetype.TypeIds.Length; typeIndex++)
			{
				int typeId = archetype.TypeIds[typeIndex];
				if (_relationInfoByTypeId.TryGetValue(typeId, out var relationInfo))
				{
					relations.Add(new SnapshotRelationRecord(relationInfo.RelationType, entity, relationInfo.Target));
					continue;
				}

				Type componentType = _typeById[typeId] ??
				                     throw new InvalidOperationException(
					                     $"Component type id '{typeId}' is not registered."
				                     );
				object component = chunk.Columns[typeIndex].GetValue(location.RowIndex);
				components.Add(new(componentType, component));
			}

			entities.Add(new(entity, components.ToArray()));
		}

		var snapshot = new WorldSnapshot(resources.ToArray(), entities.ToArray(), relations.ToArray());
		writer.Write(in snapshot);
	}

	/// <summary>
	///     Restores world state from snapshot payload supplied by <paramref name="reader" />.
	/// </summary>
	/// <typeparam name="TSnapshotReader">Reader type providing the snapshot payload.</typeparam>
	/// <param name="reader">Snapshot reader providing restore payload.</param>
	/// <param name="options">Optional type-allowlist and validation options.</param>
	public void RestoreSnapshot<TSnapshotReader>(
		ref TSnapshotReader              reader,
		SnapshotDeserializationOptions? options = null)
		where TSnapshotReader : struct, IWorldSnapshotReader
	{
		ThrowIfDisposed();
		if (_activeQueryCursors > 0)
			throw new InvalidOperationException("Snapshot restore cannot run while a query cursor is active.");

		options ??= SnapshotDeserializationOptions.Default;
		var snapshot = reader.Read() ?? throw new InvalidOperationException("Snapshot reader returned null payload.");
		// Validate the entire payload first so rejected snapshots never mutate the current world.
		var restorePlan = ValidateSnapshotRestorePlan(snapshot, options);
		try
		{
			Clear();
			ApplySnapshotRestorePlan(restorePlan);
		}
		catch
		{
			Clear();
			throw;
		}
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
		var plan = handle.Plan;
		int matchingArchetypeCount = GetOrRefreshMatchingArchetypes(plan);
		int matchingEntityCount = FillQueryResults(plan, out int matchingChunkCount, false);
		return new(
			matchingArchetypeCount,
			matchingChunkCount,
			matchingEntityCount,
			plan.ArchetypeCacheVersion,
			plan.ArchetypeCacheVersion == _archetypeVersion,
			ResolveTypes(plan.AllTypeIds),
			ResolveTypes(plan.AnyTypeIds),
			ResolveTypes(plan.NoneTypeIds),
			ResolveTypes(plan.OptionalTypeIds),
			ResolveTypes(plan.AddedTypeIds),
			ResolveTypes(plan.ChangedTypeIds),
			plan.RelatedRelationType,
			plan.RelatedTarget
		);
	}

	public void RunPhase(SystemLoopPhase loopPhase, float deltaTime)
	{
		ThrowIfDisposed();
		if (Interlocked.Increment(ref _updateDepth) != 1)
		{
			Interlocked.Decrement(ref _updateDepth);
			throw new InvalidOperationException("Re-entrant world updates are not supported.");
		}

		try
		{
			_systemManager.UpdatePhase(this, loopPhase, deltaTime);
		}
		finally
		{
			Interlocked.Decrement(ref _updateDepth);
		}
	}

	public void Set<T>(Entity entity, in T component) where T : struct
	{
		ThrowIfDisposed();
		ApplySetFromCommand(entity, in component);
	}

	public void SetResource<T>(T resource) where T : notnull
	{
		ThrowIfDisposed();
		if (_resources.TryGetValue(typeof(T), out object? existing) && existing is IResourceBox existingBox)
			existingBox.DisposeValue();

		_resources[typeof(T)] = new ResourceBox<T>(resource);
	}

	public void Tick(float deltaTime) => RunPhase(SystemLoopPhase.Tick, deltaTime);

	public WorldDiagnostics GetDiagnostics()
	{
		ThrowIfDisposed();
		return new(
			new("Entities", _config.EntityCapacity, _aliveCount, _entityHighWatermark, _entityOverflowCount),
			new(
				"ComponentTypes",
				_config.ComponentTypeCapacity,
				_typeCount,
				_registeredTypeHighWatermark,
				_componentTypeOverflowCount
			),
			new("QueryResults", _config.QueryResultCapacity, 0, _queryHighWatermark, _queryOverflowCount)
		);
	}

	public ScheduleDiagnostics GetScheduleDiagnostics()
	{
		ThrowIfDisposed();
		return _systemManager.GetDiagnostics();
	}

	internal ArchetypeStorage GetArchetypeForCursor(int archetypeId)
	{
		if ((uint)archetypeId >= (uint)_archetypes.Count)
			throw new ArgumentOutOfRangeException(nameof(archetypeId));

		return _archetypes[archetypeId];
	}

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

		var location = _locationByEntityId[entity.Id];
		if (!location.IsValid)
			return false;

		var archetype = _archetypes[location.ArchetypeId];
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
		var location = _locationByEntityId[entity.Id];
		if (!location.IsValid)
			throw new InvalidOperationException($"Entity '{entity.Id}' is not in a valid archetype.");

		if (location.ArchetypeId != sourceArchetypeId)
			return false;

		var sourceArchetype = _archetypes[sourceArchetypeId];
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
		var location = _locationByEntityId[entity.Id];
		if (!location.IsValid)
			throw new InvalidOperationException($"Entity '{entity.Id}' is not in a valid archetype.");

		if (location.ArchetypeId != sourceArchetypeId)
			return false;

		var sourceArchetype = _archetypes[sourceArchetypeId];
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

		var location = _locationByEntityId[entity.Id];
		if (!location.IsValid)
		{
			component = default;
			return false;
		}

		var archetype = _archetypes[location.ArchetypeId];
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
	{
		int id;
		if (_freeCount > 0)
		{
			id = _freeEntityIds[--_freeCount];
		}
		else
		{
			if (_nextEntityId >= _config.EntityCapacity)
			{
				_entityOverflowCount++;
				throw new InvalidOperationException(
					$"Entity capacity '{_config.EntityCapacity}' was exceeded."
				);
			}

			id = _nextEntityId++;
		}

		_aliveByEntityId[id] = true;
		_aliveCount++;
		if (_aliveCount > _entityHighWatermark)
			_entityHighWatermark = _aliveCount;

		var emptyArchetype = _archetypes[_emptyArchetypeId];
		emptyArchetype.AllocateRow(id, out int chunkIndex, out int rowIndex);
		_locationByEntityId[id] = new(_emptyArchetypeId, chunkIndex, rowIndex);

		return new(id, _versionByEntityId[id]);
	}

	internal int GetArchetypeEntityCount(int archetypeId)
	{
		if ((uint)archetypeId >= (uint)_archetypes.Count)
			throw new ArgumentOutOfRangeException(nameof(archetypeId));

		return _archetypes[archetypeId].EntityCount;
	}

	internal int GetOrCreateComponentTypeId<T>() where T : struct
	{
		if (Volatile.Read(ref ComponentTypeIdCache<T>.OwnerInstanceId) == _instanceId)
			return ComponentTypeIdCache<T>.TypeId;

		int typeId = GetOrCreateComponentTypeIdGeneric<T>();
		ComponentTypeIdCache<T>.TypeId = typeId;
		Volatile.Write(ref ComponentTypeIdCache<T>.OwnerInstanceId, _instanceId);
		return typeId;
	}

	internal int GetOrCreateComponentTypeId(Type componentType)
	{
		if (componentType is null) throw new ArgumentNullException(nameof(componentType));
		if (!componentType.IsValueType)
			throw new ArgumentException("Components must be value types.", nameof(componentType));

		if (_typeToId.TryGetValue(componentType, out int existing))
			return existing;

		if (_typeCount >= _typeById.Length)
		{
			_componentTypeOverflowCount++;
			throw new InvalidOperationException(
				$"Component type capacity '{_config.ComponentTypeCapacity}' was exceeded."
			);
		}

		int typeId = _typeCount++;
		_typeToId[componentType]   = typeId;
		_typeById[typeId]          = componentType;
		_typeIsManagedLane[typeId] = ContainsReferences(componentType);
		if (_typeCount > _registeredTypeHighWatermark)
			_registeredTypeHighWatermark = _typeCount;

		return typeId;
	}

	internal int ResolveEntityIdForCursorIndex(
		QueryChunkMatch[] chunkMatches,
		int               chunkMatchCount,
		int               index,
		ref int           chunkMatchHint)
	{
		if ((uint)index >= (uint)_queryEntities.Length)
			throw new ArgumentOutOfRangeException(nameof(index));

		if ((uint)chunkMatchHint < (uint)chunkMatchCount)
		{
			if (TryResolveEntityIdFromChunkMatch(chunkMatches[chunkMatchHint], index, out int hintedEntityId))
				return hintedEntityId;

			int nextHint = chunkMatchHint + 1;
			if ((uint)nextHint < (uint)chunkMatchCount &&
				TryResolveEntityIdFromChunkMatch(chunkMatches[nextHint], index, out int nextEntityId))
			{
				chunkMatchHint = nextHint;
				return nextEntityId;
			}

			int previousHint = chunkMatchHint - 1;
			if (previousHint >= 0 &&
				TryResolveEntityIdFromChunkMatch(chunkMatches[previousHint], index, out int previousEntityId))
			{
				chunkMatchHint = previousHint;
				return previousEntityId;
			}
		}

		var low  = 0;
		int high = chunkMatchCount - 1;
		while (low <= high)
		{
			int middle = low + (high - low >> 1);
			var match  = chunkMatches[middle];
			int start  = match.EntityStartIndex;
			int offset = index - start;
			if (offset < 0)
			{
				high = middle - 1;
				continue;
			}

			if ((uint)offset >= (uint)match.Count)
			{
				low = middle + 1;
				continue;
			}

			chunkMatchHint = middle;
			var chunk = _archetypes[match.ArchetypeId].GetChunkUnchecked(match.ChunkIndex);
			return chunk.EntityIds[match.RowStart + offset];
		}

		throw new ArgumentOutOfRangeException(nameof(index));
	}

	internal int[] BuildMaskWordIndices(ulong[] maskWords)
	{
		var count = 0;
		for (var i = 0; i < maskWords.Length; i++)
		{
			if (maskWords[i] != 0UL)
				count++;
		}

		if (count == 0)
			return [];

		var indices = new int[count];
		var j       = 0;
		for (var i = 0; i < maskWords.Length; i++)
		{
			if (maskWords[i] == 0UL)
				continue;

			indices[j++] = i;
		}

		return indices;
	}

	internal ref T GetComponentRefForCursorUnchecked<T>(int entityId, int typeId) where T : struct
	{
		var location = _locationByEntityId[entityId];
		if (!location.IsValid)
			throw new InvalidOperationException($"Entity '{entityId}' is not in a valid archetype.");

		return ref _archetypes[location.ArchetypeId].GetRef<T>(location.ChunkIndex, location.RowIndex, typeId);
	}

	internal ulong[] BuildMaskWords(int[] typeIds)
	{
		var words = new ulong[_maskWordCount];
		for (var i = 0; i < typeIds.Length; i++)
		{
			int typeId    = typeIds[i];
			int wordIndex = typeId >> 6;
			int bitIndex  = typeId & 63;
			words[wordIndex] |= 1UL << bitIndex;
		}

		return words;
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
	{
		if (entityIds is null) throw new ArgumentNullException(nameof(entityIds));
		if (payloads is null) throw new ArgumentNullException(nameof(payloads));
		if (payloadIndices is null) throw new ArgumentNullException(nameof(payloadIndices));
		if (count < 0) throw new ArgumentOutOfRangeException(nameof(count));
		if (entityOffset < 0 || entityOffset + count > entityIds.Length)
			throw new ArgumentOutOfRangeException(nameof(entityOffset));

		if (payloadOffset < 0 || payloadOffset + count > payloadIndices.Length)
			throw new ArgumentOutOfRangeException(nameof(payloadOffset));

		var sourceArchetype = _archetypes[sourceArchetypeId];
		int sourceColumnIndex = -1;
		uint changeVersion = 0;
		if (targetArchetypeId == sourceArchetypeId)
		{
			sourceColumnIndex = sourceArchetype.GetColumnIndexOrNegative(typeId);
			if (sourceColumnIndex < 0)
				throw new InvalidOperationException(
					$"Type id '{typeId}' does not exist in archetype '{sourceArchetypeId}'."
				);

			changeVersion = AdvanceComponentChangeVersion();
		}

		for (var i = 0; i < count; i++)
		{
			int payloadIndex = payloadIndices[payloadOffset + i];
			if ((uint)payloadIndex >= (uint)payloadCount)
				throw new InvalidOperationException(
					$"Payload index '{payloadIndex}' is out of range for '{typeof(T).Name}'."
				);

			int              entityId  = entityIds[entityOffset + i];
			ref readonly var component = ref payloads[payloadIndex];
			var              location  = _locationByEntityId[entityId];
			if (!location.IsValid || location.ArchetypeId != sourceArchetypeId)
			{
				SetComponentInternal(entityId, typeId, in component);
				continue;
			}

			if (targetArchetypeId == sourceArchetypeId)
			{
				ref var existing = ref sourceArchetype.GetRef<T>(location.ChunkIndex, location.RowIndex, typeId);
				existing = component;
				var sourceChunk = sourceArchetype.GetChunkUnchecked(location.ChunkIndex);
				sourceArchetype.MarkComponentChanged(sourceChunk, sourceColumnIndex, changeVersion);
				MarkComponentChanged(entityId, typeId, changeVersion);
				continue;
			}

			MoveEntityToArchetypeWithSet(
				entityId,
				location,
				sourceArchetype,
				targetArchetypeId,
				typeId,
				in component
			);
		}
	}

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
	{
		if (count == 0)
			return;

		var sourceArchetype = _archetypes[sourceArchetypeId];
		if (targetArchetypeId == sourceArchetypeId)
		{
			uint changeVersion = AdvanceComponentChangeVersion();
			int sourceColumnIndex = sourceArchetype.GetColumnIndexOrNegative(typeId);
			if (sourceColumnIndex < 0)
				throw new InvalidOperationException(
					$"Type id '{typeId}' does not exist in archetype '{sourceArchetypeId}'."
				);

			int     cachedChunkIndex = -1;
			Span<T> cachedColumn     = default;
			for (var i = 0; i < count; i++)
			{
				int payloadIndex = payloadIndices[payloadOffset + i];
				int entityId     = entityIds[entityOffset + i];
				var location     = _locationByEntityId[entityId];
				if (location.ChunkIndex != cachedChunkIndex)
				{
					cachedChunkIndex = location.ChunkIndex;
					var sourceChunk = sourceArchetype.GetChunkUnchecked(cachedChunkIndex);
					cachedColumn = sourceArchetype.GetSpanByIndex<T>(sourceChunk, sourceColumnIndex);
					sourceArchetype.MarkComponentChanged(sourceChunk, sourceColumnIndex, changeVersion);
				}

				cachedColumn[location.RowIndex] = payloads[payloadIndex];
				MarkComponentChanged(entityId, typeId, changeVersion);
			}

			return;
		}

		var   targetArchetype         = _archetypes[targetArchetypeId];
		int[] sourceTargetColumnPairs = GetOrCreateTransitionCopyMap(sourceArchetype, targetArchetype);
		for (var i = 0; i < count; i++)
		{
			int payloadIndex = payloadIndices[payloadOffset + i];
			int entityId     = entityIds[entityOffset + i];
			var location     = _locationByEntityId[entityId];
			MoveEntityToArchetypeWithSet(
				entityId,
				location,
				sourceArchetype,
				targetArchetype,
				sourceTargetColumnPairs,
				typeId,
				in payloads[payloadIndex]
			);
		}
	}

	internal void ApplySetFromCommand<T>(Entity entity, in T component) where T : struct
	{
		EnsureAlive(entity);
		int typeId = GetOrCreateComponentTypeId<T>();
		SetComponentInternal(entity.Id, typeId, in component);
	}

	internal void ApplySetFromCommandKnownTransition<T>(
		Entity entity,
		in T   component,
		int    typeId,
		int    sourceArchetypeId,
		int    targetArchetypeId)
		where T : struct
	{
		EnsureAlive(entity);
		var location = _locationByEntityId[entity.Id];
		if (!location.IsValid || location.ArchetypeId != sourceArchetypeId)
		{
			SetComponentInternal(entity.Id, typeId, in component);
			return;
		}

		var sourceArchetype = _archetypes[sourceArchetypeId];
		if (targetArchetypeId == sourceArchetypeId)
		{
			ref var existing = ref sourceArchetype.GetRef<T>(location.ChunkIndex, location.RowIndex, typeId);
			existing = component;
			int sourceColumnIndex = sourceArchetype.GetColumnIndexOrNegative(typeId);
			if (sourceColumnIndex < 0)
				throw new InvalidOperationException(
					$"Type id '{typeId}' does not exist in archetype '{sourceArchetypeId}'."
				);

			var sourceChunk = sourceArchetype.GetChunkUnchecked(location.ChunkIndex);
			uint changeVersion = AdvanceComponentChangeVersion();
			sourceArchetype.MarkComponentChanged(sourceChunk, sourceColumnIndex, changeVersion);
			MarkComponentChanged(entity.Id, typeId, changeVersion);
			return;
		}

		MoveEntityToArchetypeWithSetKnownTransition(
			entity.Id,
			location,
			sourceArchetype,
			targetArchetypeId,
			typeId,
			in component
		);
	}

	internal void DescribeRemoveTransition(
		Entity  entity,
		int     typeId,
		out int sourceArchetypeId,
		out int targetArchetypeId)
	{
		EnsureAlive(entity);
		var location = _locationByEntityId[entity.Id];
		if (!location.IsValid)
			throw new InvalidOperationException($"Entity '{entity.Id}' is not in a valid archetype.");

		sourceArchetypeId = location.ArchetypeId;
		var sourceArchetype = _archetypes[sourceArchetypeId];
		targetArchetypeId = sourceArchetype.HasType(typeId)
								? GetOrCreateRemoveTransition(sourceArchetype, typeId)
								: sourceArchetypeId;
	}

	internal void DescribeSetTransition(Entity entity, int typeId, out int sourceArchetypeId, out int targetArchetypeId)
	{
		EnsureAlive(entity);
		var location = _locationByEntityId[entity.Id];
		if (!location.IsValid)
			throw new InvalidOperationException($"Entity '{entity.Id}' is not in a valid archetype.");

		sourceArchetypeId = location.ArchetypeId;
		var sourceArchetype = _archetypes[sourceArchetypeId];
		targetArchetypeId = sourceArchetype.HasType(typeId)
								? sourceArchetypeId
								: GetOrCreateAddTransition(sourceArchetype, typeId);
	}

	internal void DestroyEntityInternal(Entity entity)
	{
		EnsureAlive(entity);
		ReleaseRelationsForTarget(entity);
		int entityId  = entity.Id;
		var location  = _locationByEntityId[entityId];
		var archetype = _archetypes[location.ArchetypeId];
		if (archetype.RemoveAt(location.ChunkIndex, location.RowIndex, out int movedEntityId))
			UpdateMovedEntityLocation(location.ArchetypeId, location.ChunkIndex, location.RowIndex, movedEntityId);

		_aliveByEntityId[entityId]    = false;
		_locationByEntityId[entityId] = EntityLocation.Invalid;
		_versionByEntityId[entityId]++;
		_freeEntityIds[_freeCount++] = entityId;
		_aliveCount--;
		for (var typeId = 0; typeId < _typeCount; typeId++)
		{
			_addedVersionByEntityType.Remove(ComposeEntityTypeKey(entityId, typeId));
			_changeVersionByEntityType.Remove(ComposeEntityTypeKey(entityId, typeId));
		}
	}

	internal void ExitQueryCursor()
	{
		if (_activeQueryCursors > 0)
			_activeQueryCursors = 0;
	}

	internal void MaterializeQueryEntities(
		QueryChunkMatch[] chunkMatches,
		int               chunkMatchCount,
		Entity[]          destination,
		int               entityCount)
	{
		if (entityCount == 0)
			return;

		var writeIndex = 0;
		for (var i = 0; i < chunkMatchCount; i++)
		{
			var match  = chunkMatches[i];
			var chunk  = _archetypes[match.ArchetypeId].GetChunk(match.ChunkIndex);
			int rowEnd = match.RowStart + match.Count;
			for (int row = match.RowStart; row < rowEnd; row++)
			{
				int entityId = chunk.EntityIds[row];
				destination[writeIndex++] = new(entityId, _versionByEntityId[entityId]);
			}
		}

		if (writeIndex != entityCount)
			throw new InvalidOperationException("Query entity materialization count mismatch.");
	}

	internal void RemoveAllComponentsFromCommandKnownTransitionFast(int sourceArchetypeId, int targetArchetypeId)
	{
		if (targetArchetypeId == sourceArchetypeId)
			return;

		var   sourceArchetype         = _archetypes[sourceArchetypeId];
		var   targetArchetype         = _archetypes[targetArchetypeId];
		int[] sourceTargetColumnPairs = GetOrCreateTransitionCopyMap(sourceArchetype, targetArchetype);
		for (var chunkIndex = 0; chunkIndex < sourceArchetype.ChunkCount; chunkIndex++)
		{
			var chunk = sourceArchetype.GetChunk(chunkIndex);
			if (chunk.Count == 0)
				continue;

			MoveEntireChunkToArchetype(
				sourceArchetype,
				chunkIndex,
				chunk,
				targetArchetype,
				sourceTargetColumnPairs
			);
		}
	}

	internal void RemoveComponentBatchFromCommandKnownTransition(
		int[] entityIds,
		int   entityOffset,
		int   count,
		int   typeId,
		int   sourceArchetypeId,
		int   targetArchetypeId)
	{
		if (entityIds is null) throw new ArgumentNullException(nameof(entityIds));
		if (count < 0) throw new ArgumentOutOfRangeException(nameof(count));
		if (entityOffset < 0 || entityOffset + count > entityIds.Length)
			throw new ArgumentOutOfRangeException(nameof(entityOffset));

		var sourceArchetype = _archetypes[sourceArchetypeId];
		for (var i = 0; i < count; i++)
		{
			int entityId = entityIds[entityOffset + i];
			var location = _locationByEntityId[entityId];
			if (!location.IsValid || location.ArchetypeId != sourceArchetypeId)
			{
				RemoveComponentFromCommand(new(entityId, _versionByEntityId[entityId]), typeId);
				continue;
			}

			if (targetArchetypeId == sourceArchetypeId)
				continue;

			MoveEntityToArchetype(entityId, location, sourceArchetype, targetArchetypeId);
		}
	}

	internal void RemoveComponentBatchFromCommandKnownTransitionFast(
		int[] entityIds,
		int   entityOffset,
		int   count,
		int   sourceArchetypeId,
		int   targetArchetypeId)
	{
		if (count == 0 || targetArchetypeId == sourceArchetypeId)
			return;

		var   sourceArchetype         = _archetypes[sourceArchetypeId];
		var   targetArchetype         = _archetypes[targetArchetypeId];
		int[] sourceTargetColumnPairs = GetOrCreateTransitionCopyMap(sourceArchetype, targetArchetype);
		for (var i = 0; i < count; i++)
		{
			int entityId = entityIds[entityOffset + i];
			var location = _locationByEntityId[entityId];
			MoveEntityToArchetype(
				entityId,
				location,
				sourceArchetype,
				targetArchetype,
				sourceTargetColumnPairs
			);
		}
	}

	internal void RemoveComponentFromCommand(Entity entity, int typeId)
	{
		EnsureAlive(entity);
		if ((uint)typeId >= (uint)_config.ComponentTypeCapacity)
			return;

		int entityId = entity.Id;
		var location = _locationByEntityId[entityId];
		if (!location.IsValid)
			return;

		var sourceArchetype = _archetypes[location.ArchetypeId];
		if (!sourceArchetype.HasType(typeId))
			return;

		int targetArchetypeId = GetOrCreateRemoveTransition(sourceArchetype, typeId);
		MoveEntityToArchetype(entityId, location, sourceArchetype, targetArchetypeId);
	}

	internal void RemoveComponentFromCommandKnownTransition(
		Entity entity,
		int    typeId,
		int    sourceArchetypeId,
		int    targetArchetypeId)
	{
		EnsureAlive(entity);
		var location = _locationByEntityId[entity.Id];
		if (!location.IsValid || location.ArchetypeId != sourceArchetypeId)
		{
			RemoveComponentFromCommand(entity, typeId);
			return;
		}

		if (targetArchetypeId == sourceArchetypeId)
			return;

		var sourceArchetype = _archetypes[sourceArchetypeId];
		MoveEntityToArchetype(entity.Id, location, sourceArchetype, targetArchetypeId);
	}

	internal void RemoveMarkedComponentsFromCommandKnownTransitionFast(
		uint[] batchEntityMarkerBits,
		int    sourceArchetypeId,
		int    targetArchetypeId)
	{
		if (targetArchetypeId == sourceArchetypeId)
			return;

		var   sourceArchetype         = _archetypes[sourceArchetypeId];
		var   targetArchetype         = _archetypes[targetArchetypeId];
		int[] sourceTargetColumnPairs = GetOrCreateTransitionCopyMap(sourceArchetype, targetArchetype);
		for (var chunkIndex = 0; chunkIndex < sourceArchetype.ChunkCount; chunkIndex++)
		{
			var chunk = sourceArchetype.GetChunk(chunkIndex);
			if (chunk.Count == 0)
				continue;

			if (IsChunkFullyMarked(batchEntityMarkerBits, chunk))
			{
				MoveEntireChunkToArchetype(
					sourceArchetype,
					chunkIndex,
					chunk,
					targetArchetype,
					sourceTargetColumnPairs
				);

				continue;
			}

			int rowCount = chunk.Count;
			var writeRow = 0;
			var readRow  = 0;
			while (readRow < rowCount)
			{
				int entityId = chunk.EntityIds[readRow];
				if ((uint)entityId < (uint)_config.EntityCapacity &&
					IsEntityMarked(batchEntityMarkerBits, entityId))
				{
					targetArchetype.AllocateRow(entityId, out int targetChunkIndex, out int targetRowIndex);
					var targetChunk = targetArchetype.GetChunk(targetChunkIndex);
					targetArchetype.CopySharedColumnsFromWithPairs(
						chunk,
						readRow,
						targetChunk,
						targetRowIndex,
						sourceTargetColumnPairs
					);

					_locationByEntityId[entityId] = new(targetArchetype.Id, targetChunkIndex, targetRowIndex);
					readRow++;
					continue;
				}

				int runStart = readRow;
				readRow++;
				while (readRow < rowCount)
				{
					int runEntityId = chunk.EntityIds[readRow];
					if ((uint)runEntityId < (uint)_config.EntityCapacity &&
						IsEntityMarked(batchEntityMarkerBits, runEntityId))
						break;

					readRow++;
				}

				int runLength = readRow - runStart;
				if (writeRow != runStart)
				{
					sourceArchetype.MoveRowRangeWithinChunk(
						chunk,
						runStart,
						writeRow,
						runLength
					);

					int runWriteEnd = writeRow + runLength;
					for (int row = writeRow; row < runWriteEnd; row++)
					{
						int survivorEntityId = chunk.EntityIds[row];
						if ((uint)survivorEntityId < (uint)_config.EntityCapacity)
							_locationByEntityId[survivorEntityId] = new(sourceArchetypeId, chunkIndex, row);
					}
				}

				writeRow += runLength;
			}

			sourceArchetype.FinalizeChunkCompaction(chunkIndex, writeRow);
		}
	}

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

		var location = _locationByEntityId[entity.Id];
		if (!location.IsValid)
			throw new InvalidOperationException($"Entity '{entity.Id}' is not in a valid archetype.");

		archetype = _archetypes[location.ArchetypeId];
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

	internal void TrackPotentialAccessorRefWrite(
		ArchetypeStorage.Chunk chunk,
		int                    columnIndex,
		int                    typeId,
		int                    rowIndex)
	{
		if (!_trackRefWriteChanges)
			return;

		if ((uint)rowIndex >= (uint)chunk.Count)
			return;

		uint changeVersion = AdvanceComponentChangeVersion();
		chunk.ComponentChangeVersions[columnIndex] = changeVersion;
		MarkComponentChanged(chunk.EntityIds[rowIndex], typeId, changeVersion);
	}

	internal void TrackPotentialChunkMatchRefWrites(
		QueryChunkMatch[] chunkMatches,
		int               chunkMatchCount,
		int               typeId)
	{
		if (!_trackRefWriteChanges)
			return;

		for (var i = 0; i < chunkMatchCount; i++)
		{
			var match = chunkMatches[i];
			if (match.Count == 0)
				continue;

			var archetype = _archetypes[match.ArchetypeId];
			int columnIndex = archetype.GetColumnIndexOrNegative(typeId);
			if (columnIndex < 0)
				continue;

			var  chunk = archetype.GetChunkUnchecked(match.ChunkIndex);
			uint changeVersion = AdvanceComponentChangeVersion();
			chunk.ComponentChangeVersions[columnIndex] = changeVersion;
			int rowEnd = match.RowStart + match.Count;
			for (var row = match.RowStart; row < rowEnd; row++)
				MarkComponentChanged(chunk.EntityIds[row], typeId, changeVersion);
		}
	}

	internal void TrackPotentialCursorRefWrite(
		ArchetypeStorage.Chunk chunk,
		int                    columnIndex,
		int                    typeId,
		int                    rowIndex) =>
		TrackPotentialAccessorRefWrite(chunk, columnIndex, typeId, rowIndex);

	internal void RunDirectFast<TSpec, TJob, T1>(QueryHandle<TSpec> handle, TJob job)
		where TSpec : struct, ICompiledQuerySpec
		where TJob : struct, IForEach<T1>
		where T1 : unmanaged
	{
		ThrowIfDisposed();
		ThrowIfDirectIterationUnavailable(handle);
		int matchCount = GetOrRefreshMatchingArchetypes(handle.Plan);
		if (matchCount == 0)
			return;

		int[] matchingArchetypeIds = handle.Plan.MatchingArchetypeIds;
		int   typeId1              = GetOrCreateComponentTypeId<T1>();
		TrackPotentialDirectFastRefWrites(matchingArchetypeIds, matchCount, typeId1);
		var   processedEntityCount = 0;
		// TODO: Cache per-plan column indices per archetype for typed run signatures to remove these lookups.
		for (var i = 0; i < matchCount; i++)
		{
			int archetypeId  = matchingArchetypeIds[i];
			var archetype    = _archetypes[archetypeId];
			int columnIndex1 = archetype.GetColumnIndexOrNegative(typeId1);
			if (columnIndex1 < 0)
				throw new KeyNotFoundException(
					$"Type id '{typeId1}' does not exist in archetype '{archetypeId}'."
				);

			for (var chunkIndex = 0; chunkIndex < archetype.ChunkCount; chunkIndex++)
			{
				var chunk      = archetype.GetChunkUnchecked(chunkIndex);
				int chunkCount = chunk.Count;
				if (chunkCount == 0)
					continue;

				int remaining = _queryEntities.Length - processedEntityCount;
				if (remaining <= 0)
				{
					_queryOverflowCount++;
					if (_config.OverflowPolicy == WorldOverflowPolicy.FailFast)
						throw new InvalidOperationException(
							$"Query result capacity '{_config.QueryResultCapacity}' was exceeded."
						);

					goto done;
				}

				int     rowsToProcess = Math.Min(remaining, chunkCount);
				ref var c1Start       = ref archetype.GetRefByIndex<T1>(chunk, columnIndex1, 0);
				for (var offset = 0; offset < rowsToProcess; offset++)
					job.Execute(ref Unsafe.Add(ref c1Start, offset));

				processedEntityCount += rowsToProcess;
				if (rowsToProcess < chunkCount)
				{
					_queryOverflowCount++;
					if (_config.OverflowPolicy == WorldOverflowPolicy.FailFast)
						throw new InvalidOperationException(
							$"Query result capacity '{_config.QueryResultCapacity}' was exceeded."
						);

					goto done;
				}
			}
		}

		done:
		if (processedEntityCount > _queryHighWatermark)
			_queryHighWatermark = processedEntityCount;
	}

	internal void RunDirectFast<TSpec, TJob, T1, T2>(QueryHandle<TSpec> handle, TJob job)
		where TSpec : struct, ICompiledQuerySpec
		where TJob : struct, IForEach<T1, T2>
		where T1 : unmanaged
		where T2 : unmanaged
	{
		ThrowIfDisposed();
		ThrowIfDirectIterationUnavailable(handle);
		int matchCount = GetOrRefreshMatchingArchetypes(handle.Plan);
		if (matchCount == 0)
			return;

		int[] matchingArchetypeIds = handle.Plan.MatchingArchetypeIds;
		int   typeId1              = GetOrCreateComponentTypeId<T1>();
		TrackPotentialDirectFastRefWrites(matchingArchetypeIds, matchCount, typeId1);
		int   typeId2              = GetOrCreateComponentTypeId<T2>();
		var   processedEntityCount = 0;
		for (var i = 0; i < matchCount; i++)
		{
			int archetypeId  = matchingArchetypeIds[i];
			var archetype    = _archetypes[archetypeId];
			int columnIndex1 = archetype.GetColumnIndexOrNegative(typeId1);
			if (columnIndex1 < 0)
				throw new KeyNotFoundException(
					$"Type id '{typeId1}' does not exist in archetype '{archetypeId}'."
				);

			int columnIndex2 = archetype.GetColumnIndexOrNegative(typeId2);
			if (columnIndex2 < 0)
				throw new KeyNotFoundException(
					$"Type id '{typeId2}' does not exist in archetype '{archetypeId}'."
				);

			for (var chunkIndex = 0; chunkIndex < archetype.ChunkCount; chunkIndex++)
			{
				var chunk      = archetype.GetChunkUnchecked(chunkIndex);
				int chunkCount = chunk.Count;
				if (chunkCount == 0)
					continue;

				int remaining = _queryEntities.Length - processedEntityCount;
				if (remaining <= 0)
				{
					_queryOverflowCount++;
					if (_config.OverflowPolicy == WorldOverflowPolicy.FailFast)
						throw new InvalidOperationException(
							$"Query result capacity '{_config.QueryResultCapacity}' was exceeded."
						);

					goto done;
				}

				int     rowsToProcess = Math.Min(remaining, chunkCount);
				ref var c1Start       = ref archetype.GetRefByIndex<T1>(chunk, columnIndex1, 0);
				ref var c2Start       = ref archetype.GetRefByIndex<T2>(chunk, columnIndex2, 0);
				for (var offset = 0; offset < rowsToProcess; offset++)
				{
					ref var c1 = ref Unsafe.Add(ref c1Start, offset);
					ref var c2 = ref Unsafe.Add(ref c2Start, offset);
					job.Execute(ref c1, in c2);
				}

				processedEntityCount += rowsToProcess;
				if (rowsToProcess < chunkCount)
				{
					_queryOverflowCount++;
					if (_config.OverflowPolicy == WorldOverflowPolicy.FailFast)
						throw new InvalidOperationException(
							$"Query result capacity '{_config.QueryResultCapacity}' was exceeded."
						);

					goto done;
				}
			}
		}

		done:
		if (processedEntityCount > _queryHighWatermark)
			_queryHighWatermark = processedEntityCount;
	}

	internal void RunDirectFast<TSpec, TJob, T1, T2, T3>(QueryHandle<TSpec> handle, TJob job)
		where TSpec : struct, ICompiledQuerySpec
		where TJob : struct, IForEach<T1, T2, T3>
		where T1 : unmanaged
		where T2 : unmanaged
		where T3 : unmanaged
	{
		ThrowIfDisposed();
		ThrowIfDirectIterationUnavailable(handle);
		int matchCount = GetOrRefreshMatchingArchetypes(handle.Plan);
		if (matchCount == 0)
			return;

		int[] matchingArchetypeIds = handle.Plan.MatchingArchetypeIds;
		int   typeId1              = GetOrCreateComponentTypeId<T1>();
		TrackPotentialDirectFastRefWrites(matchingArchetypeIds, matchCount, typeId1);
		int   typeId2              = GetOrCreateComponentTypeId<T2>();
		int   typeId3              = GetOrCreateComponentTypeId<T3>();
		var   processedEntityCount = 0;
		for (var i = 0; i < matchCount; i++)
		{
			int archetypeId  = matchingArchetypeIds[i];
			var archetype    = _archetypes[archetypeId];
			int columnIndex1 = archetype.GetColumnIndexOrNegative(typeId1);
			if (columnIndex1 < 0)
				throw new KeyNotFoundException(
					$"Type id '{typeId1}' does not exist in archetype '{archetypeId}'."
				);

			int columnIndex2 = archetype.GetColumnIndexOrNegative(typeId2);
			if (columnIndex2 < 0)
				throw new KeyNotFoundException(
					$"Type id '{typeId2}' does not exist in archetype '{archetypeId}'."
				);

			int columnIndex3 = archetype.GetColumnIndexOrNegative(typeId3);
			if (columnIndex3 < 0)
				throw new KeyNotFoundException(
					$"Type id '{typeId3}' does not exist in archetype '{archetypeId}'."
				);

			for (var chunkIndex = 0; chunkIndex < archetype.ChunkCount; chunkIndex++)
			{
				var chunk      = archetype.GetChunkUnchecked(chunkIndex);
				int chunkCount = chunk.Count;
				if (chunkCount == 0)
					continue;

				int remaining = _queryEntities.Length - processedEntityCount;
				if (remaining <= 0)
				{
					_queryOverflowCount++;
					if (_config.OverflowPolicy == WorldOverflowPolicy.FailFast)
						throw new InvalidOperationException(
							$"Query result capacity '{_config.QueryResultCapacity}' was exceeded."
						);

					goto done;
				}

				int     rowsToProcess = Math.Min(remaining, chunkCount);
				ref var c1Start       = ref archetype.GetRefByIndex<T1>(chunk, columnIndex1, 0);
				ref var c2Start       = ref archetype.GetRefByIndex<T2>(chunk, columnIndex2, 0);
				ref var c3Start       = ref archetype.GetRefByIndex<T3>(chunk, columnIndex3, 0);
				for (var offset = 0; offset < rowsToProcess; offset++)
				{
					ref var c1 = ref Unsafe.Add(ref c1Start, offset);
					ref var c2 = ref Unsafe.Add(ref c2Start, offset);
					ref var c3 = ref Unsafe.Add(ref c3Start, offset);
					job.Execute(ref c1, in c2, in c3);
				}

				processedEntityCount += rowsToProcess;
				if (rowsToProcess < chunkCount)
				{
					_queryOverflowCount++;
					if (_config.OverflowPolicy == WorldOverflowPolicy.FailFast)
						throw new InvalidOperationException(
							$"Query result capacity '{_config.QueryResultCapacity}' was exceeded."
						);

					goto done;
				}
			}
		}

		done:
		if (processedEntityCount > _queryHighWatermark)
			_queryHighWatermark = processedEntityCount;
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

	private static bool IsEntityMarked(uint[] markerBits, int entityId)
	{
		int  markerWordIndex = entityId >> 5;
		uint markerBit       = 1u << (entityId & 31);
		return (markerBits[markerWordIndex] & markerBit) != 0u;
	}

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

	private static int GetMaskWordCount(int componentTypeCapacity) =>
		Math.Max(1, (componentTypeCapacity + 63) / 64);

	private static int[] AddTypeIdSorted(int[] source, int typeId)
	{
		var result   = new int[source.Length + 1];
		var src      = 0;
		var dst      = 0;
		var inserted = false;
		while (src < source.Length)
		{
			if (!inserted && typeId < source[src])
			{
				result[dst++] = typeId;
				inserted      = true;
			}

			result[dst++] = source[src++];
		}

		if (!inserted)
			result[dst] = typeId;

		return result;
	}

	private static int[] RemoveTypeIdSorted(int[] source, int typeId)
	{
		var result  = new int[source.Length - 1];
		var dst     = 0;
		var removed = false;
		for (var i = 0; i < source.Length; i++)
		{
			if (!removed && source[i] == typeId)
			{
				removed = true;
				continue;
			}

			if (dst < result.Length)
				result[dst++] = source[i];
		}

		if (!removed)
			throw new InvalidOperationException($"Type id '{typeId}' was not found in source archetype.");

		return result;
	}

	private int GetOrCreateRelationTypeId(Type relationType, Entity target)
	{
		if (relationType is null)
			throw new ArgumentNullException(nameof(relationType));

		var key = (relationType, target.Id, target.Version);
		if (_relationTypeIdByKey.TryGetValue(key, out int existing))
			return existing;

		if (_typeCount >= _typeById.Length)
		{
			_componentTypeOverflowCount++;
			throw new InvalidOperationException(
				$"Component type capacity '{_config.ComponentTypeCapacity}' was exceeded."
			);
		}

		int typeId = _typeCount++;
		_typeById[typeId]          = typeof(RelationMarker);
		_typeIsManagedLane[typeId] = false;
		if (_typeCount > _registeredTypeHighWatermark)
			_registeredTypeHighWatermark = _typeCount;

		_relationTypeIdByKey[key]     = typeId;
		_relationInfoByTypeId[typeId] = new(relationType, target);
		AppendRelationTypeId(_relationTypeIdsByRelationType, relationType,               typeId);
		AppendRelationTypeId(_relationTypeIdsByTarget,      (target.Id, target.Version), typeId);
		return typeId;
	}

	private int[] GetRelationTypeIds(Type relationType)
	{
		if (relationType is null)
			throw new ArgumentNullException(nameof(relationType));

		return _relationTypeIdsByRelationType.TryGetValue(relationType, out int[]? ids) ? ids : [];
	}

	private bool TryGetRelationTypeId(Type relationType, Entity target, out int relationTypeId)
	{
		if (relationType is null)
			throw new ArgumentNullException(nameof(relationType));

		return _relationTypeIdByKey.TryGetValue((relationType, target.Id, target.Version), out relationTypeId);
	}

	private void ReleaseRelationsForTarget(Entity target)
	{
		if (!_relationTypeIdsByTarget.TryGetValue((target.Id, target.Version), out int[]? relationTypeIds) ||
			relationTypeIds.Length == 0)
			return;

		_relationTypeIdsByTarget.Remove((target.Id, target.Version));
		for (var i = 0; i < relationTypeIds.Length; i++)
		{
			int relationTypeId = relationTypeIds[i];
			RemoveRelationTypeFromAllSources(relationTypeId);
			if (!_relationInfoByTypeId.TryGetValue(relationTypeId, out var info))
				continue;

			_relationInfoByTypeId.Remove(relationTypeId);
			_relationTypeIdByKey.Remove((info.RelationType, info.Target.Id, info.Target.Version));
			RemoveRelationTypeId(
				_relationTypeIdsByRelationType,
				info.RelationType,
				relationTypeId
			);
		}
	}

	private void RemoveRelationTypeFromAllSources(int relationTypeId)
	{
		if (relationTypeId < 0)
			return;

		for (var entityId = 0; entityId < _nextEntityId; entityId++)
		{
			if (!_aliveByEntityId[entityId])
				continue;

			var location = _locationByEntityId[entityId];
			if (!location.IsValid)
				continue;

			var archetype = _archetypes[location.ArchetypeId];
			if (!archetype.HasType(relationTypeId))
				continue;

			RemoveComponentFromCommand(new(entityId, _versionByEntityId[entityId]), relationTypeId);
		}
	}

	private static void AppendRelationTypeId<TKey>(Dictionary<TKey, int[]> map, TKey key, int relationTypeId)
		where TKey : notnull
	{
		int[] current = map.TryGetValue(key, out int[]? existing) ? existing : [];
		var updated = new int[current.Length + 1];
		Array.Copy(current, updated, current.Length);
		updated[current.Length] = relationTypeId;
		map[key]                = updated;
	}

	private static void RemoveRelationTypeId<TKey>(Dictionary<TKey, int[]> map, TKey key, int relationTypeId)
		where TKey : notnull
	{
		if (!map.TryGetValue(key, out int[]? existing) || existing.Length == 0)
			return;

		int index = -1;
		for (var i = 0; i < existing.Length; i++)
		{
			if (existing[i] != relationTypeId)
				continue;

			index = i;
			break;
		}

		if (index < 0)
			return;

		if (existing.Length == 1)
		{
			map.Remove(key);
			return;
		}

		var updated = new int[existing.Length - 1];
		if (index > 0)
			Array.Copy(existing, 0, updated, 0, index);

		if (index < existing.Length - 1)
			Array.Copy(existing, index + 1, updated, index, existing.Length - index - 1);

		map[key] = updated;
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

	private bool ArchetypeMatches(CompiledQueryPlan plan, ulong[] archetypeMaskWords)
	{
		for (var i = 0; i < plan.AllMaskWordIndices.Length; i++)
		{
			int   wordIndex = plan.AllMaskWordIndices[i];
			ulong allWord   = plan.AllMaskWords[wordIndex];
			if ((archetypeMaskWords[wordIndex] & allWord) != allWord)
				return false;
		}

		for (var i = 0; i < plan.NoneMaskWordIndices.Length; i++)
		{
			int wordIndex = plan.NoneMaskWordIndices[i];
			if ((archetypeMaskWords[wordIndex] & plan.NoneMaskWords[wordIndex]) != 0UL)
				return false;
		}

		if (plan.AnyTypeIds.Length == 0)
			return true;

		for (var i = 0; i < plan.AnyMaskWordIndices.Length; i++)
		{
			int wordIndex = plan.AnyMaskWordIndices[i];
			if ((archetypeMaskWords[wordIndex] & plan.AnyMaskWords[wordIndex]) != 0UL)
				return true;
		}

		return false;
	}

	private bool ArchetypeMatchesRelationFilter(CompiledQueryPlan plan, ArchetypeStorage archetype)
	{
		Type? relationType = plan.RelatedRelationType;
		if (relationType is null)
			return true;

		Entity target = plan.RelatedTarget;
		if (target == Entity.Wildcard)
		{
			int[] relationTypeIds = GetRelationTypeIds(relationType);
			for (var i = 0; i < relationTypeIds.Length; i++)
			{
				if (archetype.HasType(relationTypeIds[i]))
					return true;
			}

			return false;
		}

		return TryGetRelationTypeId(relationType, target, out int relationTypeId) &&
			   archetype.HasType(relationTypeId);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private bool IsAliveUnchecked(Entity entity) =>
		entity.Id >= 0 &&
		entity.Id < _aliveByEntityId.Length &&
		_aliveByEntityId[entity.Id] &&
		_versionByEntityId[entity.Id] == entity.Version;

	private bool IsChunkFullyMarked(uint[] markerBits, ArchetypeStorage.Chunk chunk)
	{
		for (var row = 0; row < chunk.Count; row++)
		{
			int entityId = chunk.EntityIds[row];
			if ((uint)entityId >= (uint)_config.EntityCapacity || !IsEntityMarked(markerBits, entityId))
				return false;
		}

		return true;
	}

	private bool TryAppendQueryChunk(in QueryChunkMatch chunkMatch, ref int chunkMatchCount)
	{
		if (chunkMatchCount >= _queryChunkMatches.Length)
		{
			_queryOverflowCount++;
			if (_config.OverflowPolicy == WorldOverflowPolicy.FailFast)
				throw new InvalidOperationException(
					$"Query result capacity '{_config.QueryResultCapacity}' was exceeded."
				);

			return false;
		}

		_queryChunkMatches[chunkMatchCount++] = chunkMatch;
		return true;
	}

	private bool TryAppendQueryRange(
		int refArchetypeId,
		int chunkIndex,
		int rowStart,
		int rowCount,
		ref int count,
		ref int chunkMatchCount)
	{
		int remaining = _queryEntities.Length - count;
		if (remaining <= 0)
		{
			_queryOverflowCount++;
			if (_config.OverflowPolicy == WorldOverflowPolicy.FailFast)
				throw new InvalidOperationException(
					$"Query result capacity '{_config.QueryResultCapacity}' was exceeded."
				);

			return false;
		}

		int rowsToAppend = Math.Min(remaining, rowCount);
		if (!TryAppendQueryChunk(
				new(refArchetypeId, chunkIndex, rowStart, rowsToAppend, count),
				ref chunkMatchCount
			))
			return false;

		count += rowsToAppend;
		if (rowsToAppend >= rowCount)
			return true;

		_queryOverflowCount++;
		if (_config.OverflowPolicy == WorldOverflowPolicy.FailFast)
			throw new InvalidOperationException(
				$"Query result capacity '{_config.QueryResultCapacity}' was exceeded."
			);

		return false;
	}

	private bool TryGetComponentUnchecked<T>(int entityId, int typeId, out T component) where T : struct
	{
		var location = _locationByEntityId[entityId];
		if (!location.IsValid)
		{
			component = default;
			return false;
		}

		var archetype   = _archetypes[location.ArchetypeId];
		int columnIndex = archetype.GetColumnIndexOrNegative(typeId);
		if (columnIndex < 0)
		{
			component = default;
			return false;
		}

		var chunk = archetype.GetChunkUnchecked(location.ChunkIndex);
		if ((uint)location.RowIndex >= (uint)chunk.Count)
		{
			component = default;
			return false;
		}

		component = archetype.GetRefByIndex<T>(chunk, columnIndex, location.RowIndex);
		return true;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private bool TryResolveEntityIdFromChunkMatch(in QueryChunkMatch match, int index, out int entityId)
	{
		int offset = index - match.EntityStartIndex;
		if ((uint)offset >= (uint)match.Count)
		{
			entityId = default;
			return false;
		}

		var chunk = _archetypes[match.ArchetypeId].GetChunkUnchecked(match.ChunkIndex);
		entityId = chunk.EntityIds[match.RowStart + offset];
		return true;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static long ComposeEntityTypeKey(int entityId, int typeId) => (long)entityId << 32 | (uint)typeId;

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private bool EntityMatchesChangedFilters(CompiledQueryPlan plan, int entityId)
	{
		// TODO: Track ref-based writes from direct/component-access APIs in change metadata without regressing hot loops.
		uint lastObservedVersion = plan.LastObservedChangeVersion;
		for (var i = 0; i < plan.AddedTypeIds.Length; i++)
		{
			long key = ComposeEntityTypeKey(entityId, plan.AddedTypeIds[i]);
			if (!_addedVersionByEntityType.TryGetValue(key, out uint addedVersion))
				return false;

			if (addedVersion <= lastObservedVersion)
				return false;
		}

		for (var i = 0; i < plan.ChangedTypeIds.Length; i++)
		{
			long key = ComposeEntityTypeKey(entityId, plan.ChangedTypeIds[i]);
			if (!_changeVersionByEntityType.TryGetValue(key, out uint changeVersion))
				return false;

			if (changeVersion <= lastObservedVersion)
				return false;
		}

		return true;
	}

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

	private Type[] ResolveTypes(int[] typeIds)
	{
		if (typeIds.Length == 0)
			return [];

		var resolved = new Type[typeIds.Length];
		for (var i = 0; i < typeIds.Length; i++)
		{
			int typeId = typeIds[i];
			resolved[i] = _typeById[typeId] ??
						  throw new InvalidOperationException(
							  $"Component type id '{typeId}' is not registered."
						  );
		}

		return resolved;
	}

	private void ApplySnapshotRestorePlan(in SnapshotRestorePlan restorePlan)
	{
		var entityMap = new Dictionary<Entity, Entity>(restorePlan.Entities.Length);
		for (var i = 0; i < restorePlan.Entities.Length; i++)
		{
			var captured = restorePlan.Entities[i];
			entityMap[captured.Entity] = Spawn();
		}

		for (var i = 0; i < restorePlan.Resources.Length; i++)
		{
			var resource = restorePlan.Resources[i];
			SetResourceBoxed(resource.ResourceType, resource.Value);
		}

		for (var i = 0; i < restorePlan.Entities.Length; i++)
		{
			var captured = restorePlan.Entities[i];
			if (!entityMap.TryGetValue(captured.Entity, out Entity restored))
				throw new InvalidOperationException(
					$"Snapshot entity '{captured.Entity.Id}:{captured.Entity.Version}' was not restored."
				);

			for (var componentIndex = 0; componentIndex < captured.Components.Length; componentIndex++)
			{
				var component = captured.Components[componentIndex];
				SetComponentFromSnapshot(restored, component.ComponentType, component.Value);
			}
		}

		for (var i = 0; i < restorePlan.Relations.Length; i++)
		{
			var relation = restorePlan.Relations[i];
			if (!entityMap.TryGetValue(relation.Source, out Entity source))
				throw new InvalidOperationException(
					$"Snapshot relation source '{relation.Source.Id}:{relation.Source.Version}' was not restored."
				);

			if (!entityMap.TryGetValue(relation.Target, out Entity target))
				throw new InvalidOperationException(
					$"Snapshot relation target '{relation.Target.Id}:{relation.Target.Version}' was not restored."
				);

			AddRelationFromSnapshot(relation.RelationType, source, target);
		}
	}

	private SnapshotRestorePlan ValidateSnapshotRestorePlan(
		WorldSnapshot                   snapshot,
		SnapshotDeserializationOptions options)
	{
		var snapshotEntities = snapshot.Entities;
		var snapshotResources = snapshot.Resources;
		var snapshotRelations = snapshot.Relations;
		var entityIds = new HashSet<Entity>(snapshotEntities.Length);
		var resourceTypes = new HashSet<Type>();
		var missingComponentTypes = new HashSet<Type>();
		var relationMarkerTypes = new HashSet<(Type relationType, Entity target)>();
		var relationTriples = new HashSet<(Type relationType, Entity source, Entity target)>();

		if (snapshotEntities.Length > _config.EntityCapacity)
			throw new InvalidOperationException(
				$"Snapshot entity capacity '{snapshotEntities.Length}' exceeds configured entity capacity '{_config.EntityCapacity}'."
			);

		for (var i = 0; i < snapshotResources.Length; i++)
		{
			var resource = snapshotResources[i];
			ValidateSnapshotResource(resource, options);
			if (!resourceTypes.Add(resource.ResourceType))
				throw new InvalidOperationException(
					$"Duplicate snapshot resource type '{resource.ResourceType.FullName}' is not allowed."
				);
		}

		for (var i = 0; i < snapshotEntities.Length; i++)
		{
			var captured = snapshotEntities[i];
			if (captured.Entity == Entity.None)
				throw new InvalidOperationException("Snapshot entities cannot use Entity.None as an identity.");

			if (!entityIds.Add(captured.Entity))
				throw new InvalidOperationException(
					$"Duplicate snapshot entity '{captured.Entity.Id}:{captured.Entity.Version}' is not allowed."
				);

			if (captured.Components is null)
				throw new InvalidOperationException(
					$"Snapshot entity '{captured.Entity.Id}:{captured.Entity.Version}' has a null component array."
				);

			var entityComponentTypes = new HashSet<Type>();
			for (var componentIndex = 0; componentIndex < captured.Components.Length; componentIndex++)
			{
				var component = captured.Components[componentIndex];
				ValidateSnapshotComponent(component, options);
				if (!entityComponentTypes.Add(component.ComponentType))
					throw new InvalidOperationException(
						$"Duplicate snapshot component type '{component.ComponentType.FullName}' for entity '{captured.Entity.Id}:{captured.Entity.Version}' is not allowed."
					);

				if (!_typeToId.ContainsKey(component.ComponentType))
					missingComponentTypes.Add(component.ComponentType);
			}
		}

		for (var i = 0; i < snapshotRelations.Length; i++)
		{
			var relation = snapshotRelations[i];
			ValidateSnapshotRelation(relation, options);
			if (!entityIds.Contains(relation.Source))
				throw new InvalidOperationException(
					$"Snapshot relation source '{relation.Source.Id}:{relation.Source.Version}' was not found in the entity payload."
				);

			if (!entityIds.Contains(relation.Target))
				throw new InvalidOperationException(
					$"Snapshot relation target '{relation.Target.Id}:{relation.Target.Version}' was not found in the entity payload."
				);

			if (!relationTriples.Add((relation.RelationType, relation.Source, relation.Target)))
				throw new InvalidOperationException(
					$"Duplicate snapshot relation '{relation.RelationType.FullName}' from '{relation.Source.Id}:{relation.Source.Version}' to '{relation.Target.Id}:{relation.Target.Version}' is not allowed."
				);

			relationMarkerTypes.Add((relation.RelationType, relation.Target));
		}

		int projectedTypeCount = checked(_typeCount + missingComponentTypes.Count + relationMarkerTypes.Count);
		if (projectedTypeCount > _config.ComponentTypeCapacity)
			throw new InvalidOperationException(
				$"Snapshot restore would exceed component type capacity '{_config.ComponentTypeCapacity}'."
			);

		return new(snapshotResources, snapshotEntities, snapshotRelations);
	}

	private void ValidateSnapshotResource(
		in SnapshotResourceRecord       resource,
		SnapshotDeserializationOptions options)
	{
		if (resource.ResourceType is null)
			throw new InvalidOperationException("Snapshot resource type cannot be null.");

		if (resource.Value is null)
			throw new InvalidOperationException(
				$"Snapshot resource '{resource.ResourceType.FullName}' contains a null value."
			);

		if (resource.Value.GetType() != resource.ResourceType)
			throw new InvalidOperationException(
				$"Snapshot resource value type '{resource.Value.GetType().FullName}' does not match declared type '{resource.ResourceType.FullName}'."
			);

		if (!options.IsResourceTypeAllowListed(resource.ResourceType))
			throw new InvalidOperationException(
				$"Snapshot resource type '{resource.ResourceType.FullName}' is not allow-listed."
			);

		if (!options.IsTypeAllowed(resource.ResourceType))
			throw new InvalidOperationException(
				$"Snapshot resource type '{resource.ResourceType.FullName}' is not allowed."
			);
	}

	private void ValidateSnapshotComponent(
		in SnapshotComponentRecord      component,
		SnapshotDeserializationOptions options)
	{
		if (component.ComponentType is null)
			throw new InvalidOperationException("Snapshot component type cannot be null.");

		if (!component.ComponentType.IsValueType)
			throw new InvalidOperationException(
				$"Snapshot component type '{component.ComponentType.FullName}' must be a value type."
			);

		if (component.Value is null)
			throw new InvalidOperationException(
				$"Snapshot component '{component.ComponentType.FullName}' contains a null value."
			);

		if (component.Value.GetType() != component.ComponentType)
			throw new InvalidOperationException(
				$"Snapshot component value type '{component.Value.GetType().FullName}' does not match declared type '{component.ComponentType.FullName}'."
			);

		if (!options.IsComponentTypeAllowListed(component.ComponentType))
			throw new InvalidOperationException(
				$"Snapshot component type '{component.ComponentType.FullName}' is not allow-listed."
			);

		if (!options.IsTypeAllowed(component.ComponentType))
			throw new InvalidOperationException(
				$"Snapshot component type '{component.ComponentType.FullName}' is not allowed."
			);
	}

	private void ValidateSnapshotRelation(
		in SnapshotRelationRecord       relation,
		SnapshotDeserializationOptions options)
	{
		if (relation.RelationType is null)
			throw new InvalidOperationException("Snapshot relation type cannot be null.");

		if (!relation.RelationType.IsValueType)
			throw new InvalidOperationException(
				$"Snapshot relation type '{relation.RelationType.FullName}' must be a value type."
			);

		if (relation.Source == Entity.None || relation.Source == Entity.Wildcard ||
			relation.Target == Entity.None || relation.Target == Entity.Wildcard)
			throw new InvalidOperationException("Snapshot relations must reference concrete entities.");

		if (!options.IsRelationTypeAllowListed(relation.RelationType))
			throw new InvalidOperationException(
				$"Snapshot relation type '{relation.RelationType.FullName}' is not allow-listed."
			);

		if (!options.IsTypeAllowed(relation.RelationType))
			throw new InvalidOperationException(
				$"Snapshot relation type '{relation.RelationType.FullName}' is not allowed."
			);
	}

	private void SetResourceBoxed(Type resourceType, object value)
	{
		if (resourceType is null) throw new ArgumentNullException(nameof(resourceType));
		if (value is null) throw new ArgumentNullException(nameof(value));
		if (!resourceType.IsInstanceOfType(value))
			throw new ArgumentException(
				$"Resource value type '{value.GetType().FullName}' is not assignable to '{resourceType.FullName}'.",
				nameof(value)
			);

		if (_resources.TryGetValue(resourceType, out object? existing) && existing is IResourceBox existingBox)
			existingBox.DisposeValue();

		var boxType = typeof(ResourceBox<>).MakeGenericType(resourceType);
		_resources[resourceType] = Activator.CreateInstance(boxType, value) ??
								  throw new InvalidOperationException(
									  $"Failed to create resource box for type '{resourceType.FullName}'."
								  );
	}

	private void SetComponentFromSnapshot(Entity entity, Type componentType, object value)
	{
		if (componentType is null) throw new ArgumentNullException(nameof(componentType));
		if (value is null) throw new ArgumentNullException(nameof(value));
		if (!componentType.IsValueType)
			throw new ArgumentException("Components must be value types.", nameof(componentType));

		EnsureAlive(entity);
		int typeId = GetOrCreateComponentTypeId(componentType);
		SetComponentInternalBoxed(entity.Id, typeId, componentType, value);
	}

	private void AddRelationFromSnapshot(Type relationType, Entity source, Entity target)
	{
		if (relationType is null) throw new ArgumentNullException(nameof(relationType));
		if (target == Entity.Wildcard || target == Entity.None)
			throw new ArgumentException("Snapshot relation target must be a concrete entity.", nameof(target));

		EnsureAlive(source);
		EnsureAlive(target);
		int relationTypeId = GetOrCreateRelationTypeId(relationType, target);
		var marker = default(RelationMarker);
		SetComponentInternal(source.Id, relationTypeId, in marker);
	}

	private int FillQueryResults(
		CompiledQueryPlan plan,
		out int           chunkMatchCount,
		bool              advanceObservedChangeVersion = true)
	{
		var count = 0;
		chunkMatchCount = 0;
		uint  queryVersion = _componentChangeVersion;
		int   matchCount   = GetOrRefreshMatchingArchetypes(plan);
		int[] matches      = plan.MatchingArchetypeIds;
		bool requiresChangedFilter = plan.ChangedTypeIds.Length > 0 || plan.AddedTypeIds.Length > 0;
		for (var i = 0; i < matchCount; i++)
		{
			var archetype = _archetypes[matches[i]];
			for (var chunkIndex = 0; chunkIndex < archetype.ChunkCount; chunkIndex++)
			{
				var chunk = archetype.GetChunk(chunkIndex);
				if (chunk.Count == 0)
					continue;

				if (!requiresChangedFilter)
				{
					if (!TryAppendQueryRange(
							matches[i],
							chunkIndex,
							0,
							chunk.Count,
							ref count,
							ref chunkMatchCount
						))
						goto done;

					continue;
				}

				var row = 0;
				while (row < chunk.Count)
				{
					while (row < chunk.Count && !EntityMatchesChangedFilters(plan, chunk.EntityIds[row]))
						row++;

					if (row >= chunk.Count)
						break;

					int runStart = row;
					row++;
					while (row < chunk.Count && EntityMatchesChangedFilters(plan, chunk.EntityIds[row]))
						row++;

					if (!TryAppendQueryRange(
							matches[i],
							chunkIndex,
							runStart,
							row - runStart,
							ref count,
							ref chunkMatchCount
						))
						goto done;
				}
			}
		}

		done:
		if (advanceObservedChangeVersion)
			plan.LastObservedChangeVersion = queryVersion;
		if (count > _queryHighWatermark)
			_queryHighWatermark = count;

		return count;
	}

	private int GetOrCreateAddTransition(ArchetypeStorage sourceArchetype, int typeId)
	{
		int cached = sourceArchetype.GetKnownAddTransition(typeId);
		if (cached != int.MinValue)
			return cached;

		if (sourceArchetype.HasType(typeId))
		{
			sourceArchetype.SetKnownAddTransition(typeId, sourceArchetype.Id);
			return sourceArchetype.Id;
		}

		int target = GetOrCreateArchetype(AddTypeIdSorted(sourceArchetype.TypeIds, typeId));
		sourceArchetype.SetKnownAddTransition(typeId, target);
		_archetypes[target].SetKnownRemoveTransition(typeId, sourceArchetype.Id);
		return target;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private uint AdvanceComponentChangeVersion()
	{
		_componentChangeVersion++;
		if (_componentChangeVersion == 0)
			_componentChangeVersion = 1;

		return _componentChangeVersion;
	}

	private void TrackPotentialDirectFastRefWrites(int[] matchingArchetypeIds, int matchCount, int typeId)
	{
		if (!_trackRefWriteChanges)
			return;

		for (var i = 0; i < matchCount; i++)
		{
			var archetype = _archetypes[matchingArchetypeIds[i]];
			int columnIndex = archetype.GetColumnIndexOrNegative(typeId);
			if (columnIndex < 0)
				continue;

			for (var chunkIndex = 0; chunkIndex < archetype.ChunkCount; chunkIndex++)
			{
				var chunk = archetype.GetChunkUnchecked(chunkIndex);
				if (chunk.Count == 0)
					continue;

				uint changeVersion = AdvanceComponentChangeVersion();
				chunk.ComponentChangeVersions[columnIndex] = changeVersion;
				for (var row = 0; row < chunk.Count; row++)
					MarkComponentChanged(chunk.EntityIds[row], typeId, changeVersion);
			}
		}
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private void TrackPotentialSingleRefWrite(int entityId, int typeId)
	{
		if (!_trackRefWriteChanges)
			return;

		var location = _locationByEntityId[entityId];
		if (!location.IsValid)
			return;

		var archetype = _archetypes[location.ArchetypeId];
		int columnIndex = archetype.GetColumnIndexOrNegative(typeId);
		if (columnIndex < 0)
			return;

		var  chunk = archetype.GetChunkUnchecked(location.ChunkIndex);
		uint changeVersion = AdvanceComponentChangeVersion();
		chunk.ComponentChangeVersions[columnIndex] = changeVersion;
		MarkComponentChanged(entityId, typeId, changeVersion);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private void TrackPotentialQueryChunkMatchRefWrites(int chunkMatchCount, int typeId)
	{
		if (!_trackRefWriteChanges)
			return;

		TrackPotentialChunkMatchRefWrites(_queryChunkMatches, chunkMatchCount, typeId);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private void MarkComponentChanged(int entityId, int typeId, uint version) =>
		_changeVersionByEntityType[ComposeEntityTypeKey(entityId, typeId)] = version;

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private void MarkComponentAdded(int entityId, int typeId, uint version) =>
		_addedVersionByEntityType[ComposeEntityTypeKey(entityId, typeId)] = version;

	private int GetOrCreateArchetype(int[] sortedTypeIds)
	{
		var key = new ArchetypeTypeSetKey(sortedTypeIds);
		if (_archetypeByTypeSet.TryGetValue(key, out int existing))
			return existing;

		var columnTypes = new Type[sortedTypeIds.Length];
		var managedLane = new bool[sortedTypeIds.Length];
		for (var i = 0; i < sortedTypeIds.Length; i++)
		{
			int typeId = sortedTypeIds[i];
			columnTypes[i] = _typeById[typeId] ??
							 throw new InvalidOperationException(
								 $"Component type id '{typeId}' was not registered before archetype creation."
							 );

			managedLane[i] = _typeIsManagedLane[typeId];
		}

		int archetypeId = _archetypes.Count;
		var archetype = new ArchetypeStorage(
			archetypeId,
			sortedTypeIds,
			BuildMaskWords(sortedTypeIds),
			columnTypes,
			managedLane,
			_config.ComponentTypeCapacity,
			_config.ChunkCapacity
		);

		_archetypes.Add(archetype);
		_archetypeByTypeSet[key] = archetypeId;
		_archetypeVersion++;
		return archetypeId;
	}

	private int GetOrCreateComponentTypeIdGeneric<T>() where T : struct
	{
		var componentType = typeof(T);
		if (_typeToId.TryGetValue(componentType, out int existing))
			return existing;

		if (_typeCount >= _typeById.Length)
		{
			_componentTypeOverflowCount++;
			throw new InvalidOperationException(
				$"Component type capacity '{_config.ComponentTypeCapacity}' was exceeded."
			);
		}

		int typeId = _typeCount++;
		_typeToId[componentType]   = typeId;
		_typeById[typeId]          = componentType;
		_typeIsManagedLane[typeId] = RuntimeHelpers.IsReferenceOrContainsReferences<T>();
		if (_typeCount > _registeredTypeHighWatermark)
			_registeredTypeHighWatermark = _typeCount;

		return typeId;
	}

	private int GetOrCreateRemoveTransition(ArchetypeStorage sourceArchetype, int typeId)
	{
		int cached = sourceArchetype.GetKnownRemoveTransition(typeId);
		if (cached != int.MinValue)
			return cached;

		if (!sourceArchetype.HasType(typeId))
		{
			sourceArchetype.SetKnownRemoveTransition(typeId, sourceArchetype.Id);
			return sourceArchetype.Id;
		}

		int target = GetOrCreateArchetype(RemoveTypeIdSorted(sourceArchetype.TypeIds, typeId));
		sourceArchetype.SetKnownRemoveTransition(typeId, target);
		_archetypes[target].SetKnownAddTransition(typeId, sourceArchetype.Id);
		return target;
	}

	private int GetOrRefreshMatchingArchetypes(CompiledQueryPlan plan)
	{
		if (plan.ArchetypeCacheVersion == _archetypeVersion)
			return plan.MatchingArchetypeCount;

		int[] matches = plan.MatchingArchetypeIds;
		if (matches.Length < _archetypes.Count)
			plan.MatchingArchetypeIds = matches = new int[_archetypes.Count];

		var count = 0;
		for (var i = 0; i < _archetypes.Count; i++)
		{
			var archetype = _archetypes[i];
			if (!ArchetypeMatches(plan, archetype.MaskWords))
				continue;

			if (!ArchetypeMatchesRelationFilter(plan, archetype))
				continue;

			matches[count++] = i;
		}

		plan.MatchingArchetypeCount = count;
		plan.ArchetypeCacheVersion  = _archetypeVersion;
		return count;
	}

	private int[] GetOrCreateTransitionCopyMap(ArchetypeStorage sourceArchetype, ArchetypeStorage targetArchetype)
	{
		long pairKey = (long)sourceArchetype.Id << 32 | (uint)targetArchetype.Id;
		if (_transitionCopyMapByPair.TryGetValue(pairKey, out int[]? existing))
			return existing;

		var sharedColumnCount = 0;
		for (var i = 0; i < targetArchetype.TypeIds.Length; i++)
		{
			int typeId = targetArchetype.TypeIds[i];
			if (sourceArchetype.GetColumnIndexOrNegative(typeId) >= 0)
				sharedColumnCount++;
		}

		var pairs     = new int[sharedColumnCount * 2];
		var pairIndex = 0;
		for (var i = 0; i < targetArchetype.TypeIds.Length; i++)
		{
			int typeId            = targetArchetype.TypeIds[i];
			int sourceColumnIndex = sourceArchetype.GetColumnIndexOrNegative(typeId);
			if (sourceColumnIndex < 0)
				continue;

			pairs[pairIndex++] = sourceColumnIndex;
			pairs[pairIndex++] = i;
		}

		_transitionCopyMapByPair[pairKey] = pairs;
		return pairs;
	}

	private ref T GetComponentRefUnchecked<T>(int entityId, int typeId) where T : struct
	{
		var location = _locationByEntityId[entityId];
		if (!location.IsValid)
			throw new InvalidOperationException($"Entity '{entityId}' is not in a valid archetype.");

		var archetype   = _archetypes[location.ArchetypeId];
		int columnIndex = archetype.GetColumnIndexOrNegative(typeId);
		if (columnIndex < 0)
			throw new KeyNotFoundException(
				$"Component '{typeof(T).Name}' was not found for entity '{entityId}:{_versionByEntityId[entityId]}'."
			);

		var chunk = archetype.GetChunkUnchecked(location.ChunkIndex);
		return ref archetype.GetRefByIndex<T>(chunk, columnIndex, location.RowIndex);
	}

	private void DisposeResources()
	{
		foreach (object boxed in _resources.Values)
		{
			if (boxed is not IResourceBox resourceBox)
				continue;

			resourceBox.DisposeValue();
		}

		_resources.Clear();
	}

	private void EnsureAlive(Entity entity)
	{
		if (!IsAliveUnchecked(entity))
			throw new InvalidOperationException($"Entity '{entity.Id}:{entity.Version}' is not alive.");
	}

	private void MoveEntireChunkToArchetype(
		ArchetypeStorage       sourceArchetype,
		int                    sourceChunkIndex,
		ArchetypeStorage.Chunk sourceChunk,
		ArchetypeStorage       targetArchetype,
		int[]                  sourceTargetColumnPairs)
	{
		int rowCount  = sourceChunk.Count;
		var sourceRow = 0;
		while (sourceRow < rowCount)
		{
			int reservedRows = targetArchetype.ReserveRows(
				rowCount - sourceRow,
				out int targetChunkIndex,
				out int targetRowStart
			);

			var targetChunk = targetArchetype.GetChunkUnchecked(targetChunkIndex);
			Array.Copy(
				sourceChunk.EntityIds,
				sourceRow,
				targetChunk.EntityIds,
				targetRowStart,
				reservedRows
			);

			targetArchetype.CopySharedColumnsFromWithPairs(
				sourceChunk,
				sourceRow,
				targetChunk,
				targetRowStart,
				reservedRows,
				sourceTargetColumnPairs
			);

			int sourceRowEnd = sourceRow + reservedRows;
			int targetRow    = targetRowStart;
			for (; sourceRow < sourceRowEnd; sourceRow++, targetRow++)
			{
				int entityId = sourceChunk.EntityIds[sourceRow];
				_locationByEntityId[entityId] = new(targetArchetype.Id, targetChunkIndex, targetRow);
			}
		}

		sourceArchetype.ClearChunk(sourceChunkIndex);
	}

	private void MoveEntityToArchetype(
		int              entityId,
		EntityLocation   sourceLocation,
		ArchetypeStorage sourceArchetype,
		int              targetArchetypeId)
	{
		if (targetArchetypeId == sourceArchetype.Id)
			return;

		var   targetArchetype         = _archetypes[targetArchetypeId];
		int[] sourceTargetColumnPairs = GetOrCreateTransitionCopyMap(sourceArchetype, targetArchetype);
		MoveEntityToArchetype(
			entityId,
			sourceLocation,
			sourceArchetype,
			targetArchetype,
			sourceTargetColumnPairs
		);
	}

	private void MoveEntityToArchetype(
		int              entityId,
		EntityLocation   sourceLocation,
		ArchetypeStorage sourceArchetype,
		ArchetypeStorage targetArchetype,
		int[]            sourceTargetColumnPairs)
	{
		var sourceChunk = sourceArchetype.GetChunk(sourceLocation.ChunkIndex);
		targetArchetype.AllocateRow(entityId, out int targetChunkIndex, out int targetRowIndex);
		var targetChunk = targetArchetype.GetChunk(targetChunkIndex);
		targetArchetype.CopySharedColumnsFromWithPairs(
			sourceChunk,
			sourceLocation.RowIndex,
			targetChunk,
			targetRowIndex,
			sourceTargetColumnPairs
		);

		_locationByEntityId[entityId] = new(targetArchetype.Id, targetChunkIndex, targetRowIndex);
		if (sourceArchetype.RemoveAt(sourceLocation.ChunkIndex, sourceLocation.RowIndex, out int movedEntityId))
			UpdateMovedEntityLocation(
				sourceArchetype.Id, sourceLocation.ChunkIndex, sourceLocation.RowIndex, movedEntityId
			);
	}

	private void MoveEntityToArchetypeWithSet<T>(
		int              entityId,
		EntityLocation   sourceLocation,
		ArchetypeStorage sourceArchetype,
		int              targetArchetypeId,
		int              setTypeId,
		in T             setComponent)
		where T : struct
	{
		if (targetArchetypeId == sourceArchetype.Id)
		{
			ref var current = ref sourceArchetype.GetRef<T>(
								  sourceLocation.ChunkIndex, sourceLocation.RowIndex, setTypeId
							  );

			current = setComponent;
			int sourceColumnIndex = sourceArchetype.GetColumnIndexOrNegative(setTypeId);
			if (sourceColumnIndex < 0)
				throw new InvalidOperationException(
					$"Type id '{setTypeId}' does not exist in archetype '{sourceArchetype.Id}'."
				);

			var sourceChunk = sourceArchetype.GetChunkUnchecked(sourceLocation.ChunkIndex);
			uint changeVersion = AdvanceComponentChangeVersion();
			sourceArchetype.MarkComponentChanged(sourceChunk, sourceColumnIndex, changeVersion);
			MarkComponentChanged(entityId, setTypeId, changeVersion);
			return;
		}

		var   targetArchetype         = _archetypes[targetArchetypeId];
		int[] sourceTargetColumnPairs = GetOrCreateTransitionCopyMap(sourceArchetype, targetArchetype);
		MoveEntityToArchetypeWithSet(
			entityId,
			sourceLocation,
			sourceArchetype,
			targetArchetype,
			sourceTargetColumnPairs,
			setTypeId,
			in setComponent
		);
	}

	private void MoveEntityToArchetypeWithSet<T>(
		int              entityId,
		EntityLocation   sourceLocation,
		ArchetypeStorage sourceArchetype,
		ArchetypeStorage targetArchetype,
		int[]            sourceTargetColumnPairs,
		int              setTypeId,
		in T             setComponent)
		where T : struct
	{
		bool setWasAdded = sourceArchetype.GetColumnIndexOrNegative(setTypeId) < 0;
		var sourceChunk = sourceArchetype.GetChunk(sourceLocation.ChunkIndex);
		targetArchetype.AllocateRow(entityId, out int targetChunkIndex, out int targetRowIndex);
		var targetChunk = targetArchetype.GetChunk(targetChunkIndex);
		targetArchetype.CopySharedColumnsFromWithPairs(
			sourceChunk,
			sourceLocation.RowIndex,
			targetChunk,
			targetRowIndex,
			sourceTargetColumnPairs
		);

		ref var setRef = ref targetArchetype.GetRef<T>(targetChunkIndex, targetRowIndex, setTypeId);
		setRef                        = setComponent;
		int targetColumnIndex = targetArchetype.GetColumnIndexOrNegative(setTypeId);
		if (targetColumnIndex < 0)
			throw new InvalidOperationException(
				$"Type id '{setTypeId}' does not exist in archetype '{targetArchetype.Id}'."
			);

		uint changeVersion = AdvanceComponentChangeVersion();
		targetArchetype.MarkComponentChanged(targetChunk, targetColumnIndex, changeVersion);
		MarkComponentChanged(entityId, setTypeId, changeVersion);
		if (setWasAdded)
			MarkComponentAdded(entityId, setTypeId, changeVersion);

		_locationByEntityId[entityId] = new(targetArchetype.Id, targetChunkIndex, targetRowIndex);

		if (sourceArchetype.RemoveAt(sourceLocation.ChunkIndex, sourceLocation.RowIndex, out int movedEntityId))
			UpdateMovedEntityLocation(
				sourceArchetype.Id, sourceLocation.ChunkIndex, sourceLocation.RowIndex, movedEntityId
			);
	}

	private void MoveEntityToArchetypeWithSetKnownTransition<T>(
		int              entityId,
		EntityLocation   sourceLocation,
		ArchetypeStorage sourceArchetype,
		int              targetArchetypeId,
		int              setTypeId,
		in T             setComponent)
		where T : struct
	{
		MoveEntityToArchetypeWithSet(
			entityId,
			sourceLocation,
			sourceArchetype,
			targetArchetypeId,
			setTypeId,
			in setComponent
		);
	}

	private void MoveEntityToArchetypeWithSetBoxed(
		int              entityId,
		EntityLocation   sourceLocation,
		ArchetypeStorage sourceArchetype,
		int              targetArchetypeId,
		int              setTypeId,
		Type             setComponentType,
		object           setComponent)
	{
		if (targetArchetypeId == sourceArchetype.Id)
		{
			int sourceColumnIndex = sourceArchetype.GetColumnIndexOrNegative(setTypeId);
			if (sourceColumnIndex < 0)
				throw new InvalidOperationException(
					$"Type id '{setTypeId}' does not exist in archetype '{sourceArchetype.Id}'."
				);

			var sourceChunk = sourceArchetype.GetChunkUnchecked(sourceLocation.ChunkIndex);
			sourceChunk.Columns[sourceColumnIndex].SetValue(sourceLocation.RowIndex, setComponent);
			uint changeVersion = AdvanceComponentChangeVersion();
			sourceArchetype.MarkComponentChanged(sourceChunk, sourceColumnIndex, changeVersion);
			MarkComponentChanged(entityId, setTypeId, changeVersion);
			return;
		}

		bool setWasAdded = sourceArchetype.GetColumnIndexOrNegative(setTypeId) < 0;
		var sourceChunkForMove = sourceArchetype.GetChunk(sourceLocation.ChunkIndex);
		var targetArchetype = _archetypes[targetArchetypeId];
		int[] sourceTargetColumnPairs = GetOrCreateTransitionCopyMap(sourceArchetype, targetArchetype);
		targetArchetype.AllocateRow(entityId, out int targetChunkIndex, out int targetRowIndex);
		var targetChunk = targetArchetype.GetChunk(targetChunkIndex);
		targetArchetype.CopySharedColumnsFromWithPairs(
			sourceChunkForMove,
			sourceLocation.RowIndex,
			targetChunk,
			targetRowIndex,
			sourceTargetColumnPairs
		);

		int targetColumnIndex = targetArchetype.GetColumnIndexOrNegative(setTypeId);
		if (targetColumnIndex < 0)
			throw new InvalidOperationException(
				$"Type id '{setTypeId}' does not exist in archetype '{targetArchetype.Id}'."
			);

		// TODO: Cache boxed snapshot set delegates by component type to reduce reflection-heavy restore overhead.
		if (targetChunk.Columns[targetColumnIndex].ComponentType != setComponentType)
			throw new InvalidOperationException(
				$"Snapshot component type '{setComponentType.FullName}' does not match runtime column type '{targetChunk.Columns[targetColumnIndex].ComponentType.FullName}'."
			);

		targetChunk.Columns[targetColumnIndex].SetValue(targetRowIndex, setComponent);
		uint targetChangeVersion = AdvanceComponentChangeVersion();
		targetArchetype.MarkComponentChanged(targetChunk, targetColumnIndex, targetChangeVersion);
		MarkComponentChanged(entityId, setTypeId, targetChangeVersion);
		if (setWasAdded)
			MarkComponentAdded(entityId, setTypeId, targetChangeVersion);

		_locationByEntityId[entityId] = new(targetArchetype.Id, targetChunkIndex, targetRowIndex);
		if (sourceArchetype.RemoveAt(sourceLocation.ChunkIndex, sourceLocation.RowIndex, out int movedEntityId))
			UpdateMovedEntityLocation(
				sourceArchetype.Id, sourceLocation.ChunkIndex, sourceLocation.RowIndex, movedEntityId
			);
	}

	private void SetComponentInternalBoxed(
		int    entityId,
		int    typeId,
		Type   componentType,
		object component)
	{
		var location = _locationByEntityId[entityId];
		if (!location.IsValid)
			throw new InvalidOperationException($"Entity '{entityId}' is not in a valid archetype.");

		var sourceArchetype = _archetypes[location.ArchetypeId];
		if (sourceArchetype.HasType(typeId))
		{
			int sourceColumnIndex = sourceArchetype.GetColumnIndexOrNegative(typeId);
			if (sourceColumnIndex < 0)
				throw new InvalidOperationException(
					$"Type id '{typeId}' does not exist in archetype '{sourceArchetype.Id}'."
				);

			var sourceChunk = sourceArchetype.GetChunkUnchecked(location.ChunkIndex);
			sourceChunk.Columns[sourceColumnIndex].SetValue(location.RowIndex, component);
			uint changeVersion = AdvanceComponentChangeVersion();
			sourceArchetype.MarkComponentChanged(sourceChunk, sourceColumnIndex, changeVersion);
			MarkComponentChanged(entityId, typeId, changeVersion);
			return;
		}

		int targetArchetypeId = GetOrCreateAddTransition(sourceArchetype, typeId);
		MoveEntityToArchetypeWithSetBoxed(
			entityId,
			location,
			sourceArchetype,
			targetArchetypeId,
			typeId,
			componentType,
			component
		);
	}

	private void SetComponentInternal<T>(int entityId, int typeId, in T component) where T : struct
	{
		var location = _locationByEntityId[entityId];
		if (!location.IsValid)
			throw new InvalidOperationException($"Entity '{entityId}' is not in a valid archetype.");

		var sourceArchetype = _archetypes[location.ArchetypeId];
		if (sourceArchetype.HasType(typeId))
		{
			ref var existing = ref sourceArchetype.GetRef<T>(location.ChunkIndex, location.RowIndex, typeId);
			existing = component;
			int sourceColumnIndex = sourceArchetype.GetColumnIndexOrNegative(typeId);
			if (sourceColumnIndex < 0)
				throw new InvalidOperationException(
					$"Type id '{typeId}' does not exist in archetype '{sourceArchetype.Id}'."
				);

			var sourceChunk = sourceArchetype.GetChunkUnchecked(location.ChunkIndex);
			uint changeVersion = AdvanceComponentChangeVersion();
			sourceArchetype.MarkComponentChanged(sourceChunk, sourceColumnIndex, changeVersion);
			MarkComponentChanged(entityId, typeId, changeVersion);
			return;
		}

		int targetArchetypeId = GetOrCreateAddTransition(sourceArchetype, typeId);
		MoveEntityToArchetypeWithSet(entityId, location, sourceArchetype, targetArchetypeId, typeId, in component);
	}

	private void ThrowIfDirectIterationUnavailable<TSpec>(QueryHandle<TSpec> handle)
		where TSpec : struct, ICompiledQuerySpec
	{
		if (_activeQueryCursors > 0)
			throw new InvalidOperationException("Direct query iteration cannot run while a query cursor is active.");

		if (!ReferenceEquals(handle.Plan.Owner, this))
			throw new InvalidOperationException("Query handle belongs to a different world.");
	}

	private void ThrowIfDisposed()
	{
		if (_disposed)
			throw new ObjectDisposedException(nameof(World));
	}

	private void UpdateMovedEntityLocation(int archetypeId, int chunkIndex, int rowIndex, int movedEntityId)
	{
		var moved = _locationByEntityId[movedEntityId];
		_locationByEntityId[movedEntityId] = new(archetypeId, chunkIndex, rowIndex);
		if (moved.ArchetypeId != archetypeId || moved.ChunkIndex != chunkIndex)
			throw new InvalidOperationException("Moved entity location update is inconsistent.");
	}

	private static class ComponentTypeIdCache<T> where T : struct
	{
		public static int OwnerInstanceId;
		public static int TypeId;
	}

	private interface IResourceBox
	{
		object BoxedValue { get; }

		Type ResourceType { get; }

		void DisposeValue();
	}

	private sealed class ResourceBox<T>(T value) : IResourceBox where T : notnull
	{
		public T Value = value;

		public object BoxedValue => Value;

		public Type ResourceType => typeof(T);

		public void DisposeValue()
		{
			if (Value is IDisposable disposable)
				disposable.Dispose();
		}
	}

	private readonly record struct SnapshotRestorePlan(
		SnapshotResourceRecord[] Resources,
		SnapshotEntityRecord[] Entities,
		SnapshotRelationRecord[] Relations);
}



