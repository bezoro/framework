using Bezoro.ECS.Abstractions;
using Bezoro.ECS.Internal;
using Bezoro.ECS.Options;
using Bezoro.ECS.Types;

namespace Bezoro.ECS.Services;

/// <summary>
///     Provides the main interface to the Entity Component System (ECS), allowing for management of entities,
///     components, and systems within an isolated game world or simulation context.
/// </summary>
public class World : IWorld
{
	private readonly Dictionary<ArchetypeKey, Archetype> _archetypesByKey = new();
	private readonly EntityManager                       _entityManager   = new();
	private readonly List<Archetype>                     _archetypes      = [];
	private readonly SystemManager                       _systemManager;
	private readonly WorldOptions                        _options;
	private          bool                                _isUpdating;

	/// <summary>
	///     Initializes a new instance of the <see cref="World" /> class with default options.
	/// </summary>
	public World() : this(new()) { }

	/// <summary>
	///     Initializes a new instance of the <see cref="World" /> class.
	/// </summary>
	/// <param name="options">The world configuration.</param>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="options" /> is null.</exception>
	/// <exception cref="ArgumentOutOfRangeException">Thrown when the chunk capacity is invalid.</exception>
	public World(WorldOptions options)
	{
		if (options is null) throw new ArgumentNullException(nameof(options));

		if (options.ChunkCapacity <= 0)
			throw new ArgumentOutOfRangeException(nameof(options.ChunkCapacity), "Chunk capacity must be positive.");

		if (options.MaxDegreeOfParallelism <= 0)
			throw new ArgumentOutOfRangeException(
				nameof(options.MaxDegreeOfParallelism),
				"Parallelism must be positive."
			);

		_options       = options;
		_systemManager = new(options.MaxDegreeOfParallelism);
		EmptyArchetype = CreateArchetypeInternal([], []);
	}

	/// <summary>
	///     Gets the default empty archetype.
	/// </summary>
	internal Archetype EmptyArchetype { get; }

	internal int MaxDegreeOfParallelism => _options.MaxDegreeOfParallelism;

	internal IReadOnlyList<Archetype> Archetypes => _archetypes;

	/// <summary>
	///     Creates or retrieves an archetype for the specified component types.
	/// </summary>
	/// <param name="componentTypes">The component types that form the archetype.</param>
	/// <returns>The matching archetype.</returns>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="componentTypes" /> is null.</exception>
	/// <exception cref="ArgumentException">Thrown when a component type is invalid.</exception>
	public Archetype GetOrCreateArchetype(params Type[] componentTypes)
	{
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

		if (uniqueCount == 0) return EmptyArchetype;

		if (uniqueCount != typeIds.Length)
		{
			Array.Resize(ref typeIds, uniqueCount);
			Array.Resize(ref types,   uniqueCount);
		}

		return GetOrCreateArchetype(typeIds, types);
	}

	/// <summary>
	///     Creates or retrieves an archetype for component type <typeparamref name="T1" />.
	/// </summary>
	public Archetype GetOrCreateArchetype<T1>()
		where T1 : struct, IComponent =>
		GetOrCreateArchetype(typeof(T1));

	/// <summary>
	///     Creates or retrieves an archetype for component types <typeparamref name="T1" /> and <typeparamref name="T2" />.
	/// </summary>
	public Archetype GetOrCreateArchetype<T1, T2>()
		where T1 : struct, IComponent
		where T2 : struct, IComponent =>
		GetOrCreateArchetype(typeof(T1), typeof(T2));

	/// <summary>
	///     Creates or retrieves an archetype for component types <typeparamref name="T1" />, <typeparamref name="T2" />,
	///     and <typeparamref name="T3" />.
	/// </summary>
	public Archetype GetOrCreateArchetype<T1, T2, T3>()
		where T1 : struct, IComponent
		where T2 : struct, IComponent
		where T3 : struct, IComponent =>
		GetOrCreateArchetype(typeof(T1), typeof(T2), typeof(T3));

	/// <summary>
	///     Creates or retrieves an archetype for component types <typeparamref name="T1" />, <typeparamref name="T2" />,
	///     <typeparamref name="T3" />, and <typeparamref name="T4" />.
	/// </summary>
	public Archetype GetOrCreateArchetype<T1, T2, T3, T4>()
		where T1 : struct, IComponent
		where T2 : struct, IComponent
		where T3 : struct, IComponent
		where T4 : struct, IComponent =>
		GetOrCreateArchetype(typeof(T1), typeof(T2), typeof(T3), typeof(T4));

	/// <summary>
	///     Determines whether the specified entity has a component of type <typeparamref name="T" />.
	/// </summary>
	/// <typeparam name="T">The type of component to check for.</typeparam>
	/// <param name="entity">The entity to query.</param>
	/// <returns>True if the entity has the component; otherwise, false.</returns>
	public bool HasComponent<T>(Entity entity) where T : struct, IComponent
	{
		_entityManager.EnsureAlive(entity);
		int typeId = ComponentTypeRegistry.GetOrCreate<T>();
		return HasComponent(typeId, entity);
	}

	/// <summary>
	///     Determines whether the specified entity is currently alive.
	/// </summary>
	/// <param name="entity">The entity to query.</param>
	/// <returns>True if the entity is alive; otherwise, false.</returns>
	public bool IsAlive(Entity entity) =>
		_entityManager.IsAlive(entity);

	/// <summary>
	///     Attempts to retrieve the component of type <typeparamref name="T" /> attached to the specified entity.
	/// </summary>
	/// <typeparam name="T">The type of component to retrieve.</typeparam>
	/// <param name="entity">The entity from which to retrieve the component.</param>
	/// <param name="component">The component instance if found.</param>
	/// <returns><c>true</c> if the component exists; otherwise, <c>false</c>.</returns>
	public bool TryGetComponent<T>(Entity entity, out T component) where T : struct, IComponent
	{
		_entityManager.EnsureAlive(entity);

		int typeId = ComponentTypeRegistry.GetOrCreate<T>();
		if (!TryGetComponentArray(entity, typeId, out var chunk, out int slot, out int componentIndex))
		{
			component = default;
			return false;
		}

		component = ((T[])chunk.Components[componentIndex])[slot];
		return true;
	}

	/// <summary>
	///     Creates a new command buffer for deferred structural changes.
	/// </summary>
	public CommandBuffer CreateCommandBuffer() => new(this);

	/// <summary>
	///     Creates a new entity within this world.
	/// </summary>
	/// <returns>A new <see cref="Entity" /> instance with a unique identifier.</returns>
	/// <exception cref="InvalidOperationException">Thrown when structural changes are attempted during update.</exception>
	public Entity CreateEntity()
	{
		EnsureNotUpdating();
		var entity = _entityManager.CreateEntity();
		AddEntityToArchetype(entity, EmptyArchetype);
		return entity;
	}

	/// <summary>
	///     Creates a new entity within the specified archetype.
	/// </summary>
	/// <param name="archetype">The archetype the entity should belong to.</param>
	/// <returns>A new <see cref="Entity" /> instance with a unique identifier.</returns>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="archetype" /> is null.</exception>
	/// <exception cref="InvalidOperationException">Thrown when the archetype belongs to a different world.</exception>
	/// <exception cref="InvalidOperationException">Thrown when structural changes are attempted during update.</exception>
	public Entity CreateEntity(Archetype archetype)
	{
		if (archetype is null) throw new ArgumentNullException(nameof(archetype));

		EnsureNotUpdating();
		EnsureOwnedArchetype(archetype);

		var entity = _entityManager.CreateEntity();
		AddEntityToArchetype(entity, archetype);
		return entity;
	}

	/// <summary>
	///     Creates a query builder over entities.
	/// </summary>
	public Query Query() =>
		new(this, null, []);

	/// <summary>
	///     Creates a query builder restricted to a specific archetype.
	/// </summary>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="archetype" /> is null.</exception>
	/// <exception cref="InvalidOperationException">Thrown when the archetype belongs to a different world.</exception>
	public Query Query(Archetype archetype)
	{
		if (archetype is null) throw new ArgumentNullException(nameof(archetype));

		EnsureOwnedArchetype(archetype);
		return new(this, archetype, []);
	}

	/// <summary>
	///     Retrieves the component of type <typeparamref name="T" /> attached to the specified entity.
	/// </summary>
	/// <typeparam name="T">The type of component to retrieve.</typeparam>
	/// <param name="entity">The entity from which to retrieve the component.</param>
	/// <returns>The component of type <typeparamref name="T" />.</returns>
	/// <exception cref="KeyNotFoundException">Thrown when the component does not exist.</exception>
	public T GetComponent<T>(Entity entity) where T : struct, IComponent
	{
		if (TryGetComponent(entity, out T component))
			return component;

		throw new KeyNotFoundException($"Component of type {typeof(T).Name} not found for entity {entity.Id}.");
	}

	/// <summary>
	///     Adds or replaces a component of type <typeparamref name="T" /> on the specified entity.
	/// </summary>
	/// <typeparam name="T">The type of component to add or replace.</typeparam>
	/// <param name="entity">The entity to modify.</param>
	/// <param name="component">The component instance to set.</param>
	public void AddComponent<T>(Entity entity, in T component) where T : struct, IComponent =>
		SetComponent(entity, in component);

	/// <summary>
	///     Destroys the specified entity, removing all its components and recycling its ID.
	/// </summary>
	/// <param name="entity">The entity to destroy.</param>
	/// <exception cref="InvalidOperationException">Thrown when the entity is not alive.</exception>
	/// <exception cref="InvalidOperationException">Thrown when structural changes are attempted during update.</exception>
	public void DestroyEntity(Entity entity)
	{
		EnsureNotUpdating();
		DestroyEntityInternal(entity);
	}

	/// <summary>
	///     Registers a system with the world to participate in update cycles.
	/// </summary>
	/// <param name="system">The system to register.</param>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="system" /> is null.</exception>
	/// <exception cref="InvalidOperationException">Thrown when the system is already registered.</exception>
	public void RegisterSystem(ISystem system) =>
		_systemManager.RegisterSystem(system);

	/// <summary>
	///     Removes a component of type <typeparamref name="T" /> from the specified entity.
	/// </summary>
	/// <typeparam name="T">The type of component to remove.</typeparam>
	/// <param name="entity">The entity from which to remove the component.</param>
	/// <exception cref="InvalidOperationException">Thrown when the entity is not alive.</exception>
	/// <exception cref="InvalidOperationException">Thrown when structural changes are attempted during update.</exception>
	public void RemoveComponent<T>(Entity entity) where T : struct, IComponent
	{
		EnsureNotUpdating();
		_entityManager.EnsureAlive(entity);

		int typeId = ComponentTypeRegistry.GetOrCreate<T>();
		RemoveComponentById(entity, typeId);
	}

	/// <summary>
	///     Sets a component of type <typeparamref name="T" /> on the specified entity.
	///     If the component does not exist, it is added.
	/// </summary>
	/// <typeparam name="T">The type of component to set.</typeparam>
	/// <param name="entity">The entity to modify.</param>
	/// <param name="component">The component instance to set.</param>
	/// <exception cref="InvalidOperationException">Thrown when the entity is not alive.</exception>
	/// <exception cref="InvalidOperationException">Thrown when structural changes are attempted during update.</exception>
	public void SetComponent<T>(Entity entity, in T component) where T : struct, IComponent
	{
		_entityManager.EnsureAlive(entity);

		int typeId = ComponentTypeRegistry.GetOrCreate<T>();
		if (TrySetComponentInPlace(entity, typeId, component)) return;

		if (_isUpdating)
			throw new InvalidOperationException("Structural changes are not allowed during update. Use CommandBuffer.");

		AddComponentWithMove(entity, typeId, component);
	}

	/// <summary>
	///     Updates all registered systems in this world.
	/// </summary>
	/// <param name="deltaTime">The elapsed time since the last update.</param>
	public void Update(float deltaTime)
	{
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

	/// <summary>
	///     Updates all registered systems with a default delta time.
	/// </summary>
	public void Update() =>
		Update(0f);

	internal Entity CreateEntityInternal(Archetype archetype)
	{
		var entity = _entityManager.CreateEntity();
		AddEntityToArchetype(entity, archetype);
		return entity;
	}

	internal void ApplySetComponentInternal(Entity entity, int typeId, object? component)
	{
		if (component is null)
			throw new ArgumentNullException(nameof(component), "Component value cannot be null.");

		_entityManager.EnsureAlive(entity);

		if (TrySetComponentBoxed(entity, typeId, component)) return;

		AddComponentWithMoveBoxed(entity, typeId, component);
	}

	internal void DestroyEntityInternal(Entity entity)
	{
		_entityManager.EnsureAlive(entity);
		RemoveEntityFromArchetype(entity);
		_entityManager.DestroyEntity(entity);
	}

	internal void EnsureOwnedArchetype(Archetype archetype)
	{
		if (!ReferenceEquals(archetype.Owner, this))
			throw new InvalidOperationException("Archetype belongs to a different world.");
	}

	internal void RemoveComponentById(Entity entity, int typeId)
	{
		if (!_entityManager.IsAlive(entity)) return;

		var location = _entityManager.GetLocation(entity);
		if (!location.IsValid) return;

		var source = _archetypes[location.ArchetypeId];
		if (source.GetTypeIndex(typeId) < 0) return;

		int[] newTypeIds = RemoveTypeId(source.TypeIds, typeId);
		var   target     = GetOrCreateArchetypeByTypeIds(newTypeIds);

		MoveEntity(entity, source, location, target, -1, null);
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

	private static void ClearComponentSlot(Chunk chunk, int slot)
	{
		for (var i = 0; i < chunk.Components.Length; i++)
			Array.Clear(chunk.Components[i], slot, 1);
	}

	private Archetype CreateArchetypeInternal(int[] typeIds, Type[] types)
	{
		var archetype = new Archetype(this, _archetypes.Count, typeIds, types, _options.ChunkCapacity);
		_archetypes.Add(archetype);
		_archetypesByKey.Add(new(typeIds), archetype);
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

	private bool TryGetComponentArray(Entity entity, int typeId, out Chunk chunk, out int slot, out int componentIndex)
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

		chunk = archetype.Chunks[location.ChunkIndex];
		slot  = location.SlotIndex;
		return true;
	}

	private bool TrySetComponentBoxed(Entity entity, int typeId, object component)
	{
		if (!TryGetComponentArray(entity, typeId, out var chunk, out int slot, out int componentIndex))
			return false;

		chunk.Components[componentIndex].SetValue(component, slot);
		return true;
	}

	private bool TrySetComponentInPlace<T>(Entity entity, int typeId, in T component) where T : struct, IComponent
	{
		if (!TryGetComponentArray(entity, typeId, out var chunk, out int slot, out int componentIndex))
			return false;

		((T[])chunk.Components[componentIndex])[slot] = component;
		return true;
	}

	private void AddComponentWithMove<T>(Entity entity, int typeId, in T component) where T : struct, IComponent
	{
		var location = _entityManager.GetLocation(entity);
		var source   = _archetypes[location.ArchetypeId];

		int[] newTypeIds = InsertTypeId(source.TypeIds, typeId);
		var   target     = GetOrCreateArchetypeByTypeIds(newTypeIds);

		MoveEntity(entity, source, location, target, typeId, component);
	}

	private void AddComponentWithMoveBoxed(Entity entity, int typeId, object component)
	{
		var location = _entityManager.GetLocation(entity);
		var source   = _archetypes[location.ArchetypeId];

		int[] newTypeIds = InsertTypeId(source.TypeIds, typeId);
		var   target     = GetOrCreateArchetypeByTypeIds(newTypeIds);

		MoveEntity(entity, source, location, target, typeId, component);
	}

	private void AddEntityToArchetype(Entity entity, Archetype archetype)
	{
		var chunk = archetype.GetOrCreateChunkWithSpace(out int chunkIndex);
		int slot  = chunk.Count++;
		chunk.Entities[slot] = entity;
		ClearComponentSlot(chunk, slot);
		_entityManager.SetLocation(entity, new(archetype.Id, chunkIndex, slot));
	}

	private void EnsureNotUpdating()
	{
		if (_isUpdating)
			throw new InvalidOperationException("Structural changes are not allowed during update. Use CommandBuffer.");
	}

	private void MoveEntity(
		Entity         entity,
		Archetype      source,
		EntityLocation sourceLocation,
		Archetype      target,
		int            addedTypeId,
		object?        addedComponent)
	{
		var sourceChunk = source.Chunks[sourceLocation.ChunkIndex];
		var targetChunk = target.GetOrCreateChunkWithSpace(out int targetChunkIndex);
		int targetSlot  = targetChunk.Count++;

		targetChunk.Entities[targetSlot] = entity;
		ClearComponentSlot(targetChunk, targetSlot);
		_entityManager.SetLocation(entity, new(target.Id, targetChunkIndex, targetSlot));

		for (var i = 0; i < source.TypeIds.Length; i++)
		{
			int typeId      = source.TypeIds[i];
			int targetIndex = target.GetTypeIndex(typeId);
			if (targetIndex < 0) continue;

			var sourceArray = sourceChunk.Components[i];
			var targetArray = targetChunk.Components[targetIndex];
			Array.Copy(sourceArray, sourceLocation.SlotIndex, targetArray, targetSlot, 1);
		}

		if (addedTypeId >= 0 && addedComponent is { })
		{
			int addedIndex = target.GetTypeIndex(addedTypeId);
			if (addedIndex >= 0)
				targetChunk.Components[addedIndex].SetValue(addedComponent, targetSlot);
		}

		RemoveEntityFromArchetype(source, sourceLocation, entity, false);
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
		var chunk = archetype.Chunks[location.ChunkIndex];
		int slot  = location.SlotIndex;
		int last  = chunk.Count - 1;

		if (last < 0) return;

		if (slot != last)
		{
			var movedEntity = chunk.Entities[last];
			chunk.Entities[slot] = movedEntity;

			for (var i = 0; i < chunk.Components.Length; i++)
				Array.Copy(chunk.Components[i], last, chunk.Components[i], slot, 1);

			_entityManager.SetLocation(movedEntity, new(archetype.Id, location.ChunkIndex, slot));
		}

		chunk.Entities[last] = default;
		for (var i = 0; i < chunk.Components.Length; i++)
			Array.Clear(chunk.Components[i], last, 1);

		chunk.Count--;
		if (clearLocation)
			_entityManager.SetLocation(removedEntity, EntityLocation.Empty);
	}
}
