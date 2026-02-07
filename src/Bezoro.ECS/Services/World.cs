using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using Bezoro.ECS.Abstractions;
using Bezoro.ECS.Generated;
using Bezoro.ECS.Internal;
using Bezoro.ECS.Options;
using Bezoro.ECS.Types;

namespace Bezoro.ECS.Services;

/// <summary>
///     Main entry point for ECS state management.
/// </summary>
public sealed class World : IWorld, IDisposable
{
	private const           int                   SNAPSHOT_FORMAT_VERSION = 1;
	private static readonly byte[]                SnapshotMagic = [(byte)'B', (byte)'Z', (byte)'E', (byte)'C'];
	private static readonly JsonSerializerOptions SnapshotJsonOptions = new() { IncludeFields = true };

	private readonly Dictionary<ArchetypeKey, Archetype>           _archetypesByKey     = new();
	private readonly Dictionary<int, List<Action<Entity, object>>> _onRemoveInObservers = new();
	private readonly Dictionary<int, List<Delegate>>               _onAddObservers      = new();
	private readonly Dictionary<int, List<Delegate>>               _onAddRefObservers   = new();
	private readonly Dictionary<QueryCacheKey, QueryCacheEntry>    _queryCache          = new();
	private readonly Dictionary<Type, object>                      _resources           = new();
	private readonly EntityManager                                 _entityManager;
	private readonly int                                           _chunkCapacityOverride;
	private readonly int                                           _chunkSizeInBytes;
	private readonly List<Archetype>                               _archetypes = [];
	private readonly SystemManager                                 _systemManager;

	private          bool _disposed;
	private volatile bool _isUpdating;
	private          int  _activeCommandPlaybacks;
	private          int  _activeQueryIterations;

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

	public int    EntityCount => _entityManager.AliveCount;
	public string Name        { get; }

	internal Archetype EmptyArchetype { get; }

	internal bool HasActiveQueryIteration => Volatile.Read(ref _activeQueryIterations) > 0;

	internal bool IsPlayingBackCommands => Volatile.Read(ref _activeCommandPlaybacks) > 0;

	internal bool                  IsUpdating            => _isUpdating;
	internal ComponentTypeRegistry ComponentTypeRegistry { get; } = new();

	internal int MaxDegreeOfParallelism { get; }
	internal int QueryCacheEntryCount   => _queryCache.Count;

	internal int SchedulerPlanBuildCount => _systemManager.PlanBuildCount;

	internal int WorldId { get; }

	internal IReadOnlyList<Archetype> Archetypes => _archetypes;

	internal uint ChangeVersion { get; private set; }

	public static World Deserialize(byte[] bytes)
	{
		if (bytes is null) throw new ArgumentNullException(nameof(bytes));

		try
		{
			using var stream = new MemoryStream(bytes, false);
			using var reader = new BinaryReader(stream, Encoding.UTF8, true);

			byte[] magic = ReadBytesExact(reader, SnapshotMagic.Length);
			for (var i = 0; i < SnapshotMagic.Length; i++)
			{
				if (magic[i] != SnapshotMagic[i])
					throw new InvalidOperationException("Invalid snapshot payload: unsupported header.");
			}

			int version = reader.ReadInt32();
			if (version != SNAPSHOT_FORMAT_VERSION)
				throw new InvalidOperationException($"Invalid snapshot payload: unsupported version '{version}'.");

			var world                = new World();
			var entityMap            = new Dictionary<(int Id, int Version), Entity>();
			var pendingRelationships = new List<(Entity Source, Type RelationType, int TargetId, int TargetVersion)>();
			int archetypeCount       = reader.ReadInt32();
			if (archetypeCount < 0)
				throw new InvalidOperationException("Invalid snapshot payload: archetype count cannot be negative.");

			for (var archetypeIndex = 0; archetypeIndex < archetypeCount; archetypeIndex++)
			{
				int componentTypeCount = reader.ReadInt32();
				if (componentTypeCount < 0)
					throw new InvalidOperationException(
						"Invalid snapshot payload: component type count cannot be negative."
					);

				var componentTypes = new Type[componentTypeCount];
				var payloadKinds   = new SnapshotPayloadKind[componentTypeCount];
				for (var typeIndex = 0; typeIndex < componentTypeCount; typeIndex++)
				{
					string typeName = reader.ReadString();
					var type = Type.GetType(typeName, true) ??
							   throw new InvalidOperationException(
								   $"Snapshot type '{typeName}' could not be resolved."
							   );

					ulong expectedLayoutHash = reader.ReadUInt64();
					ulong actualLayoutHash   = ComputeLayoutHash(type);
					if (expectedLayoutHash != actualLayoutHash)
						throw new InvalidOperationException(
							$"Invalid snapshot payload: layout mismatch for '{type.FullName}'."
						);

					var payloadKind = (SnapshotPayloadKind)reader.ReadByte();
					if (payloadKind != GetSnapshotPayloadKind(type))
						throw new InvalidOperationException(
							$"Invalid snapshot payload: storage kind mismatch for '{type.FullName}'."
						);

					componentTypes[typeIndex] = type;
					payloadKinds[typeIndex]   = payloadKind;
				}

				int relationshipCount = reader.ReadInt32();
				if (relationshipCount < 0)
					throw new InvalidOperationException(
						"Invalid snapshot payload: relationship count cannot be negative."
					);

				var relationshipDescriptors =
					new (Type RelationType, int TargetId, int TargetVersion)[relationshipCount];

				for (var relationshipIndex = 0; relationshipIndex < relationshipCount; relationshipIndex++)
				{
					string relationTypeName = reader.ReadString();
					var relationType = Type.GetType(relationTypeName, true) ??
									   throw new InvalidOperationException(
										   $"Snapshot relation type '{relationTypeName}' could not be resolved."
									   );

					int targetId      = reader.ReadInt32();
					int targetVersion = reader.ReadInt32();
					_                                          = reader.ReadInt32(); // reserved in snapshot v1
					relationshipDescriptors[relationshipIndex] = (relationType, targetId, targetVersion);
				}

				var archetype = componentTypes.Length == 0
									? world.EmptyArchetype
									: world.GetOrCreateArchetype(componentTypes);

				int entityCount = reader.ReadInt32();
				if (entityCount < 0)
					throw new InvalidOperationException("Invalid snapshot payload: entity count cannot be negative.");

				var snapshotEntities = new (int Id, int Version)[entityCount];
				for (var entityIndex = 0; entityIndex < entityCount; entityIndex++)
					snapshotEntities[entityIndex] = (reader.ReadInt32(), reader.ReadInt32());

				var entities = new Entity[entityCount];
				for (var entityIndex = 0; entityIndex < entityCount; entityIndex++)
				{
					entities[entityIndex] = world.CreateEntityInternal(archetype);
					var snapshotEntity = snapshotEntities[entityIndex];
					if (!entityMap.TryAdd(snapshotEntity, entities[entityIndex]))
						throw new InvalidOperationException(
							$"Invalid snapshot payload: duplicate entity mapping for id '{snapshotEntity.Id}:{snapshotEntity.Version}'."
						);
				}

				for (var entityIndex = 0; entityIndex < entityCount; entityIndex++)
				{
					var entity = entities[entityIndex];
					for (var typeIndex = 0; typeIndex < componentTypes.Length; typeIndex++)
					{
						int length = reader.ReadInt32();
						if (length < 0)
							throw new InvalidOperationException(
								"Invalid snapshot payload: component payload length cannot be negative."
							);

						byte[] payload = ReadBytesExact(reader, length);
						var    componentType = componentTypes[typeIndex];
						object value = DeserializeSnapshotValue(componentType, payloadKinds[typeIndex], payload);
						int    typeId = world.GetOrCreateComponentTypeId(componentType);
						if (!world.TryGetComponentArray(
								entity, typeId, out var chunk, out int slot, out int componentIndex
							))
							throw new InvalidOperationException(
								$"Invalid snapshot payload: missing component slot for '{componentType.FullName}'."
							);

						chunk.SetValue(componentIndex, slot, value);
					}

					for (var relationshipIndex =
							 0;
						 relationshipIndex < relationshipDescriptors.Length;
						 relationshipIndex++)
					{
						(var relationType, int targetId, int targetVersion) =
							relationshipDescriptors[relationshipIndex];

						pendingRelationships.Add(
							(entities[entityIndex], RelationType: relationType, TargetId: targetId,
							 TargetVersion: targetVersion)
						);
					}
				}
			}

			int resourceCount = reader.ReadInt32();
			if (resourceCount < 0)
				throw new InvalidOperationException("Invalid snapshot payload: resource count cannot be negative.");

			for (var resourceIndex = 0; resourceIndex < resourceCount; resourceIndex++)
			{
				string typeName = reader.ReadString();
				var type = Type.GetType(typeName, true) ??
						   throw new InvalidOperationException($"Snapshot type '{typeName}' could not be resolved.");

				ulong expectedLayoutHash = reader.ReadUInt64();
				ulong actualLayoutHash   = ComputeLayoutHash(type);
				if (expectedLayoutHash != actualLayoutHash)
					throw new InvalidOperationException(
						$"Invalid snapshot payload: layout mismatch for '{type.FullName}'."
					);

				var payloadKind = (SnapshotPayloadKind)reader.ReadByte();
				if (payloadKind != GetSnapshotPayloadKind(type))
					throw new InvalidOperationException(
						$"Invalid snapshot payload: storage kind mismatch for '{type.FullName}'."
					);

				int length = reader.ReadInt32();
				if (length < 0)
					throw new InvalidOperationException(
						"Invalid snapshot payload: resource payload length cannot be negative."
					);

				byte[] payload = ReadBytesExact(reader, length);
				object value   = DeserializeSnapshotValue(type, payloadKind, payload);
				world.SetResourceObject(type, value);
			}

			for (var i = 0; i < pendingRelationships.Count; i++)
			{
				(var source, var relationType, int targetId, int targetVersion) = pendingRelationships[i];
				var target = targetId == Entity.Wildcard.Id
								 ? Entity.Wildcard
								 : entityMap.TryGetValue(
									 (targetId, targetVersion), out var mappedTarget
								 )
									 ? mappedTarget
									 : throw new InvalidOperationException(
										   $"Invalid snapshot payload: relationship target '{targetId}:{targetVersion}' was not found."
									   );

				world.AddRelationshipObject(source, relationType, target);
			}

			return world;
		}
		catch (InvalidOperationException)
		{
			throw;
		}
		catch (Exception ex) when (ex is EndOfStreamException or IOException or JsonException or ArgumentException
										 or NotSupportedException)
		{
			throw new InvalidOperationException("Invalid snapshot payload.", ex);
		}
	}

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

		if (uniqueCount == typeIds.Length) return GetOrCreateArchetype(typeIds, types);

		Array.Resize(ref typeIds, uniqueCount);
		Array.Resize(ref types,   uniqueCount);

		return GetOrCreateArchetype(typeIds, types);
	}

	public Archetype GetOrCreateArchetype<T1>() where T1 : struct, IComponent => GetOrCreateArchetype(typeof(T1));

	public Archetype GetOrCreateArchetype<T1, T2>() where T1 : struct, IComponent where T2 : struct, IComponent =>
		GetOrCreateArchetype(typeof(T1), typeof(T2));

	public Archetype GetOrCreateArchetype<T1, T2, T3>()
		where T1 : struct, IComponent where T2 : struct, IComponent where T3 : struct, IComponent =>
		GetOrCreateArchetype(typeof(T1), typeof(T2), typeof(T3));

	public Archetype GetOrCreateArchetype<T1, T2, T3, T4>()
		where T1 : struct, IComponent
		where T2 : struct, IComponent
		where T3 : struct, IComponent
		where T4 : struct, IComponent => GetOrCreateArchetype(typeof(T1), typeof(T2), typeof(T3), typeof(T4));

	public bool Has<T>(Entity entity) where T : struct, IComponent
	{
		_entityManager.EnsureAlive(entity);
		int typeId = ComponentTypeRegistry.GetOrCreate<T>();
		return HasComponent(typeId, entity);
	}

	public bool IsAlive(Entity entity) => _entityManager.IsAlive(entity);

	public bool TryGet<T>(Entity entity, out T component) where T : struct, IComponent
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

	public byte[] Serialize()
	{
		using var stream = new MemoryStream();
		using var writer = new BinaryWriter(stream, Encoding.UTF8, true);

		writer.Write(SnapshotMagic);
		writer.Write(SNAPSHOT_FORMAT_VERSION);

		var archetypesToWrite = new List<Archetype>();
		for (var a = 0; a < _archetypes.Count; a++)
		{
			var archetype   = _archetypes[a];
			var entityCount = 0;
			for (var c = 0; c < archetype.Chunks.Count; c++)
				entityCount += archetype.Chunks[c].Count;

			if (entityCount > 0)
				archetypesToWrite.Add(archetype);
		}

		writer.Write(archetypesToWrite.Count);
		for (var a = 0; a < archetypesToWrite.Count; a++)
		{
			var archetype = archetypesToWrite[a];

			var serializedTypeIds = new List<int>();
			for (var i = 0; i < archetype.TypeIds.Length; i++)
			{
				int typeId = archetype.TypeIds[i];
				if (ComponentTypeRegistry.IsRelationship(typeId))
					continue;

				serializedTypeIds.Add(typeId);
			}

			writer.Write(serializedTypeIds.Count);
			for (var i = 0; i < serializedTypeIds.Count; i++)
			{
				int typeId = serializedTypeIds[i];
				var type   = ComponentTypeRegistry.GetType(typeId);
				writer.Write(type.AssemblyQualifiedName!);
				writer.Write(ComputeLayoutHash(type));
				writer.Write((byte)GetSnapshotPayloadKind(type));
			}

			var relationshipDescriptors = new List<RelationshipInfo>();
			for (var i = 0; i < archetype.TypeIds.Length; i++)
			{
				int typeId = archetype.TypeIds[i];
				if (!ComponentTypeRegistry.IsRelationship(typeId))
					continue;

				if (!ComponentTypeRegistry.TryGetRelationshipInfo(typeId, out var relationship))
					throw new InvalidOperationException(
						"Relationship type information is missing for snapshot serialization."
					);

				relationshipDescriptors.Add(relationship);
			}

			writer.Write(relationshipDescriptors.Count);
			for (var i = 0; i < relationshipDescriptors.Count; i++)
			{
				var relationship = relationshipDescriptors[i];
				writer.Write(relationship.RelationType.AssemblyQualifiedName!);
				writer.Write(relationship.Target.Id);
				writer.Write(relationship.Target.Version);
				// Snapshot v1 reserves the target world id field. Entity handles are now {id, version}.
				writer.Write(0);
			}

			var rows = new List<(Chunk Chunk, int Row)>();
			for (var c = 0; c < archetype.Chunks.Count; c++)
			{
				var chunk = archetype.Chunks[c];
				for (var row = 0; row < chunk.Count; row++)
					rows.Add((chunk, row));
			}

			writer.Write(rows.Count);
			for (var rowIndex = 0; rowIndex < rows.Count; rowIndex++)
			{
				var entry  = rows[rowIndex];
				var entity = entry.Chunk.Entities[entry.Row];
				writer.Write(entity.Id);
				writer.Write(entity.Version);
			}

			for (var rowIndex = 0; rowIndex < rows.Count; rowIndex++)
			{
				var entry = rows[rowIndex];
				for (var typeIndex = 0; typeIndex < serializedTypeIds.Count; typeIndex++)
				{
					int    typeId         = serializedTypeIds[typeIndex];
					int    componentIndex = archetype.GetTypeIndex(typeId);
					var    type           = ComponentTypeRegistry.GetType(typeId);
					var    payloadKind    = GetSnapshotPayloadKind(type);
					object value          = entry.Chunk.GetValue(componentIndex, entry.Row);
					byte[] payload        = SerializeSnapshotValue(type, value, payloadKind);
					writer.Write(payload.Length);
					writer.Write(payload);
				}
			}
		}

		writer.Write(_resources.Count);
		foreach ((var resourceType, object? resourceBox) in _resources)
		{
			var payloadKind = GetSnapshotPayloadKind(resourceType);
			var valueField = resourceBox.GetType().GetField("Value", BindingFlags.Instance | BindingFlags.Public) ??
							 throw new InvalidOperationException("Resource box does not expose a value field.");

			object value = valueField.GetValue(resourceBox) ??
						   throw new InvalidOperationException("Resource value cannot be null.");

			byte[] payload = SerializeSnapshotValue(resourceType, value, payloadKind);

			writer.Write(resourceType.AssemblyQualifiedName!);
			writer.Write(ComputeLayoutHash(resourceType));
			writer.Write((byte)payloadKind);
			writer.Write(payload.Length);
			writer.Write(payload);
		}

		writer.Flush();
		return stream.ToArray();
	}

	public CommandBuffer CreateCommandBuffer() => new(this);

	public Entity Spawn()
	{
		EnsureNotUpdating();
		return CreateEntityInternal(EmptyArchetype);
	}

	public Entity Spawn<T1>(in T1 component1) where T1 : struct, IComponent
	{
		EnsureNotUpdating();
		var archetype = GetOrCreateArchetype(typeof(T1));
		var entity    = CreateEntityInternal(archetype);
		Set(entity, in component1);
		return entity;
	}

	public Entity Spawn<T1, T2>(in T1 component1, in T2 component2)
		where T1 : struct, IComponent
		where T2 : struct, IComponent
	{
		EnsureNotUpdating();
		var archetype = GetOrCreateArchetype(typeof(T1), typeof(T2));
		var entity    = CreateEntityInternal(archetype);
		Set(entity, in component1);
		Set(entity, in component2);
		return entity;
	}

	public Entity Spawn<T1, T2, T3>(in T1 component1, in T2 component2, in T3 component3)
		where T1 : struct, IComponent
		where T2 : struct, IComponent
		where T3 : struct, IComponent
	{
		EnsureNotUpdating();
		var archetype = GetOrCreateArchetype(typeof(T1), typeof(T2), typeof(T3));
		var entity    = CreateEntityInternal(archetype);
		Set(entity, in component1);
		Set(entity, in component2);
		Set(entity, in component3);
		return entity;
	}

	public Entity Spawn<T1, T2, T3, T4>(in T1 component1, in T2 component2, in T3 component3, in T4 component4)
		where T1 : struct, IComponent
		where T2 : struct, IComponent
		where T3 : struct, IComponent
		where T4 : struct, IComponent
	{
		EnsureNotUpdating();
		var archetype = GetOrCreateArchetype(typeof(T1), typeof(T2), typeof(T3), typeof(T4));
		var entity    = CreateEntityInternal(archetype);
		Set(entity, in component1);
		Set(entity, in component2);
		Set(entity, in component3);
		Set(entity, in component4);
		return entity;
	}

	public IDisposable Observe<T>(Action<Entity, T> observer) where T : struct, IComponent
	{
		if (observer is null) throw new ArgumentNullException(nameof(observer));

		int typeId = ComponentTypeRegistry.GetOrCreate<T>();
		if (!_onAddObservers.TryGetValue(typeId, out var handlers))
		{
			handlers                = [];
			_onAddObservers[typeId] = handlers;
		}

		handlers.Add(observer);
		return new ObserverSubscription(() => RemoveObserver(_onAddObservers, typeId, observer));
	}

	public IDisposable ObserveAdd<T>(OnAddObserver<T> observer) where T : struct, IComponent
	{
		if (observer is null) throw new ArgumentNullException(nameof(observer));

		int typeId = ComponentTypeRegistry.GetOrCreate<T>();
		if (!_onAddRefObservers.TryGetValue(typeId, out var handlers))
		{
			handlers                   = [];
			_onAddRefObservers[typeId] = handlers;
		}

		handlers.Add(observer);
		return new ObserverSubscription(() => RemoveObserver(_onAddRefObservers, typeId, observer));
	}

	public IDisposable ObserveRemove<T>(OnRemoveObserver<T> observer) where T : struct, IComponent
	{
		if (observer is null) throw new ArgumentNullException(nameof(observer));

		int typeId = ComponentTypeRegistry.GetOrCreate<T>();
		if (!_onRemoveInObservers.TryGetValue(typeId, out var handlers))
		{
			handlers                     = [];
			_onRemoveInObservers[typeId] = handlers;
		}

		var wrapped = (Action<Entity, object>)((entity, value) =>
												  {
													  var typed = (T)value;
													  observer(entity, in typed);
												  });

		handlers.Add(wrapped);
		return new ObserverSubscription(() => RemoveObserver(_onRemoveInObservers, typeId, wrapped));
	}

	public Query Query() =>
		new(this, null, new([], [], [], [], [], null, Entity.None));

	public Query Query<T1>()
		where T1 : struct, IComponent =>
		Query().All<T1>();

	public Query Query<T1, T2>()
		where T1 : struct, IComponent
		where T2 : struct, IComponent =>
		Query().All<T1>().All<T2>();

	public Query Query<T1, T2, T3>()
		where T1 : struct, IComponent
		where T2 : struct, IComponent
		where T3 : struct, IComponent =>
		Query().All<T1>().All<T2>().All<T3>();

	public Query Query<T1, T2, T3, T4>()
		where T1 : struct, IComponent
		where T2 : struct, IComponent
		where T3 : struct, IComponent
		where T4 : struct, IComponent =>
		Query().All<T1>().All<T2>().All<T3>().All<T4>();

	public Query Query(Archetype archetype)
	{
		if (archetype is null) throw new ArgumentNullException(nameof(archetype));

		EnsureOwnedArchetype(archetype);
		return new(this, archetype, new([], [], [], [], [], null, Entity.None));
	}

	public ref T Get<T>(Entity entity) where T : struct, IComponent
	{
		_entityManager.EnsureAlive(entity);
		int typeId = ComponentTypeRegistry.GetOrCreate<T>();
		if (!TryGetComponentArray(entity, typeId, out var chunk, out int slot, out int componentIndex))
			throw new KeyNotFoundException($"Component of type {typeof(T).Name} not found for entity {entity.Id}.");

		return ref chunk.GetReference<T>(componentIndex, slot);
	}

	public ref T GetResource<T>() where T : notnull
	{
		if (!_resources.TryGetValue(typeof(T), out object? boxed))
			throw new KeyNotFoundException($"Resource of type {typeof(T).Name} was not found.");

		return ref ((ResourceBox<T>)boxed).Value;
	}

	public void Add<T>(Entity entity) where T : struct, IComponent
	{
		var component = default(T);
		Add(entity, in component);
	}

	public void Add<T>(Entity entity, in T component) where T : struct, IComponent
	{
		EnsureNotUpdating();
		int typeId = ComponentTypeRegistry.GetOrCreate<T>();
		ApplyAddComponentTyped(entity, typeId, in component);
	}

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

	public void AddSystem(ISystem system, Stage stage = Stage.Tick) =>
		_systemManager.RegisterSystem(this, system, stage);

	public void AddSystem<TSystem>(Stage stage = Stage.Tick)
		where TSystem : ISystem, new() =>
		AddSystem(new TSystem(), stage);

	public void Clear()
	{
		EnsureNotUpdating();

		for (var i = 0; i < _archetypes.Count; i++)
			_archetypes[i].ClearChunks();

		_entityManager.Clear();
		BumpChangeVersion();
	}

	public void Despawn(Entity entity)
	{
		EnsureNotUpdating();
		DestroyEntityInternal(entity);
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
		_onAddObservers.Clear();
		_onAddRefObservers.Clear();
		_onRemoveInObservers.Clear();
	}

	/// <summary>
	///     Updates systems registered to the <see cref="SystemLoopPhase.FixedTick" /> loop phase.
	/// </summary>
	/// <param name="deltaTime">Elapsed fixed-step time in seconds for this update.</param>
	public void FixedTick(float deltaTime)
	{
		RunPhase(SystemLoopPhase.FixedTick, deltaTime);
	}

	/// <summary>
	///     Updates systems registered to the <see cref="SystemLoopPhase.LateTick" /> loop phase.
	/// </summary>
	/// <param name="deltaTime">Elapsed time in seconds for this late update.</param>
	public void LateTick(float deltaTime)
	{
		RunPhase(SystemLoopPhase.LateTick, deltaTime);
	}

	public void Remove<T>(Entity entity) where T : struct, IComponent
	{
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
		if (_disposed) throw new ObjectDisposedException(nameof(World));

		BumpChangeVersion();
		_isUpdating = true;
		try
		{
			_systemManager.UpdatePhase(this, loopPhase, deltaTime);
		}
		finally
		{
			_isUpdating = false;
		}
	}

	public void Set<T>(Entity entity, in T component) where T : struct, IComponent
	{
		_entityManager.EnsureAlive(entity);
		int typeId = ComponentTypeRegistry.GetOrCreate<T>();
		if (TrySetComponentInPlace(entity, typeId, component))
		{
			RaiseOnAdd<T>(entity, typeId);
			BumpChangeVersion();
			MarkComponentChanged(entity, typeId);
			return;
		}

		if (_isUpdating || HasActiveQueryIteration)
			throw new InvalidOperationException(
				"Structural changes are not allowed during update or query iteration. Use CommandBuffer."
			);

		AddComponentWithMove(entity, typeId, component);
		RaiseOnAdd<T>(entity, typeId);
		BumpChangeVersion();
		MarkComponentChanged(entity, typeId);
	}

	public void SetResource<T>(T resource) where T : notnull
	{
		_resources[typeof(T)] = new ResourceBox<T>(resource);
	}

	/// <summary>
	///     Updates systems registered to the <see cref="SystemLoopPhase.Tick" /> loop phase.
	/// </summary>
	/// <param name="deltaTime">Elapsed time in seconds for this update.</param>
	public void Tick(float deltaTime)
	{
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
		BumpChangeVersion();
		return entity;
	}

	internal int GetOrCreateComponentTypeId<T>() where T : struct, IComponent =>
		ComponentTypeRegistry.GetOrCreate<T>();

	internal int GetOrCreateComponentTypeId(Type type) =>
		ComponentTypeRegistry.GetOrCreate(type);

	internal int GetOrCreateRelationshipTypeId(Type relationType, Entity target) =>
		ComponentTypeRegistry.GetOrCreateRelationship(relationType, target);

	internal IReadOnlyList<Archetype> GetOrCreateQueryMatches(QuerySpec spec)
	{
		var key = spec.CacheKey;
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

	internal void ApplyAddComponentTyped<T>(Entity entity, int typeId, in T component) where T : struct, IComponent
	{
		_entityManager.EnsureAlive(entity);

		if (HasComponent(typeId, entity))
			throw new InvalidOperationException(
				$"Entity {entity.Id} already has component {typeof(T).Name}. Use Set to update."
			);

		AddComponentWithMove(entity, typeId, in component);
		RaiseOnAdd<T>(entity, typeId);
		BumpChangeVersion();
		MarkComponentChanged(entity, typeId);
	}

	internal void ApplySetComponentTyped<T>(Entity entity, int typeId, in T component) where T : struct, IComponent
	{
		_entityManager.EnsureAlive(entity);

		if (TrySetComponentInPlace(entity, typeId, in component))
		{
			RaiseOnAdd<T>(entity, typeId);
			BumpChangeVersion();
			MarkComponentChanged(entity, typeId);
			return;
		}

		AddComponentWithMove(entity, typeId, in component);
		RaiseOnAdd<T>(entity, typeId);
		BumpChangeVersion();
		MarkComponentChanged(entity, typeId);
	}

	internal void DestroyEntityInternal(Entity entity)
	{
		_entityManager.EnsureAlive(entity);
		List<(int TypeId, object Value)>? removedComponents = null;
		if (_onRemoveInObservers.Count > 0)
			removedComponents = CaptureRemovedComponents(entity);

		RemoveEntityFromArchetype(entity);
		_entityManager.DestroyEntity(entity);

		if (removedComponents is { })
			for (var i = 0; i < removedComponents.Count; i++)
			{
				(int typeId, object value) = removedComponents[i];
				RaiseOnRemove(entity, typeId, value);
			}

		BumpChangeVersion();
	}

	internal void EnsureEntityAlive(Entity entity) =>
		_entityManager.EnsureAlive(entity);

	internal void EnsureOwnedArchetype(Archetype archetype)
	{
		if (!ReferenceEquals(archetype.Owner, this))
			throw new InvalidOperationException("Archetype belongs to a different world.");
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
		object removedValue = sourceChunk.GetValue(sourceIndex, sourceSlotIndex);

		if (!source.TryGetRemoveEdge(typeId, out var target))
		{
			int[] newTypeIds = RemoveTypeId(source.TypeIds, typeId);
			target = GetOrCreateArchetypeByTypeIds(newTypeIds);
			source.SetRemoveEdge(typeId, target);
		}

		MoveEntity(entity, source, location, target);
		RaiseOnRemove(entity, typeId, removedValue);
		BumpChangeVersion();
	}

	private static byte[] ReadBytesExact(BinaryReader reader, int length)
	{
		byte[] bytes = reader.ReadBytes(length);
		if (bytes.Length != length)
			throw new InvalidOperationException("Invalid snapshot payload: unexpected end of data.");

		return bytes;
	}

	private static byte[] SerializeRawUnmanagedValue(Type type, object value)
	{
		if (!ComponentTypeTraits.IsUnmanaged(type))
			throw new InvalidOperationException($"Type '{type.FullName}' is not unmanaged.");

		int size    = Marshal.SizeOf(type);
		var payload = new byte[size];
		var handle  = GCHandle.Alloc(payload, GCHandleType.Pinned);
		try
		{
			Marshal.StructureToPtr(value, handle.AddrOfPinnedObject(), false);
			return payload;
		}
		finally
		{
			handle.Free();
		}
	}

	private static byte[] SerializeSnapshotValue(Type type, object value, SnapshotPayloadKind payloadKind) =>
		payloadKind switch
		{
			SnapshotPayloadKind.RawUnmanaged => SerializeRawUnmanagedValue(type, value),
			SnapshotPayloadKind.Json         => JsonSerializer.SerializeToUtf8Bytes(value, type, SnapshotJsonOptions),
			_                                => throw new ArgumentOutOfRangeException(nameof(payloadKind))
		};

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

	private static object DeserializeRawUnmanagedValue(Type type, byte[] payload)
	{
		if (!ComponentTypeTraits.IsUnmanaged(type))
			throw new InvalidOperationException($"Type '{type.FullName}' is not unmanaged.");

		int expectedSize = Marshal.SizeOf(type);
		if (payload.Length != expectedSize)
			throw new InvalidOperationException($"Snapshot payload size mismatch for '{type.FullName}'.");

		var handle = GCHandle.Alloc(payload, GCHandleType.Pinned);
		try
		{
			return Marshal.PtrToStructure(handle.AddrOfPinnedObject(), type) ??
				   throw new InvalidOperationException(
					   $"Snapshot value for '{type.FullName}' could not be materialized."
				   );
		}
		finally
		{
			handle.Free();
		}
	}

	private static object DeserializeSnapshotValue(Type type, SnapshotPayloadKind payloadKind, byte[] payload) =>
		payloadKind switch
		{
			SnapshotPayloadKind.RawUnmanaged => DeserializeRawUnmanagedValue(type, payload),
			SnapshotPayloadKind.Json => JsonSerializer.Deserialize(payload, type, SnapshotJsonOptions) ??
										throw new InvalidOperationException(
											$"Snapshot value for '{type.FullName}' could not be deserialized."
										),
			_ => throw new ArgumentOutOfRangeException(nameof(payloadKind))
		};

	private static SnapshotPayloadKind GetSnapshotPayloadKind(Type type) =>
		ComponentTypeTraits.IsUnmanaged(type) ? SnapshotPayloadKind.RawUnmanaged : SnapshotPayloadKind.Json;

	private static string CreateDefaultName() => $"World-{Guid.NewGuid():N}";

	private static ulong ComputeFnv1AHash64(string value)
	{
		const ulong OFFSET = 14695981039346656037;
		const ulong PRIME  = 1099511628211;
		ulong       hash   = OFFSET;

		byte[] bytes = Encoding.UTF8.GetBytes(value);
		for (var i = 0; i < bytes.Length; i++)
		{
			hash ^= bytes[i];
			hash *= PRIME;
		}

		return hash;
	}

	private static ulong ComputeLayoutHash(Type type)
	{
		var builder = new StringBuilder();
		AppendLayout(type, builder, new());
		return ComputeFnv1AHash64(builder.ToString());
	}

	private static void AppendLayout(Type type, StringBuilder builder, HashSet<Type> visited)
	{
		builder.Append("T=").Append(type.AssemblyQualifiedName).Append(';');
		if (!visited.Add(type))
			return;

		if (ComponentTypeTraits.IsUnmanaged(type))
			builder.Append("S=").Append(Marshal.SizeOf(type)).Append(';');

		var fields = type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
		Array.Sort(fields, static (left, right) => string.CompareOrdinal(left.Name, right.Name));
		for (var i = 0; i < fields.Length; i++)
		{
			var field = fields[i];
			builder.Append("F=").Append(field.Name).Append(':').Append(field.FieldType.AssemblyQualifiedName)
				   .Append(';');

			var fieldType = field.FieldType;
			if (fieldType.IsValueType && !fieldType.IsPrimitive && !fieldType.IsEnum)
				AppendLayout(fieldType, builder, visited);
		}
	}

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

	private static void RemoveObserver<TObserver>(
		Dictionary<int, List<TObserver>> observers,
		int                              typeId,
		TObserver                        observer)
		where TObserver : class
	{
		if (!observers.TryGetValue(typeId, out var handlers))
			return;

		handlers.Remove(observer);
		if (handlers.Count == 0)
			observers.Remove(typeId);
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
			int[] relationIds = ComponentTypeRegistry.GetRelationshipIds(spec.RelatedRelationType);
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

		DecomposeLocation(archetype, location, out int chunkIndex, out int slotIndex);
		chunk = archetype.Chunks[chunkIndex];
		slot  = slotIndex;
		return true;
	}

	private bool TrySetComponentInPlace<T>(Entity entity, int typeId, in T component) where T : struct, IComponent
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

		var bytesPerEntity = 0;
		if (componentTypes.Length == 0)
			bytesPerEntity = ComponentSizeEstimator.GetSizeInBytes(typeof(Entity));
		else
			for (var i = 0; i < componentTypes.Length; i++)
				bytesPerEntity += ComponentSizeEstimator.GetSizeInBytes(componentTypes[i]);

		if (bytesPerEntity <= 0)
			bytesPerEntity = 1;

		int capacity = _chunkSizeInBytes / bytesPerEntity;
		return Math.Max(1, capacity);
	}

	private List<(int TypeId, object Value)> CaptureRemovedComponents(Entity entity)
	{
		var location = _entityManager.GetLocation(entity);
		if (!location.IsValid)
			return [];

		var archetype = _archetypes[location.ArchetypeId];
		DecomposeLocation(archetype, location, out int chunkIndex, out int slotIndex);
		var chunk   = archetype.Chunks[chunkIndex];
		var removed = new List<(int TypeId, object Value)>(archetype.TypeIds.Length);
		for (var i = 0; i < archetype.TypeIds.Length; i++)
		{
			int typeId = archetype.TypeIds[i];
			if (ComponentTypeRegistry.IsRelationship(typeId))
				continue;

			removed.Add((typeId, chunk.GetValue(i, slotIndex)));
		}

		return removed;
	}

	private void AddComponentObject(Entity entity, Type componentType, object value)
	{
		int typeId = ComponentTypeRegistry.GetOrCreate(componentType);

		if (_isUpdating || HasActiveQueryIteration)
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

	private void AddComponentWithMove<T>(Entity entity, int typeId, in T component) where T : struct, IComponent
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

	private void AddRelationshipObject(Entity source, Type relationType, Entity target)
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
		if (_isUpdating || HasActiveQueryIteration)
			throw new InvalidOperationException(
				"Structural changes are not allowed during update or query iteration. Use CommandBuffer."
			);
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

	private void RaiseOnAdd<T>(Entity entity, int typeId) where T : struct, IComponent
	{
		if (!IsPlayingBackCommands)
			return;

		bool hasValueHandlers = _onAddObservers.TryGetValue(typeId, out var valueHandlers);
		bool hasRefHandlers   = _onAddRefObservers.TryGetValue(typeId, out var refHandlers);
		if (!hasValueHandlers && !hasRefHandlers)
			return;

		if (!TryGetComponentArray(entity, typeId, out var chunk, out int slot, out int componentIndex))
			return;

		ref var component = ref chunk.GetReference<T>(componentIndex, slot);

		if (hasRefHandlers)
			for (var i = 0; i < refHandlers!.Count; i++)
			{
				if (refHandlers[i] is OnAddObserver<T> observer)
					observer(entity, ref component);
			}

		if (!hasValueHandlers) return;

		{
			for (var i = 0; i < valueHandlers!.Count; i++)
			{
				if (valueHandlers[i] is Action<Entity, T> observer)
					observer(entity, component);
			}
		}
	}

	private void RaiseOnRemove(Entity entity, int typeId, object removedValue)
	{
		if (!IsPlayingBackCommands)
			return;

		if (_onRemoveInObservers.TryGetValue(typeId, out var inHandlers))
			for (var i = 0; i < inHandlers.Count; i++)
				inHandlers[i](entity, removedValue);
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
		archetype.NotifyChunkFreed(chunkIndex);

		if (clearLocation)
			_entityManager.SetLocation(removedEntity, EntityLocation.Empty);
	}

	private void SetResourceObject(Type type, object value)
	{
		var boxType = typeof(ResourceBox<>).MakeGenericType(type);
		_resources[type] = Activator.CreateInstance(boxType, value)!;
	}

	private sealed class ObserverSubscription(Action unsubscribe) : IDisposable
	{
		private Action? _unsubscribe = unsubscribe;

		public void Dispose()
		{
			var unsubscribe = Interlocked.Exchange(ref _unsubscribe, null);
			unsubscribe?.Invoke();
		}
	}

	private sealed class ResourceBox<T>(T value)
		where T : notnull
	{
		public T Value = value;
	}

	private sealed class SnapshotComponent
	{
		public string Json     { get; set; } = string.Empty;
		public string TypeName { get; set; } = string.Empty;
	}

	private sealed class SnapshotEntity
	{
		public List<SnapshotComponent> Components { get; set; } = [];
	}

	private enum SnapshotPayloadKind : byte
	{
		RawUnmanaged = 0,
		Json         = 1
	}

	private sealed class SnapshotResource
	{
		public string Json     { get; set; } = string.Empty;
		public string TypeName { get; set; } = string.Empty;
	}

	private sealed class WorldSnapshot
	{
		public List<SnapshotEntity>   Entities  { get; set; } = [];
		public List<SnapshotResource> Resources { get; set; } = [];
	}
}
