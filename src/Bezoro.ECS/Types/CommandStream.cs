using System.Buffers;
using Bezoro.ECS.Internal.Fixed;
using Bezoro.ECS.Services;

namespace Bezoro.ECS.Types;

/// <summary>
/// Fixed-capacity deferred command stream for structural mutations in <see cref="World" />.
/// </summary>
public sealed class CommandStream : IDisposable
{
	private readonly World                     _world;
	private readonly int                         _commandCapacity;
	private readonly int                         _componentTypeCapacity;
	private readonly int                         _payloadCapacityPerType;
	private readonly WorldOverflowPolicy       _overflowPolicy;
	private readonly RecordedCommand[]           _commands;
	private          ICommandPayloadStore?[]?    _payloadStoresByTypeId;
	private          Entity[]?                   _resolvedTemporaryEntities;
	private          int[]?                      _resolvedTemporaryGenerationByIndex;
	private          int[]?                      _payloadStoreTypeIds;
	private          int[]?                      _batchEntityIds;
	private          int[]?                      _batchPayloadIndices;
	private          uint[]?                     _batchEntityMarkerBits;
	private          int[]?                      _batchTouchedMarkerWordIndices;
	private          int                         _batchTouchedMarkerWordCount;
	private          bool                        _disposed;
	private          bool                        _isPlayingBack;
	private          int                         _commandCount;
	private          int                         _highWatermark;
	private          int                         _nextTemporaryId = -1;
	private          int                         _overflowCount;
	private          int                         _payloadStoreTypeCount;
	private          int                         _temporaryResolveGeneration = 1;

	internal CommandStream(
		World               world,
		int                   commandCapacity,
		int                   componentTypeCapacity,
		int                   payloadCapacityPerType,
		WorldOverflowPolicy overflowPolicy)
	{
		_world                   = world;
		_commandCapacity         = commandCapacity;
		_componentTypeCapacity   = componentTypeCapacity;
		_payloadCapacityPerType  = payloadCapacityPerType;
		_overflowPolicy          = overflowPolicy;
		_commands                = ArrayPool<RecordedCommand>.Shared.Rent(commandCapacity);
	}

	internal World Owner => _world;

	/// <summary>
	/// Indicates whether commands are currently recorded.
	/// </summary>
	public bool HasCommands
	{
		get
		{
			ThrowIfDisposed();
			return _commandCount > 0;
		}
	}

	/// <summary>
	/// Records a deferred entity creation command.
	/// </summary>
	/// <returns>A temporary entity handle resolved during playback.</returns>
	public Entity CreateEntity()
	{
		ThrowIfDisposed();
		if (!CanRecordCommand())
			throw new InvalidOperationException("Command stream is full and cannot allocate another temporary entity.");

		if (!TryRecord(new(RecordedCommandType.CreateEntity, new(_nextTemporaryId--, 0), -1, -1), out var command))
			throw new InvalidOperationException("Command stream is full and cannot allocate another temporary entity.");

		return command.Entity;
	}

	/// <summary>
	/// Records a deferred entity creation command with one initial component value.
	/// </summary>
	/// <typeparam name="T">Component type.</typeparam>
	/// <param name="component">Initial component value.</param>
	/// <returns>A temporary entity handle resolved during playback.</returns>
	public Entity CreateEntity<T>(in T component) where T : struct
	{
		ThrowIfDisposed();
		if (!CanRecordCommand())
			throw new InvalidOperationException("Command stream is full and cannot allocate another temporary entity.");

		int typeId = _world.GetOrCreateComponentTypeId<T>();
		var store = GetOrCreatePayloadStore<T>(typeId);
		if (!store.TryAdd(in component, out int payloadIndex))
			throw new InvalidOperationException(
				$"Payload capacity for component '{typeof(T).Name}' was exceeded."
			);

		if (!TryRecord(
				new(
					RecordedCommandType.CreateEntityWithComponent,
					new(_nextTemporaryId--, 0),
					typeId,
					payloadIndex
				),
				out var command
			))
			throw new InvalidOperationException("Command stream is full and cannot allocate another temporary entity.");

		return command.Entity;
	}

	/// <summary>
	/// Records a deferred destroy command.
	/// </summary>
	/// <param name="entity">Entity to destroy.</param>
	public void Destroy(Entity entity)
	{
		ThrowIfDisposed();
		if (!CanRecordCommand())
			return;

		TryRecord(new(RecordedCommandType.DestroyEntity, entity, -1, -1), out _);
	}

	/// <summary>
	/// Records a deferred unmanaged component set command.
	/// </summary>
	/// <typeparam name="T">Unmanaged component type.</typeparam>
	/// <param name="entity">Entity to modify.</param>
	/// <param name="component">Component value.</param>
	public void Set<T>(Entity entity, in T component) where T : unmanaged
	{
		ThrowIfDisposed();
		if (!CanRecordCommand())
			return;

		int typeId = _world.GetOrCreateComponentTypeId<T>();
		var store = GetOrCreatePayloadStore<T>(typeId);
		if (!store.TryAdd(in component, out int payloadIndex))
			return;

		TryRecord(new(RecordedCommandType.SetComponent, entity, typeId, payloadIndex), out _);
	}

	/// <summary>
	/// Records a deferred managed-lane component set command.
	/// </summary>
	/// <typeparam name="T">Managed-lane component type.</typeparam>
	/// <param name="entity">Entity to modify.</param>
	/// <param name="component">Component value.</param>
	public void SetManaged<T>(Entity entity, in T component) where T : struct
	{
		ThrowIfDisposed();
		if (!CanRecordCommand())
			return;

		int typeId = _world.GetOrCreateComponentTypeId<T>();
		var store = GetOrCreatePayloadStore<T>(typeId);
		if (!store.TryAdd(in component, out int payloadIndex))
			return;

		TryRecord(new(RecordedCommandType.SetComponent, entity, typeId, payloadIndex), out _);
	}

	/// <summary>
	/// Records a deferred component removal command.
	/// </summary>
	/// <typeparam name="T">Component type to remove.</typeparam>
	/// <param name="entity">Entity to modify.</param>
	public void Remove<T>(Entity entity) where T : struct
	{
		ThrowIfDisposed();
		if (!CanRecordCommand())
			return;

		int typeId = _world.GetOrCreateComponentTypeId<T>();
		TryRecord(new(RecordedCommandType.RemoveComponent, entity, typeId, -1), out _);
	}

	/// <summary>
	/// Returns command-buffer diagnostics for this stream.
	/// </summary>
	public CommandStreamDiagnostics GetDiagnostics()
	{
		ThrowIfDisposed();
		return new(_commandCapacity, _commandCount, _highWatermark, _overflowCount);
	}

	/// <summary>
	/// Clears recorded commands while retaining allocated buffers.
	/// </summary>
	public void Reset()
	{
		ThrowIfDisposed();
		ResetInternal();
	}

	internal void PlaybackInternal()
	{
		ThrowIfDisposed();
		if (_isPlayingBack)
			throw new InvalidOperationException("Command stream playback is already in progress.");

		if (_commandCount == 0)
			return;

		_isPlayingBack = true;
		try
		{
			var index = 0;
			while (index < _commandCount)
			{
				var command = _commands[index];
				switch (command.Type)
				{
					case RecordedCommandType.CreateEntity:
					{
						EnsureTemporaryResolutionBuffers();
						var resolvedTemporaryEntities = _resolvedTemporaryEntities!;
						var resolvedTemporaryGenerationByIndex = _resolvedTemporaryGenerationByIndex!;
						var resolved = _world.CreateEntityInternal();
						int temporaryIndex = GetTemporaryEntityIndex(command.Entity.Id);
						resolvedTemporaryEntities[temporaryIndex] = resolved;
						resolvedTemporaryGenerationByIndex[temporaryIndex] = _temporaryResolveGeneration;
						index++;
						break;
					}
					case RecordedCommandType.DestroyEntity:
						_world.DestroyEntityInternal(ResolveEntity(command.Entity));
						index++;
						break;
					case RecordedCommandType.SetComponent:
						index = PlaybackSetRun(index);
						break;
					case RecordedCommandType.RemoveComponent:
						index = PlaybackRemoveRun(index);
						break;
					case RecordedCommandType.CreateEntityWithComponent:
					{
						EnsureTemporaryResolutionBuffers();
						var resolvedTemporaryEntities = _resolvedTemporaryEntities!;
						var resolvedTemporaryGenerationByIndex = _resolvedTemporaryGenerationByIndex!;
						var created = _world.CreateEntityInternal();
						int temporaryIndex = GetTemporaryEntityIndex(command.Entity.Id);
						resolvedTemporaryEntities[temporaryIndex] = created;
						resolvedTemporaryGenerationByIndex[temporaryIndex] = _temporaryResolveGeneration;

						ValidatePayloadStoreTypeId(command.ComponentTypeId);
						var payloadStoresByTypeId = _payloadStoresByTypeId;
						var payloadStore = payloadStoresByTypeId is not null
										   ? payloadStoresByTypeId[command.ComponentTypeId]
										   : null;
						if (payloadStore is null)
							throw new InvalidOperationException(
								$"Missing payload store for component type id '{command.ComponentTypeId}'."
							);

						payloadStore.Apply(_world, created, command.PayloadIndex);
						index++;
						break;
					}
					default:
						throw new ArgumentOutOfRangeException();
				}
			}
		}
		finally
		{
			_isPlayingBack = false;
			ResetInternal();
		}
	}

	public void Dispose()
	{
		if (_disposed)
			return;

		ResetInternal();
		var payloadStoresByTypeId = _payloadStoresByTypeId;
		var payloadStoreTypeIds = _payloadStoreTypeIds;
		for (var i = 0; i < _payloadStoreTypeCount; i++)
		{
			int typeId = payloadStoreTypeIds![i];
			payloadStoresByTypeId![typeId]?.Dispose();
			payloadStoresByTypeId[typeId] = null;
			payloadStoreTypeIds[i] = 0;
		}

		_payloadStoreTypeCount = 0;
		ArrayPool<RecordedCommand>.Shared.Return(_commands, clearArray: false);
		if (_resolvedTemporaryEntities is not null)
			ArrayPool<Entity>.Shared.Return(_resolvedTemporaryEntities, clearArray: false);
		if (_resolvedTemporaryGenerationByIndex is not null)
			ArrayPool<int>.Shared.Return(_resolvedTemporaryGenerationByIndex, clearArray: false);
		if (_batchEntityIds is not null)
			ArrayPool<int>.Shared.Return(_batchEntityIds, clearArray: false);
		if (_batchPayloadIndices is not null)
			ArrayPool<int>.Shared.Return(_batchPayloadIndices, clearArray: false);
		if (_batchEntityMarkerBits is not null)
			ArrayPool<uint>.Shared.Return(_batchEntityMarkerBits, clearArray: false);
		if (_batchTouchedMarkerWordIndices is not null)
			ArrayPool<int>.Shared.Return(_batchTouchedMarkerWordIndices, clearArray: false);
		if (_payloadStoresByTypeId is not null)
			ArrayPool<ICommandPayloadStore?>.Shared.Return(_payloadStoresByTypeId, clearArray: false);
		if (_payloadStoreTypeIds is not null)
			ArrayPool<int>.Shared.Return(_payloadStoreTypeIds, clearArray: false);

		_resolvedTemporaryEntities = null;
		_resolvedTemporaryGenerationByIndex = null;
		_batchEntityIds = null;
		_batchPayloadIndices = null;
		_batchEntityMarkerBits = null;
		_batchTouchedMarkerWordIndices = null;
		_payloadStoresByTypeId = null;
		_payloadStoreTypeIds = null;
		_disposed = true;
	}

	private Entity ResolveEntity(Entity entity)
	{
		if (entity.Id >= 0)
			return entity;

		int index = GetTemporaryEntityIndex(entity.Id);
		var resolvedTemporaryGenerationByIndex = _resolvedTemporaryGenerationByIndex;
		var resolvedTemporaryEntities = _resolvedTemporaryEntities;
		if (resolvedTemporaryGenerationByIndex is null || resolvedTemporaryEntities is null)
			throw new InvalidOperationException($"Temporary entity '{entity.Id}' has not been created yet.");
		if (resolvedTemporaryGenerationByIndex[index] != _temporaryResolveGeneration)
			throw new InvalidOperationException($"Temporary entity '{entity.Id}' has not been created yet.");

		return resolvedTemporaryEntities[index];
	}

	private int PlaybackSetRun(int startIndex)
	{
		EnsureBatchBuffers(needsPayloadIndices: true);
		var batchEntityIds = _batchEntityIds!;
		var batchPayloadIndices = _batchPayloadIndices!;
		var first = _commands[startIndex];
		ValidatePayloadStoreTypeId(first.ComponentTypeId);
		var payloadStoresByTypeId = _payloadStoresByTypeId;
		var payloadStore = payloadStoresByTypeId is not null
						   ? payloadStoresByTypeId[first.ComponentTypeId]
						   : null;
		if (payloadStore is null)
			throw new InvalidOperationException(
				$"Missing payload store for component type id '{first.ComponentTypeId}'."
			);

		var firstEntity = ResolveEntity(first.Entity);
		_world.DescribeSetTransition(
			firstEntity,
			first.ComponentTypeId,
			out int sourceArchetypeId,
			out int targetArchetypeId
		);

		BeginBatchEntityMarkerRun();
		var readIndex = startIndex;
		var runCount = 0;
		while (readIndex < _commandCount)
		{
			var command = _commands[readIndex];
			if (command.Type != RecordedCommandType.SetComponent ||
				command.ComponentTypeId != first.ComponentTypeId)
				break;

			var resolved = ResolveEntity(command.Entity);
			if (!TryMarkBatchEntity(resolved.Id))
				break;

			if (!_world.MatchesSetTransitionSource(
				resolved,
				sourceArchetypeId,
				command.ComponentTypeId,
				targetArchetypeId
			))
				break;

			batchEntityIds[runCount] = resolved.Id;
			batchPayloadIndices[runCount] = command.PayloadIndex;
			runCount++;
			readIndex++;
		}

		payloadStore.ApplyBatch(
			_world,
			batchEntityIds,
			0,
			runCount,
			batchPayloadIndices,
			0,
			first.ComponentTypeId,
			sourceArchetypeId,
			targetArchetypeId
		);

		return readIndex;
	}

	private int PlaybackRemoveRun(int startIndex)
	{
		EnsureBatchBuffers(needsPayloadIndices: false);
		var batchEntityIds = _batchEntityIds!;
		var batchEntityMarkerBits = _batchEntityMarkerBits!;
		var first = _commands[startIndex];
		var firstEntity = ResolveEntity(first.Entity);
		_world.DescribeRemoveTransition(
			firstEntity,
			first.ComponentTypeId,
			out int sourceArchetypeId,
			out int targetArchetypeId
		);

		BeginBatchEntityMarkerRun();
		var readIndex = startIndex;
		var runCount = 0;
		while (readIndex < _commandCount)
		{
			var command = _commands[readIndex];
			if (command.Type != RecordedCommandType.RemoveComponent ||
				command.ComponentTypeId != first.ComponentTypeId)
				break;

			var resolved = ResolveEntity(command.Entity);
			if (!TryMarkBatchEntity(resolved.Id))
				break;

			if (!_world.MatchesRemoveTransitionSource(
				resolved,
				sourceArchetypeId,
				command.ComponentTypeId,
				targetArchetypeId
			))
				break;

			batchEntityIds[runCount++] = resolved.Id;
			readIndex++;
		}

		if (runCount > 0)
		{
			int archetypeEntityCount = _world.GetArchetypeEntityCount(sourceArchetypeId);
			if (runCount == archetypeEntityCount)
			{
				_world.RemoveAllComponentsFromCommandKnownTransitionFast(
					sourceArchetypeId,
					targetArchetypeId
				);
			}
			else if ((runCount << 2) >= archetypeEntityCount)
			{
				_world.RemoveMarkedComponentsFromCommandKnownTransitionFast(
					batchEntityMarkerBits,
					sourceArchetypeId,
					targetArchetypeId
				);
			}
			else
			{
				_world.RemoveComponentBatchFromCommandKnownTransitionFast(
					batchEntityIds,
					0,
					runCount,
					sourceArchetypeId,
					targetArchetypeId
				);
			}
		}

		return readIndex;
	}

	private int GetTemporaryEntityIndex(int temporaryEntityId)
	{
		int index = -temporaryEntityId - 1;
		if (index < 0 || index >= _commandCapacity)
			throw new InvalidOperationException($"Invalid temporary entity id '{temporaryEntityId}'.");

		return index;
	}

	private CommandPayloadStore<T> GetOrCreatePayloadStore<T>(int typeId) where T : struct
	{
		ValidatePayloadStoreTypeId(typeId);
		EnsurePayloadStoreTables();
		var payloadStoresByTypeId = _payloadStoresByTypeId!;
		var payloadStoreTypeIds = _payloadStoreTypeIds!;

		var existing = payloadStoresByTypeId[typeId];
		if (existing is not null)
		{
			if (existing is not CommandPayloadStore<T> typedStore)
				throw new InvalidOperationException(
					$"Payload store type mismatch for component type id '{typeId}'."
				);

			return typedStore;
		}

		var store = new CommandPayloadStore<T>(_payloadCapacityPerType, _overflowPolicy);
		payloadStoresByTypeId[typeId] = store;
		payloadStoreTypeIds[_payloadStoreTypeCount++] = typeId;
		return store;
	}

	private bool TryRecord(RecordedCommand command, out RecordedCommand recordedCommand)
	{
		if (_commandCount >= _commandCapacity)
		{
			_overflowCount++;
			if (_overflowPolicy == WorldOverflowPolicy.FailFast)
				throw new InvalidOperationException(
					$"Command capacity '{_commandCapacity}' exceeded for this command stream."
				);

			recordedCommand = default;
			return false;
		}

		recordedCommand              = command;
		_commands[_commandCount++] = command;
		if (_commandCount > _highWatermark)
			_highWatermark = _commandCount;
		return true;
	}

	private bool CanRecordCommand()
	{
		if (_commandCount < _commandCapacity)
			return true;

		_overflowCount++;
		if (_overflowPolicy == WorldOverflowPolicy.FailFast)
			throw new InvalidOperationException(
				$"Command capacity '{_commandCapacity}' exceeded for this command stream."
			);

		return false;
	}

	private void ResetInternal()
	{
		_commandCount          = 0;
		_nextTemporaryId       = -1;
		BeginTemporaryResolveGeneration();
		var payloadStoresByTypeId = _payloadStoresByTypeId;
		var payloadStoreTypeIds = _payloadStoreTypeIds;
		if (payloadStoresByTypeId is null || payloadStoreTypeIds is null)
			return;

		for (var i = 0; i < _payloadStoreTypeCount; i++)
		{
			int typeId = payloadStoreTypeIds[i];
			payloadStoresByTypeId[typeId]?.Clear();
		}
	}

	private void BeginTemporaryResolveGeneration()
	{
		if (_temporaryResolveGeneration == int.MaxValue)
		{
			if (_resolvedTemporaryGenerationByIndex is not null)
			{
				Array.Clear(
					_resolvedTemporaryGenerationByIndex,
					0,
					_resolvedTemporaryGenerationByIndex.Length
				);
			}

			_temporaryResolveGeneration = 1;
			return;
		}

		_temporaryResolveGeneration++;
	}

	private void BeginBatchEntityMarkerRun()
	{
		var markerBits = _batchEntityMarkerBits;
		var touchedMarkerWordIndices = _batchTouchedMarkerWordIndices;
		if (markerBits is null || touchedMarkerWordIndices is null)
			throw new InvalidOperationException("Batch marker buffers are not initialized.");

		for (var i = 0; i < _batchTouchedMarkerWordCount; i++)
		{
			int wordIndex = touchedMarkerWordIndices[i];
			markerBits[wordIndex] = 0u;
		}

		_batchTouchedMarkerWordCount = 0;
	}

	private bool TryMarkBatchEntity(int entityId)
	{
		if (entityId < 0)
			return false;

		var markerBits = _batchEntityMarkerBits;
		var touchedMarkerWordIndices = _batchTouchedMarkerWordIndices;
		if (markerBits is null || touchedMarkerWordIndices is null)
			throw new InvalidOperationException("Batch marker buffers are not initialized.");
		if ((uint)entityId >= (uint)_world.EntityCapacity)
			return false;

		int wordIndex = entityId >> 5;
		uint bitMask = 1u << (entityId & 31);
		uint markerWord = markerBits[wordIndex];
		if ((markerWord & bitMask) != 0u)
			return false;

		if (markerWord == 0u)
			touchedMarkerWordIndices[_batchTouchedMarkerWordCount++] = wordIndex;

		markerBits[wordIndex] = markerWord | bitMask;
		return true;
	}

	private void EnsureBatchBuffers(bool needsPayloadIndices)
	{
		if (_batchEntityIds is null)
			_batchEntityIds = ArrayPool<int>.Shared.Rent(_commandCapacity);
		if (needsPayloadIndices && _batchPayloadIndices is null)
			_batchPayloadIndices = ArrayPool<int>.Shared.Rent(_commandCapacity);
		if (_batchEntityMarkerBits is null || _batchTouchedMarkerWordIndices is null)
		{
			int markerWordCount = (_world.EntityCapacity + 31) >> 5;
			_batchEntityMarkerBits = ArrayPool<uint>.Shared.Rent(markerWordCount);
			Array.Clear(_batchEntityMarkerBits, 0, markerWordCount);
			_batchTouchedMarkerWordIndices = ArrayPool<int>.Shared.Rent(markerWordCount);
			_batchTouchedMarkerWordCount = 0;
		}
	}

	private void EnsurePayloadStoreTables()
	{
		if (_payloadStoresByTypeId is not null)
			return;

		_payloadStoresByTypeId = ArrayPool<ICommandPayloadStore?>.Shared.Rent(_componentTypeCapacity);
		Array.Clear(_payloadStoresByTypeId, 0, _payloadStoresByTypeId.Length);
		_payloadStoreTypeIds = ArrayPool<int>.Shared.Rent(_componentTypeCapacity);
	}

	private void EnsureTemporaryResolutionBuffers()
	{
		if (_resolvedTemporaryEntities is not null)
			return;

		_resolvedTemporaryEntities = ArrayPool<Entity>.Shared.Rent(_commandCapacity);
		_resolvedTemporaryGenerationByIndex = ArrayPool<int>.Shared.Rent(_commandCapacity);
		Array.Clear(_resolvedTemporaryGenerationByIndex, 0, _resolvedTemporaryGenerationByIndex.Length);
		_temporaryResolveGeneration = 1;
	}

	private void ValidatePayloadStoreTypeId(int typeId)
	{
		if ((uint)typeId >= (uint)_componentTypeCapacity)
			throw new InvalidOperationException($"Invalid component type id '{typeId}' for this command stream.");
	}

	private void ThrowIfDisposed()
	{
		if (_disposed)
			throw new ObjectDisposedException(nameof(CommandStream));
	}
}

