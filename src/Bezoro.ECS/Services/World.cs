#if NET9_0
using System.Text.Json;
#endif
using Bezoro.ECS.Abstractions;
using Bezoro.ECS.Internal;
using Bezoro.ECS.Options;
using Bezoro.ECS.Types;

namespace Bezoro.ECS.Services;

/// <summary>
/// Main entry point for ECS state management.
/// </summary>
public sealed class World : IWorld, IDisposable
{
#if NET9_0
	private static readonly JsonSerializerOptions SnapshotJsonOptions = new() { IncludeFields = true };
#endif

	private sealed class ResourceBox<T>
		where T : notnull
	{
		public ResourceBox(T value)
		{
			Value = value;
		}

		public T Value;
	}

	private sealed class SnapshotResource
	{
		public string TypeName { get; set; } = string.Empty;
		public string Json { get; set; } = string.Empty;
	}

	private sealed class SnapshotComponent
	{
		public string TypeName { get; set; } = string.Empty;
		public string Json { get; set; } = string.Empty;
	}

	private sealed class SnapshotEntity
	{
		public List<SnapshotComponent> Components { get; set; } = [];
	}

	private sealed class WorldSnapshot
	{
		public List<SnapshotEntity> Entities { get; set; } = [];
		public List<SnapshotResource> Resources { get; set; } = [];
	}

	private static int _nextWorldId;
	private readonly Dictionary<ArchetypeKey, Archetype> _archetypesByKey = new();
	private readonly Dictionary<string, QueryCacheEntry> _queryCache = new();
	private readonly EntityManager _entityManager;
	private readonly int _chunkCapacityOverride;
	private readonly int _chunkSizeInBytes;
	private readonly List<Archetype> _archetypes = [];
	private readonly Dictionary<Type, object> _resources = new();
	private readonly Dictionary<int, List<Delegate>> _onAddObservers = new();
	private readonly Dictionary<int, List<Delegate>> _onRemoveObservers = new();
	private readonly SystemManager _systemManager;
	private int _activeQueryIterations;
	private volatile bool _isUpdating;
	private uint _changeVersion;
	private bool _disposed;

	public World() : this(new())
	{
	}

	public World(WorldOptions options)
	{
		if (options is null) throw new ArgumentNullException(nameof(options));

		if (options.ChunkCapacity < 0)
			throw new ArgumentOutOfRangeException(nameof(options.ChunkCapacity), "Chunk capacity must be non-negative.");

		if (options.ChunkSizeInBytes <= 0)
			throw new ArgumentOutOfRangeException(nameof(options.ChunkSizeInBytes), "Chunk size in bytes must be positive.");

		if (options.MaxDegreeOfParallelism <= 0)
			throw new ArgumentOutOfRangeException(nameof(options.MaxDegreeOfParallelism), "Parallelism must be positive.");

		_chunkCapacityOverride = options.ChunkCapacity;
		_chunkSizeInBytes = options.ChunkSizeInBytes;
		MaxDegreeOfParallelism = options.MaxDegreeOfParallelism;
		_entityManager = new(WorldId);
		_systemManager = new(MaxDegreeOfParallelism);
		EmptyArchetype = CreateArchetypeInternal([], []);
		Generated.ComponentTypeCatalog.RegisterAll();
		_changeVersion = 1;
	}

	public int EntityCount => _entityManager.AliveCount;

	internal Archetype EmptyArchetype { get; }

	internal bool IsUpdating => _isUpdating;

	internal bool HasActiveQueryIteration => Volatile.Read(ref _activeQueryIterations) > 0;

	internal int MaxDegreeOfParallelism { get; }

	internal int WorldId { get; } = Interlocked.Increment(ref _nextWorldId);

	internal IReadOnlyList<Archetype> Archetypes => _archetypes;

	internal uint ChangeVersion => _changeVersion;

	public bool IsAlive(Entity entity) => _entityManager.IsAlive(entity);

	public Entity Spawn() => CreateEntity();

	public void Despawn(Entity entity) => DestroyEntity(entity);

	public bool Has<T>(Entity entity) where T : struct, IComponent => HasComponent<T>(entity);

	public ref T Get<T>(Entity entity) where T : struct, IComponent
	{
		_entityManager.EnsureAlive(entity);
		int typeId = ComponentTypeRegistry.GetOrCreate<T>();
		if (!TryGetComponentArray(entity, typeId, out var chunk, out int slot, out int componentIndex))
			throw new KeyNotFoundException($"Component of type {typeof(T).Name} not found for entity {entity.Id}.");

		return ref chunk.GetReference<T>(componentIndex, slot);
	}

	public bool TryGet<T>(Entity entity, out T component) where T : struct, IComponent => TryGetComponent(entity, out component);

	public void Set<T>(Entity entity, in T component) where T : struct, IComponent => SetComponent(entity, in component);

	public void Add<T>(Entity entity, in T component) where T : struct, IComponent => AddComponent(entity, in component);

	public void Remove<T>(Entity entity) where T : struct, IComponent => RemoveComponent<T>(entity);

	public void SetResource<T>(T resource) where T : notnull
	{
		_resources[typeof(T)] = new ResourceBox<T>(resource);
	}

	public ref T GetResource<T>() where T : notnull
	{
		if (!_resources.TryGetValue(typeof(T), out var boxed))
			throw new KeyNotFoundException($"Resource of type {typeof(T).Name} was not found.");

		return ref ((ResourceBox<T>)boxed).Value;
	}

	public void Observe<T>(Action<Entity, T> observer) where T : struct, IComponent
	{
		if (observer is null) throw new ArgumentNullException(nameof(observer));
		int typeId = ComponentTypeRegistry.GetOrCreate<T>();
		if (!_onAddObservers.TryGetValue(typeId, out var handlers))
		{
			handlers = [];
			_onAddObservers[typeId] = handlers;
		}

		handlers.Add(observer);
	}

	public void Observe<T>(Action<Entity, T, bool> observer) where T : struct, IComponent
	{
		if (observer is null) throw new ArgumentNullException(nameof(observer));
		int typeId = ComponentTypeRegistry.GetOrCreate<T>();
		if (!_onRemoveObservers.TryGetValue(typeId, out var handlers))
		{
			handlers = [];
			_onRemoveObservers[typeId] = handlers;
		}

		handlers.Add(observer);
	}

	public Entity CreateEntity()
	{
		EnsureNotUpdating();
		var entity = _entityManager.CreateEntity();
		AddEntityToArchetype(entity, EmptyArchetype);
		BumpChangeVersion();
		return entity;
	}

	public Entity CreateEntity(Archetype archetype)
	{
		if (archetype is null) throw new ArgumentNullException(nameof(archetype));

		EnsureNotUpdating();
		EnsureOwnedArchetype(archetype);

		var entity = _entityManager.CreateEntity();
		AddEntityToArchetype(entity, archetype);
		BumpChangeVersion();
		return entity;
	}

	public Query Query() =>
		new(this, null, new([], [], [], [], [], null, Entity.None));

	public Query Query(Archetype archetype)
	{
		if (archetype is null) throw new ArgumentNullException(nameof(archetype));

		EnsureOwnedArchetype(archetype);
		return new(this, archetype, new([], [], [], [], [], null, Entity.None));
	}

	public void AddSystem(ISystem system, Stage stage = Stage.Update) =>
		_systemManager.RegisterSystem(this, system, stage);

	public void RegisterSystem(ISystem system) => AddSystem(system, system.Stage);

	public void Add<TRelation>(Entity source, Entity target)
	{
		EnsureNotUpdating();
		_entityManager.EnsureAlive(source);
		if (!_entityManager.IsAlive(target) && target != Entity.Wildcard)
			throw new InvalidOperationException("Relationship target must be alive.");

		int relationTypeId = ComponentTypeRegistry.GetOrCreateRelationship(typeof(TRelation), target);
		AddRelationshipWithMove(source, relationTypeId);
		BumpChangeVersion();
	}

	public bool HasComponent<T>(Entity entity) where T : struct, IComponent
	{
		_entityManager.EnsureAlive(entity);
		int typeId = ComponentTypeRegistry.GetOrCreate<T>();
		return HasComponent(typeId, entity);
	}

	public bool TryGetComponent<T>(Entity entity, out T component) where T : struct, IComponent
	{
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

	public T GetComponent<T>(Entity entity) where T : struct, IComponent
	{
		if (TryGetComponent(entity, out T component))
			return component;

		throw new KeyNotFoundException($"Component of type {typeof(T).Name} not found for entity {entity.Id}.");
	}

	public void AddComponent<T>(Entity entity, in T component) where T : struct, IComponent
	{
		EnsureNotUpdating();
		int typeId = ComponentTypeRegistry.GetOrCreate<T>();
		ApplyAddComponentTyped(entity, typeId, in component);
	}

	public void SetComponent<T>(Entity entity, in T component) where T : struct, IComponent
	{
		_entityManager.EnsureAlive(entity);
		int typeId = ComponentTypeRegistry.GetOrCreate<T>();
		if (TrySetComponentInPlace(entity, typeId, component))
		{
			BumpChangeVersion();
			return;
		}

		if (_isUpdating || HasActiveQueryIteration)
			throw new InvalidOperationException("Structural changes are not allowed during update or query iteration. Use CommandBuffer.");

		AddComponentWithMove(entity, typeId, component);
		BumpChangeVersion();
	}

	public void RemoveComponent<T>(Entity entity) where T : struct, IComponent
	{
		EnsureNotUpdating();
		_entityManager.EnsureAlive(entity);

		int typeId = ComponentTypeRegistry.GetOrCreate<T>();
		RemoveComponentById(entity, typeId);
	}

	public void DestroyEntity(Entity entity)
	{
		EnsureNotUpdating();
		DestroyEntityInternal(entity);
	}

	public void Update(float deltaTime)
	{
		if (_disposed) throw new ObjectDisposedException(nameof(World));

		BumpChangeVersion();
		_isUpdating = true;
		try
		{
			_systemManager.UpdateAll(this, deltaTime);
		}
		finally
		{
			_isUpdating = false;
		}
	}

	public void Update() => Update(0f);

	public void Clear()
	{
		EnsureNotUpdating();

		for (var i = 0; i < _archetypes.Count; i++)
			_archetypes[i].ClearChunks();

		_entityManager.Clear();
		BumpChangeVersion();
	}

	public CommandBuffer CreateCommandBuffer() => new(this);

	public byte[] Serialize()
	{
#if !NET9_0
		throw new PlatformNotSupportedException("World serialization is available only on net9.0.");
#else
		var snapshot = new WorldSnapshot
		{
			Entities = [],
			Resources = []
		};

		for (var a = 0; a < _archetypes.Count; a++)
		{
			var archetype = _archetypes[a];
			var chunks = archetype.Chunks;
			for (var c = 0; c < chunks.Count; c++)
			{
				var chunk = chunks[c];
				for (var row = 0; row < chunk.Count; row++)
				{
					var entitySnapshot = new SnapshotEntity { Components = [] };
					for (var i = 0; i < archetype.TypeIds.Length; i++)
					{
						int typeId = archetype.TypeIds[i];
						if (ComponentTypeRegistry.IsRelationship(typeId))
							continue;

						Type type = ComponentTypeRegistry.GetType(typeId);
						object value = chunk.GetValue(i, row);
						entitySnapshot.Components.Add(
							new SnapshotComponent
							{
								TypeName = type.AssemblyQualifiedName!,
								Json = JsonSerializer.Serialize(value, type, SnapshotJsonOptions)
							}
						);
					}

					snapshot.Entities.Add(entitySnapshot);
				}
			}
		}

		foreach (var pair in _resources)
		{
			Type resourceType = pair.Key;
			object resourceBox = pair.Value;
			var valueProperty = resourceBox.GetType().GetField("Value")!;
			object value = valueProperty.GetValue(resourceBox)!;
			snapshot.Resources.Add(
				new SnapshotResource
				{
					TypeName = resourceType.AssemblyQualifiedName!,
					Json = JsonSerializer.Serialize(value, resourceType, SnapshotJsonOptions)
				}
			);
		}

		return JsonSerializer.SerializeToUtf8Bytes(snapshot, SnapshotJsonOptions);
#endif
	}

	public static World Deserialize(byte[] bytes)
	{
		if (bytes is null) throw new ArgumentNullException(nameof(bytes));

#if !NET9_0
		throw new PlatformNotSupportedException("World deserialization is available only on net9.0.");
#else
		var snapshot = JsonSerializer.Deserialize<WorldSnapshot>(bytes, SnapshotJsonOptions)
			?? throw new InvalidOperationException("Invalid snapshot payload.");

		var world = new World();
		for (var i = 0; i < snapshot.Entities.Count; i++)
		{
			var entity = world.CreateEntity();
			var components = snapshot.Entities[i].Components;
			for (var c = 0; c < components.Count; c++)
			{
				var component = components[c];
				Type type = Type.GetType(component.TypeName, throwOnError: true)!;
				object value = JsonSerializer.Deserialize(component.Json, type, SnapshotJsonOptions)!;
				world.AddComponentObject(entity, type, value);
			}
		}

		for (var i = 0; i < snapshot.Resources.Count; i++)
		{
			var resource = snapshot.Resources[i];
			Type type = Type.GetType(resource.TypeName, throwOnError: true)!;
			object value = JsonSerializer.Deserialize(resource.Json, type, SnapshotJsonOptions)!;
			world.SetResourceObject(type, value);
		}

		return world;
#endif
	}

	public void Dispose()
	{
		if (_disposed) return;
		_disposed = true;
		_systemManager.Shutdown(this);
		for (var i = 0; i < _archetypes.Count; i++)
			_archetypes[i].ClearChunks();
		_archetypes.Clear();
		_archetypesByKey.Clear();
		_queryCache.Clear();
		_resources.Clear();
	}

	internal IReadOnlyList<Archetype> GetOrCreateQueryMatches(QuerySpec spec)
	{
		string key = spec.CacheKey;
		if (_queryCache.TryGetValue(key, out var existing))
			return existing.MatchingArchetypes;

		var entry = new QueryCacheEntry(spec);
		for (var i = 0; i < _archetypes.Count; i++)
		{
			var archetype = _archetypes[i];
			if (MatchesArchetype(spec, archetype))
				entry.MatchingArchetypes.Add(archetype);
		}

		_queryCache[key] = entry;
		return entry.MatchingArchetypes;
	}

	internal void EnsureOwnedArchetype(Archetype archetype)
	{
		if (!ReferenceEquals(archetype.Owner, this))
			throw new InvalidOperationException("Archetype belongs to a different world.");
	}

	internal void EnsureEntityAlive(Entity entity) =>
		_entityManager.EnsureAlive(entity);

	internal void EnterQueryIteration() =>
		Interlocked.Increment(ref _activeQueryIterations);

	internal void ExitQueryIteration()
	{
		int value = Interlocked.Decrement(ref _activeQueryIterations);
		if (value < 0)
			Interlocked.Exchange(ref _activeQueryIterations, 0);
	}

	internal Entity CreateEntityInternal(Archetype archetype)
	{
		var entity = _entityManager.CreateEntity();
		AddEntityToArchetype(entity, archetype);
		BumpChangeVersion();
		return entity;
	}

	internal void DestroyEntityInternal(Entity entity)
	{
		_entityManager.EnsureAlive(entity);
		RemoveEntityFromArchetype(entity);
		_entityManager.DestroyEntity(entity);
		BumpChangeVersion();
	}

	internal void ApplyAddComponentTyped<T>(Entity entity, int typeId, in T component) where T : struct, IComponent
	{
		_entityManager.EnsureAlive(entity);

		if (HasComponent(typeId, entity))
			throw new InvalidOperationException($"Entity {entity.Id} already has component {typeof(T).Name}. Use SetComponent to update.");

		AddComponentWithMove(entity, typeId, in component);
		RaiseOnAdd(entity, typeId, component);
		BumpChangeVersion();
	}

	internal void ApplySetComponentTyped<T>(Entity entity, int typeId, in T component) where T : struct, IComponent
	{
		_entityManager.EnsureAlive(entity);

		if (TrySetComponentInPlace(entity, typeId, in component))
		{
			RaiseOnAdd(entity, typeId, component);
			BumpChangeVersion();
			return;
		}

		AddComponentWithMove(entity, typeId, in component);
		RaiseOnAdd(entity, typeId, component);
		BumpChangeVersion();
	}

	internal void RemoveComponentById(Entity entity, int typeId)
	{
		if (!_entityManager.IsAlive(entity)) return;

		var location = _entityManager.GetLocation(entity);
		if (!location.IsValid) return;

		var source = _archetypes[location.ArchetypeId];
		int sourceIndex = source.GetTypeIndex(typeId);
		if (sourceIndex < 0) return;

		var sourceChunk = source.Chunks[location.ChunkIndex];
		object removedValue = sourceChunk.GetValue(sourceIndex, location.SlotIndex);

		Archetype target;
		if (!source.TryGetRemoveEdge(typeId, out target!))
		{
			int[] newTypeIds = RemoveTypeId(source.TypeIds, typeId);
			target = GetOrCreateArchetypeByTypeIds(newTypeIds);
			source.SetRemoveEdge(typeId, target);
		}

		MoveEntity(entity, source, location, target);
		RaiseOnRemove(entity, typeId, removedValue);
		BumpChangeVersion();
	}

	private bool MatchesArchetype(QuerySpec spec, Archetype archetype)
	{
		if (spec.AllTypeIds.Length > 0 && !archetype.ContainsAll(spec.AllTypeIds)) return false;
		if (spec.NoneTypeIds.Length > 0 && archetype.ContainsAny(spec.NoneTypeIds)) return false;
		if (spec.AnyTypeIds.Length > 0 && !archetype.ContainsAny(spec.AnyTypeIds)) return false;

		if (spec.RelatedRelationType is not null)
		{
			if (spec.RelatedTarget == Entity.Wildcard)
			{
				var relationIds = ComponentTypeRegistry.GetRelationshipIds(spec.RelatedRelationType);
				if (relationIds.Length == 0 || !archetype.ContainsAny(relationIds))
					return false;
			}
			else
			{
				int relationTypeId = ComponentTypeRegistry.GetOrCreateRelationship(spec.RelatedRelationType, spec.RelatedTarget);
				if (!archetype.ContainsAll([relationTypeId]))
					return false;
			}
		}

		return true;
	}

	private void OnArchetypeCreated(Archetype archetype)
	{
		foreach (var entry in _queryCache.Values)
		{
			if (MatchesArchetype(entry.Spec, archetype))
				entry.MatchingArchetypes.Add(archetype);
		}
	}

	private void AddComponentObject(Entity entity, Type componentType, object value)
	{
		int typeId = ComponentTypeRegistry.GetOrCreate(componentType);
		if (_isUpdating || HasActiveQueryIteration)
			throw new InvalidOperationException("Structural changes are not allowed during update or query iteration.");

		var location = _entityManager.GetLocation(entity);
		var source = _archetypes[location.ArchetypeId];
		Archetype target;
		if (!source.TryGetAddEdge(typeId, out target!))
		{
			int[] newTypeIds = InsertTypeId(source.TypeIds, typeId);
			target = GetOrCreateArchetypeByTypeIds(newTypeIds);
			source.SetAddEdge(typeId, target);
		}

		(var targetChunk, int targetSlot) = MoveEntityCore(entity, source, location, target);
		int addedIndex = target.GetTypeIndex(typeId);
		targetChunk.SetValue(addedIndex, targetSlot, value);
	}

	private void SetResourceObject(Type type, object value)
	{
		Type boxType = typeof(ResourceBox<>).MakeGenericType(type);
		_resources[type] = Activator.CreateInstance(boxType, value)!;
	}

	private void RaiseOnAdd<T>(Entity entity, int typeId, in T component) where T : struct, IComponent
	{
		if (!_onAddObservers.TryGetValue(typeId, out var handlers))
			return;

		for (var i = 0; i < handlers.Count; i++)
		{
			if (handlers[i] is Action<Entity, T> observer)
				observer(entity, component);
		}
	}

	private void RaiseOnRemove(Entity entity, int typeId, object removedValue)
	{
		if (!_onRemoveObservers.TryGetValue(typeId, out var handlers))
			return;

		for (var i = 0; i < handlers.Count; i++)
			handlers[i].DynamicInvoke(entity, removedValue, true);
	}

	private void AddRelationshipWithMove(Entity entity, int relationTypeId)
	{
		var location = _entityManager.GetLocation(entity);
		var source = _archetypes[location.ArchetypeId];
		if (source.GetTypeIndex(relationTypeId) >= 0)
			return;

		Archetype target;
		if (!source.TryGetAddEdge(relationTypeId, out target!))
		{
			int[] newTypeIds = InsertTypeId(source.TypeIds, relationTypeId);
			target = GetOrCreateArchetypeByTypeIds(newTypeIds);
			source.SetAddEdge(relationTypeId, target);
		}

		(var targetChunk, int targetSlot) = MoveEntityCore(entity, source, location, target);
		int addedIndex = target.GetTypeIndex(relationTypeId);
		targetChunk.GetReference<RelationMarker>(addedIndex, targetSlot) = new RelationMarker(1);
	}

	private static int[] InsertTypeId(int[] source, int typeId)
	{
		var result = new int[source.Length + 1];
		var index = 0;
		var added = false;

		for (var i = 0; i < source.Length; i++)
		{
			int current = source[i];
			if (!added && typeId < current)
			{
				result[index++] = typeId;
				added = true;
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
		var index = 0;

		for (var i = 0; i < source.Length; i++)
		{
			if (source[i] == typeId) continue;
			result[index++] = source[i];
		}

		return result;
	}

	private static void ClearComponentSlot(Chunk chunk, int slot)
	{
		chunk.ClearSlot(slot);
	}

	private (Chunk targetChunk, int targetSlot) MoveEntityCore(
		Entity entity,
		Archetype source,
		EntityLocation sourceLocation,
		Archetype target)
	{
		var sourceChunk = source.Chunks[sourceLocation.ChunkIndex];
		var targetChunk = target.GetOrCreateChunkWithSpace(out int targetChunkIndex);
		int targetSlot = targetChunk.Count++;

		targetChunk.Entities[targetSlot] = entity;
		ClearComponentSlot(targetChunk, targetSlot);
		_entityManager.SetLocation(entity, new(target.Id, targetChunkIndex, targetSlot));

		for (var i = 0; i < source.TypeIds.Length; i++)
		{
			int typeId = source.TypeIds[i];
			int targetIndex = target.GetTypeIndex(typeId);
			if (targetIndex < 0) continue;

			sourceChunk.CopyValueTo(i, sourceLocation.SlotIndex, targetChunk, targetIndex, targetSlot);
		}

		RemoveEntityFromArchetype(source, sourceLocation, entity, clearLocation: false);
		return (targetChunk, targetSlot);
	}

	private Archetype CreateArchetypeInternal(int[] typeIds, Type[] types)
	{
		int chunkCapacity = ResolveChunkCapacity(types);
		var archetype = new Archetype(this, _archetypes.Count, typeIds, types, chunkCapacity);
		_archetypes.Add(archetype);
		_archetypesByKey[new(typeIds)] = archetype;
		OnArchetypeCreated(archetype);
		return archetype;
	}

	private int ResolveChunkCapacity(Type[] componentTypes)
	{
		if (_chunkCapacityOverride > 0)
			return _chunkCapacityOverride;

		int bytesPerEntity = 0;
		if (componentTypes.Length == 0)
		{
			bytesPerEntity = ComponentSizeEstimator.GetSizeInBytes(typeof(Entity));
		}
		else
		{
			for (var i = 0; i < componentTypes.Length; i++)
				bytesPerEntity += ComponentSizeEstimator.GetSizeInBytes(componentTypes[i]);
		}

		if (bytesPerEntity <= 0)
			bytesPerEntity = 1;

		int capacity = _chunkSizeInBytes / bytesPerEntity;
		return Math.Max(1, capacity);
	}

	public Archetype GetOrCreateArchetype(params Type[] componentTypes)
	{
		if (componentTypes is null) throw new ArgumentNullException(nameof(componentTypes));
		if (componentTypes.Length == 0) return EmptyArchetype;

		var typeIds = new int[componentTypes.Length];
		var types = new Type[componentTypes.Length];

		for (var i = 0; i < componentTypes.Length; i++)
		{
			var type = componentTypes[i] ?? throw new ArgumentNullException(nameof(componentTypes));
			typeIds[i] = ComponentTypeRegistry.GetOrCreate(type);
			types[i] = type;
		}

		Array.Sort(typeIds, types);
		var uniqueCount = 0;
		for (var i = 0; i < typeIds.Length; i++)
		{
			if (i > 0 && typeIds[i] == typeIds[i - 1]) continue;
			typeIds[uniqueCount] = typeIds[i];
			types[uniqueCount] = types[i];
			uniqueCount++;
		}

		if (uniqueCount != typeIds.Length)
		{
			Array.Resize(ref typeIds, uniqueCount);
			Array.Resize(ref types, uniqueCount);
		}

		return GetOrCreateArchetype(typeIds, types);
	}

	public Archetype GetOrCreateArchetype<T1>() where T1 : struct, IComponent => GetOrCreateArchetype(typeof(T1));
	public Archetype GetOrCreateArchetype<T1, T2>() where T1 : struct, IComponent where T2 : struct, IComponent => GetOrCreateArchetype(typeof(T1), typeof(T2));
	public Archetype GetOrCreateArchetype<T1, T2, T3>() where T1 : struct, IComponent where T2 : struct, IComponent where T3 : struct, IComponent => GetOrCreateArchetype(typeof(T1), typeof(T2), typeof(T3));
	public Archetype GetOrCreateArchetype<T1, T2, T3, T4>() where T1 : struct, IComponent where T2 : struct, IComponent where T3 : struct, IComponent where T4 : struct, IComponent => GetOrCreateArchetype(typeof(T1), typeof(T2), typeof(T3), typeof(T4));

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

	private bool TryGetComponentArray(Entity entity, int typeId, out Chunk chunk, out int slot, out int componentIndex)
	{
		var location = _entityManager.GetLocation(entity);
		if (!location.IsValid)
		{
			chunk = null!;
			slot = -1;
			componentIndex = -1;
			return false;
		}

		var archetype = _archetypes[location.ArchetypeId];
		componentIndex = archetype.GetTypeIndex(typeId);
		if (componentIndex < 0)
		{
			chunk = null!;
			slot = -1;
			return false;
		}

		chunk = archetype.Chunks[location.ChunkIndex];
		slot = location.SlotIndex;
		return true;
	}

	private bool TrySetComponentInPlace<T>(Entity entity, int typeId, in T component) where T : struct, IComponent
	{
		if (!TryGetComponentArray(entity, typeId, out var chunk, out int slot, out int componentIndex))
			return false;

		chunk.GetReference<T>(componentIndex, slot) = component;
		chunk.MarkChanged(componentIndex, _changeVersion);
		return true;
	}

	private void AddComponentWithMove<T>(Entity entity, int typeId, in T component) where T : struct, IComponent
	{
		var location = _entityManager.GetLocation(entity);
		var source = _archetypes[location.ArchetypeId];

		Archetype target;
		if (!source.TryGetAddEdge(typeId, out target!))
		{
			int[] newTypeIds = InsertTypeId(source.TypeIds, typeId);
			target = GetOrCreateArchetypeByTypeIds(newTypeIds);
			source.SetAddEdge(typeId, target);
		}

		(var targetChunk, int targetSlot) = MoveEntityCore(entity, source, location, target);
		int addedIndex = target.GetTypeIndex(typeId);
		targetChunk.GetReference<T>(addedIndex, targetSlot) = component;
		targetChunk.MarkChanged(addedIndex, _changeVersion);
	}

	private void AddEntityToArchetype(Entity entity, Archetype archetype)
	{
		var chunk = archetype.GetOrCreateChunkWithSpace(out int chunkIndex);
		int slot = chunk.Count++;
		chunk.Entities[slot] = entity;
		ClearComponentSlot(chunk, slot);
		_entityManager.SetLocation(entity, new(archetype.Id, chunkIndex, slot));
	}

	private void EnsureNotUpdating()
	{
		if (_isUpdating || HasActiveQueryIteration)
			throw new InvalidOperationException("Structural changes are not allowed during update or query iteration. Use CommandBuffer.");
	}

	private void MoveEntity(Entity entity, Archetype source, EntityLocation sourceLocation, Archetype target) =>
		MoveEntityCore(entity, source, sourceLocation, target);

	private void RemoveEntityFromArchetype(Entity entity)
	{
		var location = _entityManager.GetLocation(entity);
		if (!location.IsValid) return;

		var archetype = _archetypes[location.ArchetypeId];
		RemoveEntityFromArchetype(archetype, location, entity, clearLocation: true);
	}

	private void RemoveEntityFromArchetype(Archetype archetype, EntityLocation location, Entity removedEntity, bool clearLocation)
	{
		var chunk = archetype.Chunks[location.ChunkIndex];
		int slot = location.SlotIndex;
		int last = chunk.Count - 1;
		if (last < 0) return;

		if (slot != last)
		{
			var movedEntity = chunk.Entities[last];
			chunk.Entities[slot] = movedEntity;

			for (var i = 0; i < chunk.Columns.Length; i++)
				chunk.CopyValueTo(i, last, chunk, i, slot);

			_entityManager.SetLocation(movedEntity, new(archetype.Id, location.ChunkIndex, slot));
		}

		chunk.Entities[last] = default;
		chunk.ClearSlot(last);

		chunk.Count--;
		archetype.NotifyChunkFreed(location.ChunkIndex);

		if (clearLocation)
			_entityManager.SetLocation(removedEntity, EntityLocation.Empty);
	}

	private void BumpChangeVersion()
	{
		unchecked
		{
			_changeVersion++;
			if (_changeVersion == 0)
				_changeVersion = 1;
		}
	}
}
