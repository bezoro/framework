using System.Buffers;
using Bezoro.ECS.Internal;
using Bezoro.ECS.Services;

namespace Bezoro.ECS.Types;

/// <summary>
///     Records structural changes to be applied after system execution.
/// </summary>
public sealed class CommandBuffer : IDisposable
{
	private readonly object _sync = new();
	private readonly World  _world;
	private          Dictionary<int, IComponentPayloadStore>? _componentPayloadStores;
	private          HashSet<int>? _referencedTemporaryEntities;
	private          Dictionary<int, Entity>? _resolvedTemporaryEntities;
	private          List<Command>?           _commands;
	private          bool                     _disposed;
	private          bool                     _isPlayingBack;
	private          int                      _nextCommandIndex;
	private          int                      _nextTemporaryId = -1;

	internal CommandBuffer(World world)
	{
		_world = world;
	}

	internal bool HasCommands
	{
		get
		{
			ThrowIfDisposed();
			lock (_sync)
			{
				return _commands is { } commands && commands.Count > _nextCommandIndex;
			}
		}
	}

	internal bool HasAllocatedStorage
	{
		get
		{
			ThrowIfDisposed();
			lock (_sync)
			{
				return _commands is not null ||
					   _resolvedTemporaryEntities is not null ||
					   _componentPayloadStores is not null ||
					   _referencedTemporaryEntities is not null;
			}
		}
	}

	/// <summary>
	///     Creates a new entity that will be added to the empty archetype on playback.
	/// </summary>
	/// <returns>The reserved entity handle.</returns>
	public Entity CreateEntity() =>
		CreateEntity(_world.EmptyArchetype);

	/// <summary>
	///     Creates a new entity with one initial component value.
	/// </summary>
	public Entity CreateEntity<T1>(in T1 component1)
		where T1 : struct
	{
		ThrowIfDisposed();
		_world.ThrowIfDisposed();
		int typeId = _world.GetOrCreateComponentTypeId<T1>();

		lock (_sync)
		{
			var entity = new Entity(_nextTemporaryId--, 0);
			int payloadIndex = StoreComponentPayloadUnsafe(typeId, in component1);
			(_commands ??= []).Add(
				new(CommandType.CreateEntityWithComponent, entity, null, typeId, payloadIndex, false)
			);
			return entity;
		}
	}

	/// <summary>
	///     Creates a new entity with two initial component values.
	/// </summary>
	public Entity CreateEntity<T1, T2>(in T1 component1, in T2 component2)
		where T1 : struct
		where T2 : struct
	{
		_world.ThrowIfDisposed();
		var entity = CreateEntity();
		AddComponent(entity, in component1);
		AddComponent(entity, in component2);
		return entity;
	}

	/// <summary>
	///     Creates a new entity with three initial component values.
	/// </summary>
	public Entity CreateEntity<T1, T2, T3>(in T1 component1, in T2 component2, in T3 component3)
		where T1 : struct
		where T2 : struct
		where T3 : struct
	{
		_world.ThrowIfDisposed();
		var entity = CreateEntity();
		AddComponent(entity, in component1);
		AddComponent(entity, in component2);
		AddComponent(entity, in component3);
		return entity;
	}

	/// <summary>
	///     Creates a new entity with four initial component values.
	/// </summary>
	public Entity CreateEntity<T1, T2, T3, T4>(in T1 component1, in T2 component2, in T3 component3, in T4 component4)
		where T1 : struct
		where T2 : struct
		where T3 : struct
		where T4 : struct
	{
		_world.ThrowIfDisposed();
		var entity = CreateEntity();
		AddComponent(entity, in component1);
		AddComponent(entity, in component2);
		AddComponent(entity, in component3);
		AddComponent(entity, in component4);
		return entity;
	}

	/// <summary>
	///     Creates a new entity that will be added to the specified archetype on playback.
	/// </summary>
	/// <param name="archetype">The archetype the entity should belong to.</param>
	/// <returns>The reserved entity handle.</returns>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="archetype" /> is null.</exception>
	/// <exception cref="InvalidOperationException">Thrown when the archetype belongs to a different world.</exception>
	public Entity CreateEntity(Archetype archetype)
	{
		ThrowIfDisposed();
		_world.ThrowIfDisposed();
		if (archetype is null) throw new ArgumentNullException(nameof(archetype));

		_world.EnsureOwnedArchetype(archetype);

		lock (_sync)
		{
			var entity = new Entity(_nextTemporaryId--, 0);
			(_commands ??= []).Add(new(CommandType.CreateEntity, entity, archetype, -1, -1, false));
			return entity;
		}
	}

	/// <summary>
	///     Adds a component to an entity when the command buffer is played back.
	/// </summary>
	/// <typeparam name="T">The component type.</typeparam>
	/// <param name="entity">The entity to modify.</param>
	/// <param name="component">The component value to add.</param>
	public void AddComponent<T>(Entity entity, in T component) where T : struct
	{
		ThrowIfDisposed();
		_world.ThrowIfDisposed();
		int typeId = _world.GetOrCreateComponentTypeId<T>();
		EnqueueComponentCommand(entity, typeId, in component, addOnly: true);
	}

	/// <summary>
	///     Destroys an entity when the command buffer is played back.
	/// </summary>
	/// <param name="entity">The entity to destroy.</param>
	public void DestroyEntity(Entity entity) =>
		Enqueue(new(CommandType.DestroyEntity, entity, null, -1, -1, false));

	public void Dispose()
	{
		if (_disposed) return;

		lock (_sync)
		{
			ResetStateUnsafe();
			_disposed = true;
		}
	}

	/// <summary>
	///     Applies all recorded commands to the world.
	/// </summary>
	/// <remarks>
	///     Playback is not allowed while systems are executing inside <see cref="World.Tick" />.
	///     If playback fails, successfully processed commands are removed and unprocessed commands stay queued for retry.
	/// </remarks>
	/// <exception cref="InvalidOperationException">Thrown when playback is invoked during world update.</exception>
	/// <exception cref="InvalidOperationException">Thrown when a temporary entity has not been created in this buffer.</exception>
	public void Playback() =>
		PlaybackInternal(false);

	/// <summary>
	///     Removes a component from an entity when the command buffer is played back.
	/// </summary>
	/// <typeparam name="T">The component type.</typeparam>
	/// <param name="entity">The entity to modify.</param>
	public void RemoveComponent<T>(Entity entity) where T : struct
	{
		ThrowIfDisposed();
		_world.ThrowIfDisposed();
		int typeId = _world.GetOrCreateComponentTypeId<T>();
		Enqueue(new(CommandType.RemoveComponent, entity, null, typeId, -1, false));
	}

	/// <summary>
	///     Sets a component value when the command buffer is played back.
	/// </summary>
	/// <typeparam name="T">The component type.</typeparam>
	/// <param name="entity">The entity to modify.</param>
	/// <param name="component">The component value to set.</param>
	public void SetComponent<T>(Entity entity, in T component) where T : struct
	{
		ThrowIfDisposed();
		_world.ThrowIfDisposed();
		int typeId = _world.GetOrCreateComponentTypeId<T>();
		EnqueueComponentCommand(entity, typeId, in component, addOnly: false);
	}

	internal void PlaybackInternal(bool allowDuringUpdate)
	{
		ThrowIfDisposed();
		_world.ThrowIfDisposed();
		if (_world.HasActiveQueryIteration)
			throw new InvalidOperationException("Playback cannot run during query iteration.");

		if (!allowDuringUpdate && _world.IsUpdating)
			throw new InvalidOperationException("Playback cannot run during world update.");

		Command[]?              commands   = null;
		int                     commandCount = 0;
		Dictionary<int, Entity>? tempEntities = null;
		lock (_sync)
		{
			if (_isPlayingBack)
				throw new InvalidOperationException("CommandBuffer playback is already in progress.");

			int pendingCount = (_commands?.Count ?? 0) - _nextCommandIndex;
			if (pendingCount <= 0) return;

			commands = ArrayPool<Command>.Shared.Rent(pendingCount);
			_commands!.CopyTo(_nextCommandIndex, commands, 0, pendingCount);
			commandCount = pendingCount;
			tempEntities = _resolvedTemporaryEntities ??= [];
			_isPlayingBack = true;
		}
		if (tempEntities is null)
			throw new InvalidOperationException("CommandBuffer temporary entity map was not initialized.");

		var processedCount  = 0;
		var enteredPlayback = false;
		try
		{
			_world.EnterCommandPlayback();
			enteredPlayback = true;

			for (; processedCount < commandCount; processedCount++)
			{
				var command = commands[processedCount];
				switch (command.Type)
				{
					case CommandType.CreateEntity:
					{
						var archetype = command.Archetype ?? _world.EmptyArchetype;
						_world.EnsureOwnedArchetype(archetype);
						var entity = _world.CreateEntityInternal(archetype);
						if (IsTemporaryEntityReferenced(command.Entity.Id))
							tempEntities[command.Entity.Id] = entity;
						break;
					}
					case CommandType.CreateEntityWithComponent:
					{
						var entity = _world.CreateEntityInternal(_world.EmptyArchetype);
						if (IsTemporaryEntityReferenced(command.Entity.Id))
							tempEntities[command.Entity.Id] = entity;

						GetPayloadStore(command.ComponentTypeId).Apply(
							_world,
							entity,
							command.PayloadIndex,
							addOnly: false
						);
						break;
					}
					case CommandType.DestroyEntity:
						_world.DestroyEntityInternal(ResolveEntity(command.Entity, tempEntities));
						break;
					case CommandType.AddComponent:
					case CommandType.SetComponent:
					{
						GetPayloadStore(command.ComponentTypeId).Apply(
							_world,
							ResolveEntity(command.Entity, tempEntities),
							command.PayloadIndex,
							command.AddOnly
						);
						break;
					}
					case CommandType.RemoveComponent:
					{
						var entity = ResolveEntity(command.Entity, tempEntities);
						_world.EnsureEntityAlive(entity);
						_world.RemoveComponentById(entity, command.ComponentTypeId);
						break;
					}
					default:
						throw new ArgumentOutOfRangeException();
				}
			}
		}
		finally
		{
			if (enteredPlayback)
				_world.ExitCommandPlayback();

			lock (_sync)
			{
				RemoveProcessedCommandsUnsafe(processedCount);
				UpdateResolvedTemporaryEntitiesUnsafe();
				_isPlayingBack = false;
			}

			if (commands is not null)
			{
				Array.Clear(commands, 0, commandCount);
				ArrayPool<Command>.Shared.Return(commands);
			}
		}
	}

	private static Entity ResolveEntity(Entity entity, Dictionary<int, Entity> tempEntities)
	{
		if (entity.Id >= 0) return entity;

		if (tempEntities.TryGetValue(entity.Id, out var resolved))
			return resolved;

		throw new InvalidOperationException($"Temporary entity {entity.Id} was not created in this command buffer.");
	}

	private void EnqueueComponentCommand<T>(Entity entity, int typeId, in T component, bool addOnly) where T : struct
	{
		lock (_sync)
		{
			MarkTemporaryEntityReferenceUnsafe(entity);
			int payloadIndex = StoreComponentPayloadUnsafe(typeId, in component);
			(_commands ??= []).Add(
				new(addOnly ? CommandType.AddComponent : CommandType.SetComponent, entity, null, typeId, payloadIndex, addOnly)
			);
		}
	}

	private void Enqueue(Command command)
	{
		ThrowIfDisposed();
		_world.ThrowIfDisposed();
		lock (_sync)
		{
			MarkTemporaryEntityReferenceUnsafe(command.Entity);
			(_commands ??= []).Add(command);
		}
	}

	private void RemoveProcessedCommandsUnsafe(int processedCount)
	{
		if (processedCount <= 0 || _commands is null || _commands.Count == 0) return;

		_nextCommandIndex += processedCount;
		if (_nextCommandIndex > _commands.Count)
			_nextCommandIndex = _commands.Count;

		if (_nextCommandIndex == _commands.Count)
		{
			_commands.Clear();
			ClearPayloadStoresUnsafe();
			ClearTemporaryEntityReferencesUnsafe();
			_nextCommandIndex = 0;
			return;
		}

		if (_nextCommandIndex >= 256 && _nextCommandIndex * 2 >= _commands.Count)
		{
			_commands.RemoveRange(0, _nextCommandIndex);
			_nextCommandIndex = 0;
		}
	}

	private int StoreComponentPayloadUnsafe<T>(int typeId, in T component) where T : struct
	{
		_componentPayloadStores ??= [];
		if (_componentPayloadStores.TryGetValue(typeId, out var existing))
		{
			if (existing is not ComponentPayloadStore<T> typedStore)
				throw new InvalidOperationException(
					$"Component payload store type mismatch for component type id {typeId}."
				);

			return typedStore.Add(in component);
		}

		var store = new ComponentPayloadStore<T>(typeId);
		_componentPayloadStores[typeId] = store;
		return store.Add(in component);
	}

	private IComponentPayloadStore GetPayloadStore(int typeId)
	{
		if (_componentPayloadStores is not { } stores || !stores.TryGetValue(typeId, out var payloadStore))
			throw new InvalidOperationException($"Payload store for component type id {typeId} was not found.");

		return payloadStore;
	}

	private void ClearPayloadStoresUnsafe()
	{
		if (_componentPayloadStores is null) return;

		foreach (var store in _componentPayloadStores.Values)
			store.Clear();
	}

	private bool IsTemporaryEntityReferenced(int temporaryEntityId) =>
		_referencedTemporaryEntities is { } referenced && referenced.Contains(temporaryEntityId);

	private void MarkTemporaryEntityReferenceUnsafe(Entity entity)
	{
		if (entity.Id >= 0) return;

		(_referencedTemporaryEntities ??= []).Add(entity.Id);
	}

	private void ClearTemporaryEntityReferencesUnsafe() =>
		_referencedTemporaryEntities?.Clear();

	private void UpdateResolvedTemporaryEntitiesUnsafe()
	{
		if (_resolvedTemporaryEntities is null) return;
		if ((_commands?.Count ?? 0) != _nextCommandIndex) return;

		_resolvedTemporaryEntities.Clear();
	}

	private void ResetStateUnsafe()
	{
		_isPlayingBack = false;
		_commands = null;
		_resolvedTemporaryEntities = null;
		_componentPayloadStores = null;
		_referencedTemporaryEntities = null;
		_nextCommandIndex = 0;
		_nextTemporaryId  = -1;
	}

	private void ThrowIfDisposed()
	{
		if (_disposed)
			throw new ObjectDisposedException(nameof(CommandBuffer));
	}

	private interface IComponentPayloadStore
	{
		void Apply(World world, Entity entity, int payloadIndex, bool addOnly);

		void Clear();
	}

	private sealed class ComponentPayloadStore<T>(int typeId) : IComponentPayloadStore where T : struct
	{
		private readonly List<T> _payloads = [];

		public int Add(in T payload)
		{
			_payloads.Add(payload);
			return _payloads.Count - 1;
		}

		public void Apply(World world, Entity entity, int payloadIndex, bool addOnly)
		{
			if ((uint)payloadIndex >= (uint)_payloads.Count)
				throw new InvalidOperationException($"Command payload index {payloadIndex} was out of range.");

			var payload = _payloads[payloadIndex];
			if (addOnly)
				world.ApplyAddComponentTyped(entity, typeId, in payload);
			else
				world.ApplySetComponentTyped(entity, typeId, in payload);
		}

		public void Clear() => _payloads.Clear();
	}
}
