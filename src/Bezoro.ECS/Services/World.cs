using Bezoro.ECS.Abstractions;
using Bezoro.ECS.Generated;
using Bezoro.ECS.Internal;
using Bezoro.ECS.Options;
using Bezoro.ECS.Types;
using System.Runtime.ExceptionServices;

namespace Bezoro.ECS.Services;

/// <summary>
///     Main entry point for ECS state management.
/// </summary>
public sealed partial class World : IWorld, IDisposable, IAsyncDisposable
{
	private const int MAX_QUERY_CACHE_ENTRIES = 1024;

	private readonly Dictionary<ArchetypeKey, Archetype>           _archetypesByKey     = new();
	private readonly Dictionary<int, Archetype>                    _singleTypeArchetypes = new();
	private readonly Dictionary<TypeIdPairKey, Archetype>          _pairTypeArchetypes = new();
	private readonly Dictionary<TypeIdTripleKey, Archetype>        _tripleTypeArchetypes = new();
	private readonly Dictionary<TypeIdQuadKey, Archetype>          _quadTypeArchetypes = new();
	private readonly Dictionary<QueryCacheKey, QueryCacheEntry>    _queryCache          = new();
	private readonly Dictionary<QueryCacheKey, LinkedListNode<QueryCacheKey>> _queryCacheLruNodes = new();
	private readonly Dictionary<Type, object>                      _resources           = new();
	private readonly EntityManager                                 _entityManager;
	private readonly int                                           _chunkCapacityOverride;
	private readonly int                                           _chunkSizeInBytes;
	private readonly List<Archetype>                               _archetypes = [];
	private readonly LinkedList<QueryCacheKey>                     _queryCacheLru = [];
	private readonly SystemManager                                 _systemManager;

	private          bool _disposed;
	private          bool _disposing;
	private          int  _activeCommandPlaybacks;
	private          int  _activeQueryIterations;
	private          int  _updateDepth;

	public World() : this(new WorldOptions()) { }

	public World(ReadOnlySpan<char> name) : this(name, new()) { }

	public World(WorldOptions options) : this(CreateDefaultName().AsSpan(), options) { }

	public World(ReadOnlySpan<char> name, WorldOptions options)
	{
		if (name.Trim().IsEmpty)
			throw new ArgumentException("World name cannot be null or whitespace.", nameof(name));

		if (options is null) throw new ArgumentNullException(nameof(options));

		if (options.ChunkCapacity < 0)
			throw new ArgumentOutOfRangeException(
				nameof(options.ChunkCapacity), "Chunk capacity must be non-negative."
			);

		if (options.ChunkSizeInBytes <= 0)
			throw new ArgumentOutOfRangeException(
				nameof(options.ChunkSizeInBytes), "Chunk size in bytes must be positive."
			);

		if (options.MaxDegreeOfParallelism <= 0)
			throw new ArgumentOutOfRangeException(
				nameof(options.MaxDegreeOfParallelism), "Parallelism must be positive."
			);

		_chunkCapacityOverride = options.ChunkCapacity;
		_chunkSizeInBytes      = options.ChunkSizeInBytes;
		MaxDegreeOfParallelism = options.MaxDegreeOfParallelism;
		WorldId                = CreateWorldId();
		Name                   = name.ToString();
		_entityManager         = new(WorldId);
		_systemManager         = new(MaxDegreeOfParallelism);
		EmptyArchetype         = CreateArchetypeInternal([], []);
		ComponentTypeCatalog.RegisterAll(ComponentTypeRegistry);
		ChangeVersion = 1;
	}

	public int EntityCount
	{
		get
		{
			ThrowIfDisposed();
			return _entityManager.AliveCount;
		}
	}

	public string Name { get; }

	internal Archetype EmptyArchetype { get; }

	internal bool HasActiveQueryIteration => Volatile.Read(ref _activeQueryIterations) > 0;

	internal bool IsPlayingBackCommands => Volatile.Read(ref _activeCommandPlaybacks) > 0;

	internal bool                  IsUpdating            => Volatile.Read(ref _updateDepth) > 0;
	internal ComponentTypeRegistry ComponentTypeRegistry { get; } = new();

	internal int MaxDegreeOfParallelism { get; }
	internal int QueryCacheCapacity     => MAX_QUERY_CACHE_ENTRIES;
	internal int QueryCacheEntryCount   => _queryCache.Count;

	internal int SchedulerPlanBuildCount => _systemManager.PlanBuildCount;

	internal int WorldId { get; }

	internal IReadOnlyDictionary<Type, object> Resources => _resources;

	internal IReadOnlyList<Archetype> Archetypes => _archetypes;

	internal uint ChangeVersion { get; private set; }

	public static World Deserialize(byte[] bytes) =>
		WorldSerializer.Deserialize(bytes, SnapshotDeserializationOptions.Default);

	public static World Deserialize(byte[] bytes, SnapshotDeserializationOptions options)
	{
		if (options is null) throw new ArgumentNullException(nameof(options));

		return WorldSerializer.Deserialize(bytes, options);
	}

	public Archetype GetOrCreateArchetype(params Type[] componentTypes)
	{
		ThrowIfDisposed();
		if (componentTypes is null) throw new ArgumentNullException(nameof(componentTypes));

		if (componentTypes.Length == 0) return EmptyArchetype;

		var typeIds = new int[componentTypes.Length];
		var types   = new Type[componentTypes.Length];

		for (var i = 0; i < componentTypes.Length; i++)
		{
			var type = componentTypes[i] ?? throw new ArgumentNullException(nameof(componentTypes));
			typeIds[i] = ComponentTypeRegistry.GetOrCreate(type);
			types[i]   = type;
		}

		Array.Sort(typeIds, types);
		var uniqueCount = 0;
		for (var i = 0; i < typeIds.Length; i++)
		{
			if (i > 0 && typeIds[i] == typeIds[i - 1]) continue;

			typeIds[uniqueCount] = typeIds[i];
			types[uniqueCount]   = types[i];
			uniqueCount++;
		}

		if (uniqueCount == typeIds.Length) return GetOrCreateArchetype(typeIds, types);

		Array.Resize(ref typeIds, uniqueCount);
		Array.Resize(ref types,   uniqueCount);

		return GetOrCreateArchetype(typeIds, types);
	}

	public Archetype GetOrCreateArchetype<T1>() where T1 : struct
	{
		ThrowIfDisposed();
		int typeId = ComponentTypeRegistry.GetOrCreate<T1>();
		if (_singleTypeArchetypes.TryGetValue(typeId, out var archetype))
			return archetype;

		archetype = GetOrCreateArchetype([typeId], [typeof(T1)]);
		_singleTypeArchetypes[typeId] = archetype;
		return archetype;
	}

	public Archetype GetOrCreateArchetype<T1, T2>() where T1 : struct where T2 : struct
	{
		ThrowIfDisposed();
		int typeId1 = ComponentTypeRegistry.GetOrCreate<T1>();
		int typeId2 = ComponentTypeRegistry.GetOrCreate<T2>();
		if (typeId1 == typeId2)
			return GetOrCreateArchetype<T1>();

		var key = new TypeIdPairKey(typeId1, typeId2);
		if (_pairTypeArchetypes.TryGetValue(key, out var archetype))
			return archetype;

		Type firstType  = key.First == typeId1 ? typeof(T1) : typeof(T2);
		Type secondType = key.Second == typeId1 ? typeof(T1) : typeof(T2);

		archetype = GetOrCreateArchetype(
			[key.First, key.Second],
			[firstType, secondType]
		);

		_pairTypeArchetypes[key] = archetype;
		return archetype;
	}

	public Archetype GetOrCreateArchetype<T1, T2, T3>()
		where T1 : struct where T2 : struct where T3 : struct
	{
		ThrowIfDisposed();
		int typeId1 = ComponentTypeRegistry.GetOrCreate<T1>();
		int typeId2 = ComponentTypeRegistry.GetOrCreate<T2>();
		int typeId3 = ComponentTypeRegistry.GetOrCreate<T3>();
		if (typeId1 == typeId2 || typeId1 == typeId3 || typeId2 == typeId3)
			return GetOrCreateArchetype(typeof(T1), typeof(T2), typeof(T3));

		var key = new TypeIdTripleKey(typeId1, typeId2, typeId3);
		if (_tripleTypeArchetypes.TryGetValue(key, out var archetype))
			return archetype;

		Type ResolveType(int resolvedId)
		{
			if (resolvedId == typeId1) return typeof(T1);
			if (resolvedId == typeId2) return typeof(T2);
			return typeof(T3);
		}

		archetype = GetOrCreateArchetype(
			[key.First, key.Second, key.Third],
			[
				ResolveType(key.First),
				ResolveType(key.Second),
				ResolveType(key.Third)
			]
		);

		_tripleTypeArchetypes[key] = archetype;
		return archetype;
	}

	public Archetype GetOrCreateArchetype<T1, T2, T3, T4>()
		where T1 : struct
		where T2 : struct
		where T3 : struct
		where T4 : struct
	{
		ThrowIfDisposed();
		int typeId1 = ComponentTypeRegistry.GetOrCreate<T1>();
		int typeId2 = ComponentTypeRegistry.GetOrCreate<T2>();
		int typeId3 = ComponentTypeRegistry.GetOrCreate<T3>();
		int typeId4 = ComponentTypeRegistry.GetOrCreate<T4>();
		if (typeId1 == typeId2 || typeId1 == typeId3 || typeId1 == typeId4 ||
			typeId2 == typeId3 || typeId2 == typeId4 || typeId3 == typeId4)
			return GetOrCreateArchetype(typeof(T1), typeof(T2), typeof(T3), typeof(T4));

		var key = new TypeIdQuadKey(typeId1, typeId2, typeId3, typeId4);
		if (_quadTypeArchetypes.TryGetValue(key, out var archetype))
			return archetype;

		Type ResolveType(int resolvedId)
		{
			if (resolvedId == typeId1) return typeof(T1);
			if (resolvedId == typeId2) return typeof(T2);
			if (resolvedId == typeId3) return typeof(T3);
			return typeof(T4);
		}

		archetype = GetOrCreateArchetype(
			[key.First, key.Second, key.Third, key.Fourth],
			[
				ResolveType(key.First),
				ResolveType(key.Second),
				ResolveType(key.Third),
				ResolveType(key.Fourth)
			]
		);

		_quadTypeArchetypes[key] = archetype;
		return archetype;
	}

	public bool Has<T>(Entity entity) where T : struct
	{
		ThrowIfDisposed();
		_entityManager.EnsureAlive(entity);
		int typeId = ComponentTypeRegistry.GetOrCreate<T>();
		return HasComponent(typeId, entity);
	}

	public bool IsAlive(Entity entity)
	{
		ThrowIfDisposed();
		return _entityManager.IsAlive(entity);
	}

	public bool TryGet<T>(Entity entity, out T component) where T : struct
	{
		ThrowIfDisposed();
		_entityManager.EnsureAlive(entity);
		int typeId = ComponentTypeRegistry.GetOrCreate<T>();
		if (!TryGetComponentArray(entity, typeId, out var chunk, out int slot, out int componentIndex))
		{
			component = default;
			return false;
		}

		component = chunk.GetReference<T>(componentIndex, slot);
		return true;
	}

	public byte[] Serialize()
	{
		ThrowIfDisposed();
		return WorldSerializer.Serialize(this);
	}

	public void Serialize(Stream destination)
	{
		ThrowIfDisposed();
		if (destination is null) throw new ArgumentNullException(nameof(destination));

		WorldSerializer.Serialize(this, destination);
	}

	public CommandBuffer CreateCommandBuffer()
	{
		ThrowIfDisposed();
		return new(this);
	}

	public Entity Spawn()
	{
		ThrowIfDisposed();
		EnsureNotUpdating();
		return CreateEntityInternal(EmptyArchetype);
	}

	public Entity Spawn<T1>(in T1 component1) where T1 : struct
	{
		ThrowIfDisposed();
		EnsureNotUpdating();
		var archetype = GetOrCreateArchetype<T1>();
		var entity    = CreateEntityInternal(archetype);
		Set(entity, in component1);
		return entity;
	}

	public Entity Spawn<T1, T2>(in T1 component1, in T2 component2)
		where T1 : struct
		where T2 : struct
	{
		ThrowIfDisposed();
		EnsureNotUpdating();
		var archetype = GetOrCreateArchetype<T1, T2>();
		var entity    = CreateEntityInternal(archetype);
		Set(entity, in component1);
		Set(entity, in component2);
		return entity;
	}

	public Entity Spawn<T1, T2, T3>(in T1 component1, in T2 component2, in T3 component3)
		where T1 : struct
		where T2 : struct
		where T3 : struct
	{
		ThrowIfDisposed();
		EnsureNotUpdating();
		var archetype = GetOrCreateArchetype<T1, T2, T3>();
		var entity    = CreateEntityInternal(archetype);
		Set(entity, in component1);
		Set(entity, in component2);
		Set(entity, in component3);
		return entity;
	}

	public Entity Spawn<T1, T2, T3, T4>(in T1 component1, in T2 component2, in T3 component3, in T4 component4)
		where T1 : struct
		where T2 : struct
		where T3 : struct
		where T4 : struct
	{
		ThrowIfDisposed();
		EnsureNotUpdating();
		var archetype = GetOrCreateArchetype<T1, T2, T3, T4>();
		var entity    = CreateEntityInternal(archetype);
		Set(entity, in component1);
		Set(entity, in component2);
		Set(entity, in component3);
		Set(entity, in component4);
		return entity;
	}


	public Query Query()
	{
		ThrowIfDisposed();
		return new(this, null, new([], [], [], [], [], null, Entity.None));
	}

	public Query Query(Archetype archetype)
	{
		ThrowIfDisposed();
		if (archetype is null) throw new ArgumentNullException(nameof(archetype));

		EnsureOwnedArchetype(archetype);
		return new(this, archetype, new([], [], [], [], [], null, Entity.None));
	}

	public ref T Get<T>(Entity entity) where T : struct
	{
		ThrowIfDisposed();
		_entityManager.EnsureAlive(entity);
		int typeId = ComponentTypeRegistry.GetOrCreate<T>();
		if (!TryGetComponentArray(entity, typeId, out var chunk, out int slot, out int componentIndex))
			throw new KeyNotFoundException($"Component of type {typeof(T).Name} not found for entity {entity.Id}.");

		return ref chunk.GetReference<T>(componentIndex, slot);
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
		Add(entity, in component);
	}

	public void Add<T>(Entity entity, in T component) where T : struct
	{
		ThrowIfDisposed();
		EnsureNotUpdating();
		int typeId = ComponentTypeRegistry.GetOrCreate<T>();
		ApplyAddComponentTyped(entity, typeId, in component);
	}

	public void Add<TRelation>(Entity source, Entity target)
	{
		ThrowIfDisposed();
		EnsureNotUpdating();
		_entityManager.EnsureAlive(source);
		if (!_entityManager.IsAlive(target) && target != Entity.Wildcard)
			throw new InvalidOperationException("Relationship target must be alive.");

		int relationTypeId = ComponentTypeRegistry.GetOrCreateRelationship(typeof(TRelation), target);
		AddRelationshipWithMove(source, relationTypeId);
	}

	public void AddSystem(ISystem system, Stage stage = Stage.Tick)
	{
		ThrowIfDisposed();
		_systemManager.RegisterSystem(this, system, stage);
	}

	public void AddSystem<TSystem>(Stage stage = Stage.Tick)
		where TSystem : ISystem, new() =>
		AddSystem(new TSystem(), stage);

	public void Clear()
	{
		ThrowIfDisposed();
		EnsureNotUpdating();

		ResetArchetypes(preserveEmptyArchetype: true);
		_entityManager.Clear();
		ComponentTypeRegistry.ClearRelationships();
		ClearQueryCache();
	}

	public void Despawn(Entity entity)
	{
		ThrowIfDisposed();
		EnsureNotUpdating();
		DestroyEntityInternal(entity);
	}

	public void Dispose()
	{
		if (!TryBeginDispose())
			return;

		List<Exception>? exceptions = null;
		try
		{
			ExecuteDisposeStep(_systemManager.Shutdown, ref exceptions);
			ExecuteDisposeStep(_ => CleanupWorldStateForDispose(), ref exceptions);
			MergeExceptions(ref exceptions, DisposeResources());
		}
		finally
		{
			FinishDispose();
		}

		ThrowIfAny(exceptions);
	}

	public async ValueTask DisposeAsync()
	{
		if (!TryBeginDispose())
			return;

		List<Exception>? exceptions = null;
		try
		{
			ExecuteDisposeStep(_systemManager.Shutdown, ref exceptions);
			ExecuteDisposeStep(_ => CleanupWorldStateForDispose(), ref exceptions);
			MergeExceptions(ref exceptions, await DisposeResourcesAsync().ConfigureAwait(false));
		}
		finally
		{
			FinishDispose();
		}

		ThrowIfAny(exceptions);
	}

	/// <summary>
	///     Updates systems registered to the <see cref="SystemLoopPhase.FixedTick" /> loop phase.
	/// </summary>
	/// <param name="deltaTime">Elapsed fixed-step time in seconds for this update.</param>
	public void FixedTick(float deltaTime)
	{
		ThrowIfDisposed();
		RunPhase(SystemLoopPhase.FixedTick, deltaTime);
	}

	/// <summary>
	///     Updates systems registered to the <see cref="SystemLoopPhase.LateTick" /> loop phase.
	/// </summary>
	/// <param name="deltaTime">Elapsed time in seconds for this late update.</param>
	public void LateTick(float deltaTime)
	{
		ThrowIfDisposed();
		RunPhase(SystemLoopPhase.LateTick, deltaTime);
	}

	public void Remove<T>(Entity entity) where T : struct
	{
		ThrowIfDisposed();
		EnsureNotUpdating();
		_entityManager.EnsureAlive(entity);

		int typeId = ComponentTypeRegistry.GetOrCreate<T>();
		RemoveComponentById(entity, typeId);
	}

	/// <summary>
	///     Updates systems registered to the requested loop phase.
	/// </summary>
	/// <param name="loopPhase">The host loop phase to run.</param>
	/// <param name="deltaTime">Elapsed time in seconds for this phase tick.</param>
	public void RunPhase(SystemLoopPhase loopPhase, float deltaTime)
	{
		ThrowIfDisposed();
		if (Interlocked.Increment(ref _updateDepth) != 1)
		{
			Interlocked.Decrement(ref _updateDepth);
			throw new InvalidOperationException("Re-entrant world updates are not supported.");
		}

		BumpChangeVersion();
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
		_entityManager.EnsureAlive(entity);
		int typeId = ComponentTypeRegistry.GetOrCreate<T>();
		if (TrySetComponentInPlace(entity, typeId, component))
		{
			RaiseOnAdd<T>(entity, typeId);
			MarkComponentChanged(entity, typeId);
			return;
		}

		if (IsUpdating || HasActiveQueryIteration)
			throw new InvalidOperationException(
				"Structural changes are not allowed during update or query iteration. Use CommandBuffer."
			);

		AddComponentWithMove(entity, typeId, component);
		RaiseOnAdd<T>(entity, typeId);
		MarkComponentChanged(entity, typeId);
	}

	public void SetResource<T>(T resource) where T : notnull
	{
		ThrowIfDisposed();
		SetResourceBox(typeof(T), new ResourceBox<T>(resource));
	}

	/// <summary>
	///     Updates systems registered to the <see cref="SystemLoopPhase.Tick" /> loop phase.
	/// </summary>
	/// <param name="deltaTime">Elapsed time in seconds for this update.</param>
	public void Tick(float deltaTime)
	{
		ThrowIfDisposed();
		RunPhase(SystemLoopPhase.Tick, deltaTime);
	}

	/// <summary>
	///     Gets a point-in-time diagnostics snapshot for archetype, chunk, entity, and memory usage.
	/// </summary>
	/// <remarks>
	///     Memory values are estimates derived from component and entity row sizes.
	/// </remarks>
	/// <returns>World diagnostics snapshot.</returns>
	public WorldDiagnostics GetDiagnostics()
	{
		ThrowIfDisposed();
		var  archetypeDiagnostics = new ArchetypeDiagnostics[_archetypes.Count];
		var  totalChunks          = 0;
		long totalAllocatedBytes  = 0;
		long totalLiveBytes       = 0;

		for (var archetypeIndex = 0; archetypeIndex < _archetypes.Count; archetypeIndex++)
		{
			var archetype   = _archetypes[archetypeIndex];
			int chunkCount  = archetype.Chunks.Count;
			var entityCount = 0;
			for (var chunkIndex = 0; chunkIndex < chunkCount; chunkIndex++)
				entityCount += archetype.Chunks[chunkIndex].Count;

			int  allocatedEntitySlots = checked(chunkCount * archetype.ChunkCapacity);
			long bytesPerEntity       = GetBytesPerEntity(archetype.Types);
			long allocatedBytes       = bytesPerEntity * allocatedEntitySlots;
			long liveBytes            = bytesPerEntity * entityCount;

			totalChunks         += chunkCount;
			totalAllocatedBytes += allocatedBytes;
			totalLiveBytes      += liveBytes;

			archetypeDiagnostics[archetypeIndex] = new(
				archetype.Id,
				chunkCount,
				archetype.ChunkCapacity,
				entityCount,
				allocatedEntitySlots,
				bytesPerEntity,
				allocatedBytes,
				liveBytes,
				[.. archetype.Types]
			);
		}

		return new(
			archetypeDiagnostics,
			_entityManager.AliveCount,
			totalChunks,
			totalAllocatedBytes,
			totalLiveBytes
		);
	}

	internal Entity CreateEntityInternal(Archetype archetype)
	{
		var entity = _entityManager.CreateEntity();
		AddEntityToArchetype(entity, archetype);
		return entity;
	}

	internal int GetOrCreateComponentTypeId<T>() where T : struct =>
		ComponentTypeRegistry.GetOrCreate<T>();

	internal int GetOrCreateComponentTypeId(Type type) =>
		ComponentTypeRegistry.GetOrCreate(type);

	internal int GetOrCreateRelationshipTypeId(Type relationType, Entity target) =>
		ComponentTypeRegistry.GetOrCreateRelationship(relationType, target);

	internal IReadOnlyList<Archetype> GetOrCreateQueryMatches(QuerySpec spec)
	{
		var key = spec.CacheKey;
		if (_queryCache.TryGetValue(key, out var existing))
		{
			TouchQueryCacheEntry(key);
			return existing.MatchingArchetypes;
		}

		var entry = new QueryCacheEntry(spec);
		for (var i = 0; i < _archetypes.Count; i++)
		{
			var archetype = _archetypes[i];
			if (MatchesArchetype(spec, archetype))
				entry.MatchingArchetypes.Add(archetype);
		}

		AddQueryCacheEntry(key, entry);
		return entry.MatchingArchetypes;
	}

	internal void ApplyAddComponentTyped<T>(Entity entity, int typeId, in T component) where T : struct
	{
		_entityManager.EnsureAlive(entity);

		if (HasComponent(typeId, entity))
			throw new InvalidOperationException(
				$"Entity {entity.Id} already has component {typeof(T).Name}. Use Set to update."
			);

		AddComponentWithMove(entity, typeId, in component);
		RaiseOnAdd<T>(entity, typeId);
		MarkComponentChanged(entity, typeId);
	}

	internal void ApplySetComponentTyped<T>(Entity entity, int typeId, in T component) where T : struct
	{
		_entityManager.EnsureAlive(entity);

		if (TrySetComponentInPlace(entity, typeId, in component))
		{
			RaiseOnAdd<T>(entity, typeId);
			MarkComponentChanged(entity, typeId);
			return;
		}

		AddComponentWithMove(entity, typeId, in component);
		RaiseOnAdd<T>(entity, typeId);
		MarkComponentChanged(entity, typeId);
	}

	internal void DestroyEntityInternal(Entity entity)
	{
		_entityManager.EnsureAlive(entity);
		List<(int TypeId, object Value)>? removedComponents = null;
		if (IsPlayingBackCommands && _onRemoveInObservers.Count > 0)
			removedComponents = CaptureRemovedComponents(entity);

		RemoveEntityFromArchetype(entity);
		_entityManager.DestroyEntity(entity);
		PruneRelationshipsTargeting(entity);

		if (removedComponents is { })
			for (var i = 0; i < removedComponents.Count; i++)
			{
				(int typeId, object value) = removedComponents[i];
				RaiseOnRemove(entity, typeId, value);
			}
	}

	internal void EnsureEntityAlive(Entity entity) =>
		_entityManager.EnsureAlive(entity);

	internal void EnsureOwnedArchetype(Archetype archetype)
	{
		if (!ReferenceEquals(archetype.Owner, this))
			throw new InvalidOperationException("Archetype belongs to a different world.");

		if ((uint)archetype.Id >= (uint)_archetypes.Count || !ReferenceEquals(_archetypes[archetype.Id], archetype))
			throw new InvalidOperationException("Archetype does not belong to the current world state.");
	}

	internal void EnterCommandPlayback() =>
		Interlocked.Increment(ref _activeCommandPlaybacks);

	internal void EnterQueryIteration() =>
		Interlocked.Increment(ref _activeQueryIterations);

	internal void ExitCommandPlayback()
	{
		int value = Interlocked.Decrement(ref _activeCommandPlaybacks);
		if (value < 0)
			Interlocked.Exchange(ref _activeCommandPlaybacks, 0);
	}

	internal void ExitQueryIteration()
	{
		int value = Interlocked.Decrement(ref _activeQueryIterations);
		if (value < 0)
			Interlocked.Exchange(ref _activeQueryIterations, 0);
	}

	internal void RemoveComponentById(Entity entity, int typeId)
	{
		if (!_entityManager.IsAlive(entity)) return;

		var location = _entityManager.GetLocation(entity);
		if (!location.IsValid) return;

		var source      = _archetypes[location.ArchetypeId];
		int sourceIndex = source.GetTypeIndex(typeId);
		if (sourceIndex < 0) return;

		DecomposeLocation(source, location, out int sourceChunkIndex, out int sourceSlotIndex);
		var    sourceChunk  = source.Chunks[sourceChunkIndex];
		bool   shouldRaiseRemoved = IsPlayingBackCommands &&
								  _onRemoveInObservers.TryGetValue(typeId, out var handlers) &&
								  handlers.Count > 0;
		object? removedValue = shouldRaiseRemoved
								   ? sourceChunk.GetValue(sourceIndex, sourceSlotIndex)
								   : null;

		if (!source.TryGetRemoveEdge(typeId, out var target))
		{
			int[] newTypeIds = RemoveTypeId(source.TypeIds, typeId);
			target = GetOrCreateArchetypeByTypeIds(newTypeIds);
			source.SetRemoveEdge(typeId, target);
		}

		MoveEntity(entity, source, location, target);
		if (shouldRaiseRemoved)
			RaiseOnRemove(entity, typeId, removedValue!);
	}


	private static int CreateWorldId()
	{
		byte[] bytes = Guid.NewGuid().ToByteArray();
		int    id    = BitConverter.ToInt32(bytes, 0) & int.MaxValue;
		if (id == 0)
			id = BitConverter.ToInt32(bytes, 4) & int.MaxValue;

		return id == 0 ? 1 : id;
	}

	private static int ToRowIndex(Archetype archetype, int chunkIndex, int slotIndex)
	{
		if (archetype is null) throw new ArgumentNullException(nameof(archetype));

		return checked(chunkIndex * archetype.ChunkCapacity + slotIndex);
	}

	private static int[] InsertTypeId(int[] source, int typeId)
	{
		var result = new int[source.Length + 1];
		var index  = 0;
		var added  = false;

		for (var i = 0; i < source.Length; i++)
		{
			int current = source[i];
			if (!added && typeId < current)
			{
				result[index++] = typeId;
				added           = true;
			}

			result[index++] = current;
		}

		if (!added)
			result[index] = typeId;

		return result;
	}

	private static int[] RemoveTypeId(int[] source, int typeId)
	{
		var result = new int[source.Length - 1];
		var index  = 0;

		for (var i = 0; i < source.Length; i++)
		{
			if (source[i] == typeId) continue;

			result[index++] = source[i];
		}

		return result;
	}

	private static long GetBytesPerEntity(Type[] componentTypes)
	{
		long bytesPerEntity = ComponentSizeEstimator.GetSizeInBytes(typeof(Entity));
		for (var i = 0; i < componentTypes.Length; i++)
			bytesPerEntity += ComponentSizeEstimator.GetSizeInBytes(componentTypes[i]);

		return bytesPerEntity;
	}

	private static string CreateDefaultName() => $"World-{Guid.NewGuid():N}";

	private static void ClearComponentSlot(Chunk chunk, int slot)
	{
		chunk.ClearSlot(slot);
	}

	private static void DecomposeLocation(
		Archetype      archetype,
		EntityLocation location,
		out int        chunkIndex,
		out int        slotIndex)
	{
		if (archetype is null) throw new ArgumentNullException(nameof(archetype));
		if (location.RowIndex < 0)
			throw new ArgumentOutOfRangeException(nameof(location));

		chunkIndex = location.RowIndex / archetype.ChunkCapacity;
		slotIndex  = location.RowIndex % archetype.ChunkCapacity;
	}


	private (Chunk targetChunk, int targetSlot) MoveEntityCore(
		Entity         entity,
		Archetype      source,
		EntityLocation sourceLocation,
		Archetype      target)
	{
		DecomposeLocation(source, sourceLocation, out int sourceChunkIndex, out int sourceSlotIndex);
		var sourceChunk = source.Chunks[sourceChunkIndex];
		var targetChunk = target.GetOrCreateChunkWithSpace(out int targetChunkIndex);
		int targetSlot  = targetChunk.Count++;

		targetChunk.Entities[targetSlot] = entity;
		ClearComponentSlot(targetChunk, targetSlot);
		_entityManager.SetLocation(entity, new(target.Id, ToRowIndex(target, targetChunkIndex, targetSlot)));

		for (var i = 0; i < source.TypeIds.Length; i++)
		{
			int typeId      = source.TypeIds[i];
			int targetIndex = target.GetTypeIndex(typeId);
			if (targetIndex < 0) continue;

			sourceChunk.CopyValueTo(i, sourceSlotIndex, targetChunk, targetIndex, targetSlot);
		}

		RemoveEntityFromArchetype(source, sourceLocation, entity, false);
		return (targetChunk, targetSlot);
	}

	private Archetype CreateArchetypeInternal(int[] typeIds, Type[] types)
	{
		int chunkCapacity = ResolveChunkCapacity(types);
		var archetype     = new Archetype(this, _archetypes.Count, typeIds, types, chunkCapacity);
		_archetypes.Add(archetype);
		_archetypesByKey[new(typeIds)] = archetype;
		OnArchetypeCreated(archetype);
		return archetype;
	}

	private Archetype GetOrCreateArchetype(int[] typeIds, Type[] types)
	{
		var key = new ArchetypeKey(typeIds);
		if (_archetypesByKey.TryGetValue(key, out var archetype))
			return archetype;

		return CreateArchetypeInternal(typeIds, types);
	}

	private Archetype GetOrCreateArchetypeByTypeIds(int[] typeIds)
	{
		if (typeIds.Length == 0) return EmptyArchetype;

		var types = new Type[typeIds.Length];
		for (var i = 0; i < typeIds.Length; i++)
			types[i] = ComponentTypeRegistry.GetType(typeIds[i]);

		return GetOrCreateArchetype(typeIds, types);
	}

	private bool HasComponent(int typeId, Entity entity)
	{
		var location = _entityManager.GetLocation(entity);
		if (!location.IsValid) return false;

		var archetype = _archetypes[location.ArchetypeId];
		return archetype.GetTypeIndex(typeId) >= 0;
	}

	private bool MatchesArchetype(QuerySpec spec, Archetype archetype)
	{
		if (spec.AllTypeIds.Length > 0 && !archetype.ContainsAll(spec.AllTypeIds)) return false;
		if (spec.NoneTypeIds.Length > 0 && archetype.ContainsAny(spec.NoneTypeIds)) return false;
		if (spec.AnyTypeIds.Length > 0 && !archetype.ContainsAny(spec.AnyTypeIds)) return false;

		if (spec.RelatedRelationType is null) return true;

		if (spec.RelatedTarget == Entity.Wildcard)
		{
			ReadOnlySpan<int> relationIds = ComponentTypeRegistry.GetRelationshipIds(spec.RelatedRelationType);
			if (relationIds.Length == 0 || !archetype.ContainsAny(relationIds))
				return false;
		}
		else
		{
			int relationTypeId = ComponentTypeRegistry.GetOrCreateRelationship(
				spec.RelatedRelationType, spec.RelatedTarget
			);

			if (!archetype.ContainsAll([relationTypeId]))
				return false;
		}

		return true;
	}

	internal bool TryGetComponentArray(Entity entity, int typeId, out Chunk chunk, out int slot, out int componentIndex)
	{
		var location = _entityManager.GetLocation(entity);
		if (!location.IsValid)
		{
			chunk          = null!;
			slot           = -1;
			componentIndex = -1;
			return false;
		}

		var archetype = _archetypes[location.ArchetypeId];
		componentIndex = archetype.GetTypeIndex(typeId);
		if (componentIndex < 0)
		{
			chunk = null!;
			slot  = -1;
			return false;
		}

		DecomposeLocation(archetype, location, out int chunkIndex, out int slotIndex);
		chunk = archetype.Chunks[chunkIndex];
		slot  = slotIndex;
		return true;
	}

	private bool TrySetComponentInPlace<T>(Entity entity, int typeId, in T component) where T : struct
	{
		if (!TryGetComponentArray(entity, typeId, out var chunk, out int slot, out int componentIndex))
			return false;

		chunk.GetReference<T>(componentIndex, slot) = component;
		chunk.MarkChanged(componentIndex, ChangeVersion);
		return true;
	}

	private int ResolveChunkCapacity(Type[] componentTypes)
	{
		if (_chunkCapacityOverride > 0)
			return _chunkCapacityOverride;

		var bytesPerEntity = ComponentSizeEstimator.GetSizeInBytes(typeof(Entity));
		for (var i = 0; i < componentTypes.Length; i++)
			bytesPerEntity += ComponentSizeEstimator.GetSizeInBytes(componentTypes[i]);

		if (bytesPerEntity <= 0)
			bytesPerEntity = 1;

		int capacity = _chunkSizeInBytes / bytesPerEntity;
		return Math.Max(1, capacity);
	}


	private void AddComponentObject(Entity entity, Type componentType, object value)
	{
		int typeId = ComponentTypeRegistry.GetOrCreate(componentType);

		if (IsUpdating || HasActiveQueryIteration)
			throw new InvalidOperationException("Structural changes are not allowed during update or query iteration.");

		var location = _entityManager.GetLocation(entity);
		var source   = _archetypes[location.ArchetypeId];
		if (!source.TryGetAddEdge(typeId, out var target))
		{
			int[] newTypeIds = InsertTypeId(source.TypeIds, typeId);
			target = GetOrCreateArchetypeByTypeIds(newTypeIds);
			source.SetAddEdge(typeId, target);
		}

		(var targetChunk, int targetSlot) = MoveEntityCore(entity, source, location, target);
		int addedIndex = target.GetTypeIndex(typeId);
		targetChunk.SetValue(addedIndex, targetSlot, value);
	}

	private void AddComponentWithMove<T>(Entity entity, int typeId, in T component) where T : struct
	{
		var location = _entityManager.GetLocation(entity);
		var source   = _archetypes[location.ArchetypeId];

		if (!source.TryGetAddEdge(typeId, out var target))
		{
			int[] newTypeIds = InsertTypeId(source.TypeIds, typeId);
			target = GetOrCreateArchetypeByTypeIds(newTypeIds);
			source.SetAddEdge(typeId, target);
		}

		(var targetChunk, int targetSlot) = MoveEntityCore(entity, source, location, target);
		int addedIndex = target.GetTypeIndex(typeId);
		targetChunk.GetReference<T>(addedIndex, targetSlot) = component;
		targetChunk.MarkChanged(addedIndex, ChangeVersion);
	}

	private void AddEntityToArchetype(Entity entity, Archetype archetype)
	{
		var chunk = archetype.GetOrCreateChunkWithSpace(out int chunkIndex);
		int slot  = chunk.Count++;
		chunk.Entities[slot] = entity;
		ClearComponentSlot(chunk, slot);
		_entityManager.SetLocation(entity, new(archetype.Id, ToRowIndex(archetype, chunkIndex, slot)));
	}

	internal void AddRelationshipObject(Entity source, Type relationType, Entity target)
	{
		if (relationType is null) throw new ArgumentNullException(nameof(relationType));

		_entityManager.EnsureAlive(source);
		if (!_entityManager.IsAlive(target) && target != Entity.Wildcard)
			throw new InvalidOperationException(
				$"Invalid snapshot payload: relationship target '{target.Id}:{target.Version}' is not alive."
			);

		int relationTypeId = ComponentTypeRegistry.GetOrCreateRelationship(relationType, target);
		AddRelationshipWithMove(source, relationTypeId);
	}

	private void AddRelationshipWithMove(Entity entity, int relationTypeId)
	{
		var location = _entityManager.GetLocation(entity);
		var source   = _archetypes[location.ArchetypeId];
		if (source.GetTypeIndex(relationTypeId) >= 0)
			return;

		if (!source.TryGetAddEdge(relationTypeId, out var target))
		{
			int[] newTypeIds = InsertTypeId(source.TypeIds, relationTypeId);
			target = GetOrCreateArchetypeByTypeIds(newTypeIds);
			source.SetAddEdge(relationTypeId, target);
		}

		(var targetChunk, int targetSlot) = MoveEntityCore(entity, source, location, target);
		int addedIndex = target.GetTypeIndex(relationTypeId);
		targetChunk.GetReference<RelationMarker>(addedIndex, targetSlot) = new(1);
	}

	private void BumpChangeVersion()
	{
		unchecked
		{
			ChangeVersion++;
			if (ChangeVersion == 0)
				ChangeVersion = 1;
		}
	}

	private void EnsureNotUpdating()
	{
		if (IsUpdating || HasActiveQueryIteration)
			throw new InvalidOperationException(
				"Structural changes are not allowed during update or query iteration. Use CommandBuffer."
			);
	}

	internal void ThrowIfDisposed()
	{
		if (_disposed)
			throw new ObjectDisposedException(nameof(World));
	}

	private void MarkComponentChanged(Entity entity, int typeId)
	{
		if (!TryGetComponentArray(entity, typeId, out var chunk, out _, out int componentIndex))
			return;

		chunk.MarkChanged(componentIndex, ChangeVersion);
	}

	private void MoveEntity(Entity entity, Archetype source, EntityLocation sourceLocation, Archetype target) =>
		MoveEntityCore(entity, source, sourceLocation, target);

	private void OnArchetypeCreated(Archetype archetype)
	{
		foreach (var entry in _queryCache.Values)
		{
			if (MatchesArchetype(entry.Spec, archetype))
				entry.MatchingArchetypes.Add(archetype);
		}
	}


	private void RemoveEntityFromArchetype(Entity entity)
	{
		var location = _entityManager.GetLocation(entity);
		if (!location.IsValid) return;

		var archetype = _archetypes[location.ArchetypeId];
		RemoveEntityFromArchetype(archetype, location, entity, true);
	}

	private void RemoveEntityFromArchetype(
		Archetype      archetype,
		EntityLocation location,
		Entity         removedEntity,
		bool           clearLocation)
	{
		DecomposeLocation(archetype, location, out int chunkIndex, out int slot);
		var chunk = archetype.Chunks[chunkIndex];
		int last  = chunk.Count - 1;
		if (last < 0) return;

		if (slot != last)
		{
			var movedEntity = chunk.Entities[last];
			chunk.Entities[slot] = movedEntity;

			for (var i = 0; i < chunk.Columns.Length; i++)
				chunk.CopyValueTo(i, last, chunk, i, slot);

			_entityManager.SetLocation(movedEntity, new(archetype.Id, ToRowIndex(archetype, chunkIndex, slot)));
		}

		chunk.Entities[last] = default;
		chunk.ClearSlot(last);

		chunk.Count--;
		if (chunk.Count == 0)
			ReleaseEmptyChunk(archetype, chunkIndex);
		else
			archetype.NotifyChunkFreed(chunkIndex);

		if (clearLocation)
			_entityManager.SetLocation(removedEntity, EntityLocation.Empty);
	}

	private void AddQueryCacheEntry(QueryCacheKey key, QueryCacheEntry entry)
	{
		_queryCache[key] = entry;

		var node = _queryCacheLru.AddLast(key);
		_queryCacheLruNodes[key] = node;
		if (_queryCache.Count <= MAX_QUERY_CACHE_ENTRIES)
			return;

		var oldestNode = _queryCacheLru.First;
		if (oldestNode is null)
			return;

		_queryCacheLru.RemoveFirst();
		_queryCacheLruNodes.Remove(oldestNode.Value);
		_queryCache.Remove(oldestNode.Value);
	}

	private void ClearQueryCache()
	{
		_queryCache.Clear();
		_queryCacheLru.Clear();
		_queryCacheLruNodes.Clear();
	}

	private void ResetArchetypes(bool preserveEmptyArchetype)
	{
		for (var i = 0; i < _archetypes.Count; i++)
		{
			var archetype = _archetypes[i];
			archetype.ClearChunks();
			archetype.ClearTransitions();
		}

		_archetypes.Clear();
		_archetypesByKey.Clear();
		_singleTypeArchetypes.Clear();
		_pairTypeArchetypes.Clear();
		_tripleTypeArchetypes.Clear();
		_quadTypeArchetypes.Clear();

		if (!preserveEmptyArchetype)
			return;

		_archetypes.Add(EmptyArchetype);
		_archetypesByKey[new(EmptyArchetype.TypeIds)] = EmptyArchetype;
	}

	private void PruneRelationshipsTargeting(Entity target)
	{
		ReadOnlySpan<int> relationshipTypeIds = ComponentTypeRegistry.GetRelationshipIdsForTarget(target);
		if (relationshipTypeIds.Length == 0)
			return;

		var relationshipsToRemove = new List<(Entity Source, int TypeId)>();
		for (var relationIndex = 0; relationIndex < relationshipTypeIds.Length; relationIndex++)
		{
			int relationshipTypeId = relationshipTypeIds[relationIndex];
			for (var archetypeIndex = 0; archetypeIndex < _archetypes.Count; archetypeIndex++)
			{
				var archetype = _archetypes[archetypeIndex];
				if (archetype.GetTypeIndex(relationshipTypeId) < 0)
					continue;

				for (var chunkIndex = 0; chunkIndex < archetype.Chunks.Count; chunkIndex++)
				{
					var chunk = archetype.Chunks[chunkIndex];
					for (var row = 0; row < chunk.Count; row++)
						relationshipsToRemove.Add((chunk.Entities[row], relationshipTypeId));
				}
			}
		}

		for (var i = 0; i < relationshipsToRemove.Count; i++)
		{
			(var source, int typeId) = relationshipsToRemove[i];
			if (!_entityManager.IsAlive(source))
				continue;

			RemoveComponentById(source, typeId);
		}

		ComponentTypeRegistry.ReleaseRelationshipsForTarget(target);
		ClearQueryCache();
	}

	private void ReleaseEmptyChunk(Archetype archetype, int chunkIndex)
	{
		var chunks = archetype.Chunks;
		if ((uint)chunkIndex >= (uint)chunks.Count)
			return;

		var emptyChunk = chunks[chunkIndex];
		if (emptyChunk.Count > 0)
		{
			archetype.NotifyChunkFreed(chunkIndex);
			return;
		}

		int lastChunkIndex = chunks.Count - 1;
		if (chunkIndex == lastChunkIndex)
		{
			emptyChunk.DisposeColumns();
			chunks.RemoveAt(lastChunkIndex);
			archetype.NotifyChunkFreed(chunkIndex);
			return;
		}

		var movedChunk = chunks[lastChunkIndex];
		chunks[chunkIndex] = movedChunk;
		chunks.RemoveAt(lastChunkIndex);
		emptyChunk.DisposeColumns();

		for (var row = 0; row < movedChunk.Count; row++)
		{
			var movedEntity = movedChunk.Entities[row];
			_entityManager.SetLocation(movedEntity, new(archetype.Id, ToRowIndex(archetype, chunkIndex, row)));
		}

		archetype.NotifyChunkFreed(chunkIndex);
	}

	private void TouchQueryCacheEntry(QueryCacheKey key)
	{
		if (!_queryCacheLruNodes.TryGetValue(key, out var node))
			return;

		if (ReferenceEquals(node, _queryCacheLru.Last))
			return;

		_queryCacheLru.Remove(node);
		_queryCacheLru.AddLast(node);
	}

	private List<Exception>? DisposeResources()
	{
		List<Exception>? exceptions = null;
		foreach (object boxed in _resources.Values)
		{
			if (boxed is not IResourceBox resourceBox)
				continue;

			try
			{
				resourceBox.DisposeValue();
			}
			catch (Exception ex)
			{
				RecordException(ref exceptions, ex);
			}
		}

		_resources.Clear();
		return exceptions;
	}

	private async ValueTask<List<Exception>?> DisposeResourcesAsync()
	{
		List<Exception>? exceptions = null;
		foreach (object boxed in _resources.Values)
		{
			if (boxed is not IResourceBox resourceBox)
				continue;

			try
			{
				await resourceBox.DisposeValueAsync().ConfigureAwait(false);
			}
			catch (Exception ex)
			{
				RecordException(ref exceptions, ex);
			}
		}

		_resources.Clear();
		return exceptions;
	}

	private static bool IsSameResourceValue(IResourceBox left, IResourceBox right)
	{
		object leftValue  = left.ValueObject;
		object rightValue = right.ValueObject;
		if (leftValue.GetType().IsValueType || rightValue.GetType().IsValueType)
			return false;

		return ReferenceEquals(leftValue, rightValue);
	}

	private void SetResourceBox(Type type, IResourceBox resourceBox)
	{
		if (_resources.TryGetValue(type, out var existingBoxed) && existingBoxed is IResourceBox existingResourceBox &&
			!IsSameResourceValue(existingResourceBox, resourceBox))
			existingResourceBox.DisposeValue();

		_resources[type] = resourceBox;
	}

	internal void SetResourceObject(Type type, object value)
	{
		var boxType = typeof(ResourceBox<>).MakeGenericType(type);
		if (Activator.CreateInstance(boxType, value) is not IResourceBox resourceBox)
			throw new InvalidOperationException($"Resource box for type '{type.FullName}' is invalid.");

		SetResourceBox(type, resourceBox);
	}

	private sealed class ResourceBox<T>(T value)
		: IResourceBox
		where T : notnull
	{
		public T Value = value;

		object IResourceBox.ValueObject => Value;

		void IResourceBox.DisposeValue()
		{
			if (Value is IDisposable disposable)
				disposable.Dispose();
			else if (Value is IAsyncDisposable asyncDisposable)
				Task.Run(
					async () => await asyncDisposable.DisposeAsync().ConfigureAwait(false)
				).GetAwaiter().GetResult();
		}

		async ValueTask IResourceBox.DisposeValueAsync()
		{
			if (Value is IAsyncDisposable asyncDisposable)
				await asyncDisposable.DisposeAsync().ConfigureAwait(false);
			else if (Value is IDisposable disposable)
				disposable.Dispose();
		}
	}

	private bool TryBeginDispose()
	{
		if (_disposed || _disposing)
			return false;

		_disposing = true;
		return true;
	}

	private void FinishDispose()
	{
		_onAddObservers.Clear();
		_onAddRefObservers.Clear();
		_onRemoveInObservers.Clear();
		_disposed  = true;
		_disposing = false;
	}

	private void CleanupWorldStateForDispose()
	{
		ResetArchetypes(preserveEmptyArchetype: false);
		_entityManager.Clear();
		ComponentTypeRegistry.ClearRelationships();
		ClearQueryCache();
	}

	private void ExecuteDisposeStep(Action<World> step, ref List<Exception>? exceptions)
	{
		try
		{
			step(this);
		}
		catch (Exception ex)
		{
			RecordException(ref exceptions, ex);
		}
	}

	private static void RecordException(ref List<Exception>? exceptions, Exception exception) =>
		(exceptions ??= []).Add(exception);

	private static void MergeExceptions(ref List<Exception>? destination, List<Exception>? source)
	{
		if (source is null || source.Count == 0)
			return;

		if (destination is null)
		{
			destination = source;
			return;
		}

		for (var i = 0; i < source.Count; i++)
			destination.Add(source[i]);
	}

	private static void ThrowIfAny(List<Exception>? exceptions)
	{
		if (exceptions is null || exceptions.Count == 0)
			return;

		if (exceptions.Count == 1)
			ExceptionDispatchInfo.Capture(exceptions[0]).Throw();

		throw new AggregateException("One or more errors occurred while disposing world resources.", exceptions);
	}

}
