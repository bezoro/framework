using System.Runtime.CompilerServices;
using Bezoro.ECS.Internal.Fixed;
using Bezoro.ECS.Services;
using Bezoro.ECS.Types;
using RelationMarker = Bezoro.ECS.Internal.RelationMarker;

namespace Bezoro.ECS.Internal;

internal sealed class WorldEntityStore
{
	private readonly Dictionary<ArchetypeTypeSetKey, int> _archetypeByTypeSet =
		new(ArchetypeTypeSetKeyComparer.Instance);
	private readonly List<ArchetypeStorage> _archetypes = [];
	private readonly bool[] _aliveByEntityId;
	private readonly Dictionary<long, int[]> _transitionCopyMapByPair = [];
	private readonly WorldConfig _config;
	private readonly int _emptyArchetypeId;
	private readonly int[] _freeEntityIds;
	private readonly Bezoro.ECS.Internal.Fixed.EntityLocation[] _locationByEntityId;
	private readonly int _maskWordCount;
	private readonly WorldRelationIndex _relationIndex;
	private readonly Dictionary<Type, int> _typeToId = [];
	private readonly Type?[] _typeById;
	private readonly bool[] _typeIsManagedLane;
	private readonly int[] _versionByEntityId;
	private readonly World _world;

	private int _aliveCount;
	private int _archetypeVersion;
	private int _componentTypeOverflowCount;
	private int _entityHighWatermark;
	private int _entityOverflowCount;
	private int _freeCount;
	private int _nextEntityId;
	private int _registeredTypeHighWatermark;
	private int _typeCount;

	public WorldEntityStore(World world, WorldConfig config, WorldRelationIndex relationIndex)
	{
		_world = world ?? throw new ArgumentNullException(nameof(world));
		_config = config ?? throw new ArgumentNullException(nameof(config));
		_relationIndex = relationIndex ?? throw new ArgumentNullException(nameof(relationIndex));

		_aliveByEntityId = new bool[_config.EntityCapacity];
		_versionByEntityId = new int[_config.EntityCapacity];
		_freeEntityIds = new int[_config.EntityCapacity];
		_locationByEntityId = new Bezoro.ECS.Internal.Fixed.EntityLocation[_config.EntityCapacity];
		for (var i = 0; i < _locationByEntityId.Length; i++)
			_locationByEntityId[i] = Bezoro.ECS.Internal.Fixed.EntityLocation.Invalid;

		_typeById = new Type[_config.ComponentTypeCapacity];
		_typeIsManagedLane = new bool[_config.ComponentTypeCapacity];
		_maskWordCount = GetMaskWordCount(_config.ComponentTypeCapacity);
		_emptyArchetypeId = GetOrCreateArchetype([]);
	}

	public bool[] AliveByEntityId => _aliveByEntityId;

	public int AliveCount
	{
		get => _aliveCount;
		set => _aliveCount = value;
	}

	public int ArchetypeVersion
	{
		get => _archetypeVersion;
		set => _archetypeVersion = value;
	}

	public List<ArchetypeStorage> Archetypes => _archetypes;

	public int ComponentTypeOverflowCount
	{
		get => _componentTypeOverflowCount;
		set => _componentTypeOverflowCount = value;
	}

	public Dictionary<ArchetypeTypeSetKey, int> ArchetypeByTypeSet => _archetypeByTypeSet;

	public int EmptyArchetypeId => _emptyArchetypeId;

	public int EntityHighWatermark
	{
		get => _entityHighWatermark;
		set => _entityHighWatermark = value;
	}

	public int EntityOverflowCount
	{
		get => _entityOverflowCount;
		set => _entityOverflowCount = value;
	}

	public int[] FreeEntityIds => _freeEntityIds;

	public int FreeCount
	{
		get => _freeCount;
		set => _freeCount = value;
	}

	public Bezoro.ECS.Internal.Fixed.EntityLocation[] LocationByEntityId => _locationByEntityId;

	public int MaskWordCount => _maskWordCount;

	public int NextEntityId
	{
		get => _nextEntityId;
		set => _nextEntityId = value;
	}

	public int RegisteredTypeHighWatermark
	{
		get => _registeredTypeHighWatermark;
		set => _registeredTypeHighWatermark = value;
	}

	public Dictionary<long, int[]> TransitionCopyMapByPair => _transitionCopyMapByPair;

	public Dictionary<Type, int> TypeToId => _typeToId;

	public int TypeCount
	{
		get => _typeCount;
		set => _typeCount = value;
	}

	public Type?[] TypeById => _typeById;

	public bool[] TypeIsManagedLane => _typeIsManagedLane;

	public int[] VersionByEntityId => _versionByEntityId;

	public Entity CreateEntity()
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

	public void Reset()
	{
		for (var entityId = 0; entityId < _nextEntityId; entityId++)
		{
			_aliveByEntityId[entityId] = false;
			_versionByEntityId[entityId]++;
			_locationByEntityId[entityId] = Bezoro.ECS.Internal.Fixed.EntityLocation.Invalid;
		}

		for (var i = 0; i < _archetypes.Count; i++)
			_archetypes[i].Clear();

		_aliveCount = 0;
		_freeCount = 0;
		_nextEntityId = 0;
		_relationIndex.Clear();
	}

	public void Dispose()
	{
		for (var i = 0; i < _archetypes.Count; i++)
			_archetypes[i].Dispose();

		_typeToId.Clear();
		_relationIndex.Clear();
		_archetypeByTypeSet.Clear();
		_archetypes.Clear();
		_transitionCopyMapByPair.Clear();
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public bool IsAlive(Entity entity) =>
		entity.Id >= 0 &&
		entity.Id < _aliveByEntityId.Length &&
		_aliveByEntityId[entity.Id] &&
		_versionByEntityId[entity.Id] == entity.Version;

	public void EnsureAlive(Entity entity)
	{
		if (!IsAlive(entity))
			throw new InvalidOperationException($"Entity '{entity.Id}:{entity.Version}' is not alive.");
	}

	public bool TryGetComponentUnchecked<T>(int entityId, int typeId, out T component) where T : struct
	{
		var location = _locationByEntityId[entityId];
		if (!location.IsValid)
		{
			component = default;
			return false;
		}

		var archetype = _archetypes[location.ArchetypeId];
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

	public ref T GetComponentRefUnchecked<T>(int entityId, int typeId) where T : struct
	{
		var location = _locationByEntityId[entityId];
		if (!location.IsValid)
			throw new InvalidOperationException($"Entity '{entityId}' is not in a valid archetype.");

		var archetype = _archetypes[location.ArchetypeId];
		int columnIndex = archetype.GetColumnIndexOrNegative(typeId);
		if (columnIndex < 0)
			throw new KeyNotFoundException(
				$"Component '{typeof(T).Name}' was not found for entity '{entityId}:{_versionByEntityId[entityId]}'."
			);

		var chunk = archetype.GetChunkUnchecked(location.ChunkIndex);
		return ref archetype.GetRefByIndex<T>(chunk, columnIndex, location.RowIndex);
	}

	public ref T GetComponentRefForCursorUnchecked<T>(int entityId, int typeId) where T : struct
	{
		var location = _locationByEntityId[entityId];
		if (!location.IsValid)
			throw new InvalidOperationException($"Entity '{entityId}' is not in a valid archetype.");

		return ref _archetypes[location.ArchetypeId].GetRef<T>(location.ChunkIndex, location.RowIndex, typeId);
	}

	public ArchetypeStorage GetArchetypeForCursor(int archetypeId)
	{
		if ((uint)archetypeId >= (uint)_archetypes.Count)
			throw new ArgumentOutOfRangeException(nameof(archetypeId));

		return _archetypes[archetypeId];
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public Entity GetEntityForCursor(int entityId) =>
		new(entityId, _versionByEntityId[entityId]);

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public int[] GetEntityVersionsForCursor() => _versionByEntityId;

	public int GetArchetypeEntityCount(int archetypeId)
	{
		if ((uint)archetypeId >= (uint)_archetypes.Count)
			throw new ArgumentOutOfRangeException(nameof(archetypeId));

		return _archetypes[archetypeId].EntityCount;
	}

	public int GetOrCreateComponentTypeIdGeneric<T>() where T : struct
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
		_typeToId[componentType] = typeId;
		_typeById[typeId] = componentType;
		_typeIsManagedLane[typeId] = RuntimeHelpers.IsReferenceOrContainsReferences<T>();
		if (_typeCount > _registeredTypeHighWatermark)
			_registeredTypeHighWatermark = _typeCount;

		return typeId;
	}

	public int GetOrCreateComponentTypeId(Type componentType, bool containsReferences)
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
		_typeToId[componentType] = typeId;
		_typeById[typeId] = componentType;
		_typeIsManagedLane[typeId] = containsReferences;
		if (_typeCount > _registeredTypeHighWatermark)
			_registeredTypeHighWatermark = _typeCount;

		return typeId;
	}

	public int CreateRelationTypeId()
	{
		if (_typeCount >= _typeById.Length)
		{
			_componentTypeOverflowCount++;
			throw new InvalidOperationException(
				$"Component type capacity '{_config.ComponentTypeCapacity}' was exceeded."
			);
		}

		int typeId = _typeCount++;
		_typeById[typeId] = typeof(RelationMarker);
		_typeIsManagedLane[typeId] = false;
		if (_typeCount > _registeredTypeHighWatermark)
			_registeredTypeHighWatermark = _typeCount;

		return typeId;
	}

	public void ApplySetBatchFromCommandKnownTransition<T>(
		int[] entityIds,
		int entityOffset,
		int count,
		T[] payloads,
		int payloadCount,
		int[] payloadIndices,
		int payloadOffset,
		int typeId,
		int sourceArchetypeId,
		int targetArchetypeId)
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

			int entityId = entityIds[entityOffset + i];
			ref readonly var component = ref payloads[payloadIndex];
			var location = _locationByEntityId[entityId];
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

	public void ApplySetBatchFromCommandKnownTransitionFast<T>(
		int[] entityIds,
		int entityOffset,
		int count,
		T[] payloads,
		int[] payloadIndices,
		int payloadOffset,
		int typeId,
		int sourceArchetypeId,
		int targetArchetypeId)
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

			int cachedChunkIndex = -1;
			Span<T> cachedColumn = default;
			for (var i = 0; i < count; i++)
			{
				int payloadIndex = payloadIndices[payloadOffset + i];
				int entityId = entityIds[entityOffset + i];
				var location = _locationByEntityId[entityId];
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

		var targetArchetype = _archetypes[targetArchetypeId];
		int[] sourceTargetColumnPairs = GetOrCreateTransitionCopyMap(sourceArchetype, targetArchetype);
		for (var i = 0; i < count; i++)
		{
			int payloadIndex = payloadIndices[payloadOffset + i];
			int entityId = entityIds[entityOffset + i];
			var location = _locationByEntityId[entityId];
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

	public void ApplySetFromCommand<T>(Entity entity, in T component) where T : struct
	{
		EnsureAlive(entity);
		int typeId = GetOrCreateComponentTypeIdGeneric<T>();
		SetComponentInternal(entity.Id, typeId, in component);
	}

	public void ApplySetFromCommandKnownTransition<T>(
		Entity entity,
		in T component,
		int typeId,
		int sourceArchetypeId,
		int targetArchetypeId)
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

	public void DescribeRemoveTransition(
		Entity entity,
		int typeId,
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

	public void DescribeSetTransition(Entity entity, int typeId, out int sourceArchetypeId, out int targetArchetypeId)
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

	public void DestroyEntity(Entity entity)
	{
		EnsureAlive(entity);
		_relationIndex.ReleaseRelationsForTarget(entity, RemoveRelationTypeFromAllSources);
		int entityId = entity.Id;
		var location = _locationByEntityId[entityId];
		var archetype = _archetypes[location.ArchetypeId];
		if (archetype.RemoveAt(location.ChunkIndex, location.RowIndex, out int movedEntityId))
			UpdateMovedEntityLocation(location.ArchetypeId, location.ChunkIndex, location.RowIndex, movedEntityId);

		_aliveByEntityId[entityId] = false;
		_locationByEntityId[entityId] = Bezoro.ECS.Internal.Fixed.EntityLocation.Invalid;
		_versionByEntityId[entityId]++;
		_freeEntityIds[_freeCount++] = entityId;
		_aliveCount--;
		_world.ClearEntityComponentVersionsForEntityStore(entityId, _typeCount);
	}

	public void RemoveAllComponentsFromCommandKnownTransitionFast(int sourceArchetypeId, int targetArchetypeId)
	{
		if (targetArchetypeId == sourceArchetypeId)
			return;

		var sourceArchetype = _archetypes[sourceArchetypeId];
		var targetArchetype = _archetypes[targetArchetypeId];
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

	public void RemoveComponentBatchFromCommandKnownTransition(
		int[] entityIds,
		int entityOffset,
		int count,
		int typeId,
		int sourceArchetypeId,
		int targetArchetypeId)
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

	public void RemoveComponentBatchFromCommandKnownTransitionFast(
		int[] entityIds,
		int entityOffset,
		int count,
		int sourceArchetypeId,
		int targetArchetypeId)
	{
		if (count == 0 || targetArchetypeId == sourceArchetypeId)
			return;

		var sourceArchetype = _archetypes[sourceArchetypeId];
		var targetArchetype = _archetypes[targetArchetypeId];
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

	public void RemoveComponentFromCommand(Entity entity, int typeId)
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

	public void RemoveComponentFromCommandKnownTransition(
		Entity entity,
		int typeId,
		int sourceArchetypeId,
		int targetArchetypeId)
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

	public void RemoveMarkedComponentsFromCommandKnownTransitionFast(
		uint[] batchEntityMarkerBits,
		int sourceArchetypeId,
		int targetArchetypeId)
	{
		if (targetArchetypeId == sourceArchetypeId)
			return;

		var sourceArchetype = _archetypes[sourceArchetypeId];
		var targetArchetype = _archetypes[targetArchetypeId];
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
			var readRow = 0;
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
					sourceArchetype.MoveRowRangeWithinChunk(chunk, runStart, writeRow, runLength);

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
	public bool HasComponentForAccessor(
		Entity entity,
		int typeId,
		ref int cachedArchetypeId,
		ref int cachedColumnIndex)
	{
		if (!IsAlive(entity))
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

	public bool MatchesRemoveTransitionSource(
		Entity entity,
		int sourceArchetypeId,
		int typeId,
		int targetArchetypeId)
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

	public bool MatchesSetTransitionSource(
		Entity entity,
		int sourceArchetypeId,
		int typeId,
		int targetArchetypeId)
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
	public bool TryGetComponentForAccessor<T>(
		Entity entity,
		int typeId,
		ref int cachedArchetypeId,
		ref int cachedColumnIndex,
		out T component)
		where T : unmanaged
	{
		if (!IsAlive(entity))
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

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public void ResolveAccessorLocation(
		Entity entity,
		int typeId,
		Type componentType,
		ref int cachedArchetypeId,
		ref int cachedColumnIndex,
		out ArchetypeStorage archetype,
		out int chunkIndex,
		out int rowIndex)
	{
		if (componentType is null) throw new ArgumentNullException(nameof(componentType));
		if (!IsAlive(entity))
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
		rowIndex = location.RowIndex;
	}

	public Type[] ResolveTypes(int[] typeIds)
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

	public int[] BuildMaskWordIndices(ulong[] maskWords)
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
		var j = 0;
		for (var i = 0; i < maskWords.Length; i++)
		{
			if (maskWords[i] == 0UL)
				continue;

			indices[j++] = i;
		}

		return indices;
	}

	public ulong[] BuildMaskWords(int[] typeIds)
	{
		var words = new ulong[_maskWordCount];
		for (var i = 0; i < typeIds.Length; i++)
		{
			int typeId = typeIds[i];
			int wordIndex = typeId >> 6;
			int bitIndex = typeId & 63;
			words[wordIndex] |= 1UL << bitIndex;
		}

		return words;
	}

	public void SetComponentBoxed(Entity entity, Type componentType, object value)
	{
		if (componentType is null) throw new ArgumentNullException(nameof(componentType));
		if (value is null) throw new ArgumentNullException(nameof(value));
		if (!componentType.IsValueType)
			throw new ArgumentException("Components must be value types.", nameof(componentType));

		EnsureAlive(entity);
		int typeId = GetOrCreateComponentTypeId(componentType, _world.ContainsReferencesForEntityStore(componentType));
		SetComponentInternalBoxed(entity.Id, typeId, componentType, value);
	}

	public void SetRelationComponent(Entity source, int relationTypeId)
	{
		var marker = default(RelationMarker);
		SetComponentInternal(source.Id, relationTypeId, in marker);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private uint AdvanceComponentChangeVersion() =>
		_world.AdvanceComponentChangeVersionForEntityStore();

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private void MarkComponentAdded(int entityId, int typeId, uint version) =>
		_world.MarkComponentAddedForEntityStore(entityId, typeId, version);

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private void MarkComponentChanged(int entityId, int typeId, uint version) =>
		_world.MarkComponentChangedForEntityStore(entityId, typeId, version);

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

		var pairs = new int[sharedColumnCount * 2];
		var pairIndex = 0;
		for (var i = 0; i < targetArchetype.TypeIds.Length; i++)
		{
			int typeId = targetArchetype.TypeIds[i];
			int sourceColumnIndex = sourceArchetype.GetColumnIndexOrNegative(typeId);
			if (sourceColumnIndex < 0)
				continue;

			pairs[pairIndex++] = sourceColumnIndex;
			pairs[pairIndex++] = i;
		}

		_transitionCopyMapByPair[pairKey] = pairs;
		return pairs;
	}

	private void MoveEntireChunkToArchetype(
		ArchetypeStorage sourceArchetype,
		int sourceChunkIndex,
		ArchetypeStorage.Chunk sourceChunk,
		ArchetypeStorage targetArchetype,
		int[] sourceTargetColumnPairs)
	{
		int rowCount = sourceChunk.Count;
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
			int targetRow = targetRowStart;
			for (; sourceRow < sourceRowEnd; sourceRow++, targetRow++)
			{
				int entityId = sourceChunk.EntityIds[sourceRow];
				_locationByEntityId[entityId] = new(targetArchetype.Id, targetChunkIndex, targetRow);
			}
		}

		sourceArchetype.ClearChunk(sourceChunkIndex);
	}

	private void MoveEntityToArchetype(
		int entityId,
		Bezoro.ECS.Internal.Fixed.EntityLocation sourceLocation,
		ArchetypeStorage sourceArchetype,
		int targetArchetypeId)
	{
		if (targetArchetypeId == sourceArchetype.Id)
			return;

		var targetArchetype = _archetypes[targetArchetypeId];
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
		int entityId,
		Bezoro.ECS.Internal.Fixed.EntityLocation sourceLocation,
		ArchetypeStorage sourceArchetype,
		ArchetypeStorage targetArchetype,
		int[] sourceTargetColumnPairs)
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
		int entityId,
		Bezoro.ECS.Internal.Fixed.EntityLocation sourceLocation,
		ArchetypeStorage sourceArchetype,
		int targetArchetypeId,
		int setTypeId,
		in T setComponent)
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

		var targetArchetype = _archetypes[targetArchetypeId];
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
		int entityId,
		Bezoro.ECS.Internal.Fixed.EntityLocation sourceLocation,
		ArchetypeStorage sourceArchetype,
		ArchetypeStorage targetArchetype,
		int[] sourceTargetColumnPairs,
		int setTypeId,
		in T setComponent)
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
		setRef = setComponent;
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
		int entityId,
		Bezoro.ECS.Internal.Fixed.EntityLocation sourceLocation,
		ArchetypeStorage sourceArchetype,
		int targetArchetypeId,
		int setTypeId,
		in T setComponent)
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
		int entityId,
		Bezoro.ECS.Internal.Fixed.EntityLocation sourceLocation,
		ArchetypeStorage sourceArchetype,
		int targetArchetypeId,
		int setTypeId,
		Type setComponentType,
		object setComponent)
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
		int entityId,
		int typeId,
		Type componentType,
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

	private void UpdateMovedEntityLocation(int archetypeId, int chunkIndex, int rowIndex, int movedEntityId)
	{
		var moved = _locationByEntityId[movedEntityId];
		_locationByEntityId[movedEntityId] = new(archetypeId, chunkIndex, rowIndex);
		if (moved.ArchetypeId != archetypeId || moved.ChunkIndex != chunkIndex)
			throw new InvalidOperationException("Moved entity location update is inconsistent.");
	}

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

	private static bool IsEntityMarked(uint[] markerBits, int entityId)
	{
		int markerWordIndex = entityId >> 5;
		uint markerBit = 1u << (entityId & 31);
		return (markerBits[markerWordIndex] & markerBit) != 0u;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static bool TryResolveAccessorColumnIndex(
		ArchetypeStorage archetype,
		int archetypeId,
		int typeId,
		ref int cachedArchetypeId,
		ref int cachedColumnIndex)
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
		var result = new int[source.Length + 1];
		var src = 0;
		var dst = 0;
		var inserted = false;
		while (src < source.Length)
		{
			if (!inserted && typeId < source[src])
			{
				result[dst++] = typeId;
				inserted = true;
			}

			result[dst++] = source[src++];
		}

		if (!inserted)
			result[dst] = typeId;

		return result;
	}

	private static int[] RemoveTypeIdSorted(int[] source, int typeId)
	{
		var result = new int[source.Length - 1];
		var dst = 0;
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
}
