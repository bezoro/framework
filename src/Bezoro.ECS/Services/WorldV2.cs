using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using Bezoro.ECS.Abstractions;
using Bezoro.ECS.Internal.V2;
using Bezoro.ECS.Types;

namespace Bezoro.ECS.Services;

public sealed class WorldV2 : IDisposable
{
	private static readonly Dictionary<Type, bool> ContainsReferencesByType = [];
	private static readonly MethodInfo ContainsReferencesMethod = typeof(WorldV2).GetMethod(
		nameof(ContainsReferencesGeneric),
		BindingFlags.NonPublic | BindingFlags.Static
	) ?? throw new InvalidOperationException("Missing contains-references helper.");
	private static readonly object ContainsReferencesSync = new();
	private static int _nextInstanceId;

	private readonly WorldV2Config                        _config;
	private readonly bool[]                               _aliveByEntityId;
	private readonly int[]                                _versionByEntityId;
	private readonly int[]                                _freeEntityIds;
	private readonly EntityLocation[]                     _locationByEntityId;
	private readonly Entity[]                             _queryEntities;
	private readonly QueryChunkMatch[]                    _queryChunkMatches;
	private readonly int                                  _maskWordCount;
	private readonly Dictionary<Type, int>                _typeToId = [];
	private readonly Type?[]                              _typeById;
	private readonly bool[]                               _typeIsManagedLane;
	private readonly List<ArchetypeStorage>               _archetypes = [];
	private readonly Dictionary<ArchetypeTypeSetKey, int> _archetypeByTypeSet =
		new(ArchetypeTypeSetKeyComparer.Instance);
	private readonly Dictionary<Type, CompiledQueryPlan> _compiledPlansBySpecType = [];
	private readonly Dictionary<long, int[]>             _transitionCopyMapByPair = [];
	private readonly int                                 _emptyArchetypeId;
	private readonly int                                 _instanceId;

	private bool _disposed;
	private int  _aliveCount;
	private int  _archetypeVersion;
	private int  _activeQueryCursors;
	private int  _componentTypeOverflowCount;
	private int  _entityHighWatermark;
	private int  _entityOverflowCount;
	private int  _freeCount;
	private int  _nextEntityId;
	private int  _queryHighWatermark;
	private int  _queryOverflowCount;
	private int  _registeredTypeHighWatermark;
	private int  _typeCount;

	public WorldV2(WorldV2Config config)
	{
		_instanceId = Interlocked.Increment(ref _nextInstanceId);
		_config = config ?? throw new ArgumentNullException(nameof(config));
		_config.Validate();

		_aliveByEntityId = new bool[_config.EntityCapacity];
		_versionByEntityId = new int[_config.EntityCapacity];
		_freeEntityIds = new int[_config.EntityCapacity];
		_locationByEntityId = new EntityLocation[_config.EntityCapacity];
		for (var i = 0; i < _locationByEntityId.Length; i++)
			_locationByEntityId[i] = EntityLocation.Invalid;

		_queryEntities = new Entity[_config.QueryResultCapacity];
		_queryChunkMatches = new QueryChunkMatch[_config.QueryResultCapacity];
		_typeById = new Type[_config.ComponentTypeCapacity];
		_typeIsManagedLane = new bool[_config.ComponentTypeCapacity];
		_maskWordCount = GetMaskWordCount(_config.ComponentTypeCapacity);

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

	internal int EntityCapacity => _config.EntityCapacity;

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

	public CommandStream BeginCommands() => CreateCommandStream();

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

	public QueryHandle<TSpec> Compile<TSpec>() where TSpec : struct, ICompiledQuerySpec
	{
		ThrowIfDisposed();
		var specType = typeof(TSpec);
		if (_compiledPlansBySpecType.TryGetValue(specType, out var existing))
			return new(existing);

		var builder = new QueryBuilder(this);
		var spec = default(TSpec);
		spec.Build(ref builder);
		var plan = builder.Build();
		_compiledPlansBySpecType[specType] = plan;
		return new(plan);
	}

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

	public ref T Get<T>(Entity entity) where T : unmanaged
	{
		ThrowIfDisposed();
		EnsureAlive(entity);
		int typeId = GetOrCreateComponentTypeId<T>();
		return ref GetComponentRefUnchecked<T>(entity.Id, typeId);
	}

	public bool TryGet<T>(Entity entity, out T component) where T : unmanaged
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

	public ComponentAccessor<T> GetAccessor<T>() where T : unmanaged
	{
		ThrowIfDisposed();
		int typeId = GetOrCreateComponentTypeId<T>();
		return new(this, typeId);
	}

	public bool Has<T>(Entity entity) where T : struct
	{
		ThrowIfDisposed();
		if (!IsAliveUnchecked(entity))
			return false;

		int typeId = GetOrCreateComponentTypeId<T>();
		var location = _locationByEntityId[entity.Id];
		return location.IsValid && _archetypes[location.ArchetypeId].HasType(typeId);
	}

	public bool IsAlive(Entity entity)
	{
		ThrowIfDisposed();
		return IsAliveUnchecked(entity);
	}

	public WorldV2Diagnostics GetDiagnostics()
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

		_aliveCount = 0;
		_freeCount = 0;
		_nextEntityId = 0;
		_activeQueryCursors = 0;
	}

	public void Dispose()
	{
		if (_disposed)
			return;

		for (var i = 0; i < _archetypes.Count; i++)
			_archetypes[i].Dispose();

		_typeToId.Clear();
		_compiledPlansBySpecType.Clear();
		_archetypeByTypeSet.Clear();
		_archetypes.Clear();
		_disposed = true;
	}

	internal void ExitQueryCursor()
	{
		if (_activeQueryCursors > 0)
			_activeQueryCursors = 0;
	}

	internal ulong[] BuildMaskWords(int[] typeIds)
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
		var j = 0;
		for (var i = 0; i < maskWords.Length; i++)
		{
			if (maskWords[i] == 0UL)
				continue;
			indices[j++] = i;
		}

		return indices;
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
		_typeToId[componentType] = typeId;
		_typeById[typeId] = componentType;
		_typeIsManagedLane[typeId] = RuntimeHelpers.IsReferenceOrContainsReferences<T>();
		if (_typeCount > _registeredTypeHighWatermark)
			_registeredTypeHighWatermark = _typeCount;
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
		_typeToId[componentType] = typeId;
		_typeById[typeId] = componentType;
		_typeIsManagedLane[typeId] = ContainsReferences(componentType);
		if (_typeCount > _registeredTypeHighWatermark)
			_registeredTypeHighWatermark = _typeCount;
		return typeId;
	}

	internal Entity CreateEntityInternal()
	{
		int id;
		if (_freeCount > 0)
			id = _freeEntityIds[--_freeCount];
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

	internal void DestroyEntityInternal(Entity entity)
	{
		EnsureAlive(entity);
		int entityId = entity.Id;
		var location = _locationByEntityId[entityId];
		var archetype = _archetypes[location.ArchetypeId];
		if (archetype.RemoveAt(location.ChunkIndex, location.RowIndex, out int movedEntityId))
			UpdateMovedEntityLocation(location.ArchetypeId, location.ChunkIndex, location.RowIndex, movedEntityId);

		_aliveByEntityId[entityId] = false;
		_locationByEntityId[entityId] = EntityLocation.Invalid;
		_versionByEntityId[entityId]++;
		_freeEntityIds[_freeCount++] = entityId;
		_aliveCount--;
	}

	internal void ApplySetFromCommand<T>(Entity entity, in T component) where T : struct
	{
		EnsureAlive(entity);
		int typeId = GetOrCreateComponentTypeId<T>();
		SetComponentInternal(entity.Id, typeId, in component);
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

	internal void DescribeRemoveTransition(Entity entity, int typeId, out int sourceArchetypeId, out int targetArchetypeId)
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

		var sourceArchetype = _archetypes[sourceArchetypeId];
		var targetArchetype = _archetypes[targetArchetypeId];
		var sourceTargetColumnPairs = GetOrCreateTransitionCopyMap(sourceArchetype, targetArchetype);
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

	internal void RemoveMarkedComponentsFromCommandKnownTransitionFast(
		uint[] batchEntityMarkerBits,
		int   sourceArchetypeId,
		int   targetArchetypeId)
	{
		if (targetArchetypeId == sourceArchetypeId)
			return;

		var sourceArchetype = _archetypes[sourceArchetypeId];
		var targetArchetype = _archetypes[targetArchetypeId];
		var sourceTargetColumnPairs = GetOrCreateTransitionCopyMap(sourceArchetype, targetArchetype);
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
					sourceArchetype.MoveRowRangeWithinChunk(
						chunk,
						runStart,
						writeRow,
						runLength
					);

					int runWriteEnd = writeRow + runLength;
					for (var row = writeRow; row < runWriteEnd; row++)
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

	internal void RemoveAllComponentsFromCommandKnownTransitionFast(int sourceArchetypeId, int targetArchetypeId)
	{
		if (targetArchetypeId == sourceArchetypeId)
			return;

		var sourceArchetype = _archetypes[sourceArchetypeId];
		var targetArchetype = _archetypes[targetArchetypeId];
		var sourceTargetColumnPairs = GetOrCreateTransitionCopyMap(sourceArchetype, targetArchetype);
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

	private void MoveEntireChunkToArchetype(
		ArchetypeStorage       sourceArchetype,
		int                    sourceChunkIndex,
		ArchetypeStorage.Chunk sourceChunk,
		ArchetypeStorage       targetArchetype,
		int[]                  sourceTargetColumnPairs)
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

			var sourceRowEnd = sourceRow + reservedRows;
			var targetRow = targetRowStart;
			for (; sourceRow < sourceRowEnd; sourceRow++, targetRow++)
			{
				int entityId = sourceChunk.EntityIds[sourceRow];
				_locationByEntityId[entityId] = new(targetArchetype.Id, targetChunkIndex, targetRow);
			}
		}

		sourceArchetype.ClearChunk(sourceChunkIndex);
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

	internal int GetArchetypeEntityCount(int archetypeId)
	{
		if ((uint)archetypeId >= (uint)_archetypes.Count)
			throw new ArgumentOutOfRangeException(nameof(archetypeId));

		return _archetypes[archetypeId].EntityCount;
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
		for (var i = 0; i < count; i++)
		{
			int payloadIndex = payloadIndices[payloadOffset + i];
			if ((uint)payloadIndex >= (uint)payloadCount)
				throw new InvalidOperationException(
					$"Payload index '{payloadIndex}' is out of range for '{typeof(T).Name}'."
				);

			int entityId = entityIds[entityOffset + i];
			ref readonly T component = ref payloads[payloadIndex];
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
			int sourceColumnIndex = sourceArchetype.GetColumnIndexOrNegative(typeId);
			if (sourceColumnIndex < 0)
				throw new InvalidOperationException(
					$"Type id '{typeId}' does not exist in archetype '{sourceArchetypeId}'."
				);

			var cachedChunkIndex = -1;
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
				}

				cachedColumn[location.RowIndex] = payloads[payloadIndex];
			}

			return;
		}

		var targetArchetype = _archetypes[targetArchetypeId];
		var sourceTargetColumnPairs = GetOrCreateTransitionCopyMap(sourceArchetype, targetArchetype);
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

	internal ref T GetComponentRefForCursorUnchecked<T>(int entityId, int typeId) where T : struct
	{
		var location = _locationByEntityId[entityId];
		if (!location.IsValid)
			throw new InvalidOperationException($"Entity '{entityId}' is not in a valid archetype.");

		return ref _archetypes[location.ArchetypeId].GetRef<T>(location.ChunkIndex, location.RowIndex, typeId);
	}

	internal ArchetypeStorage GetArchetypeForCursor(int archetypeId)
	{
		if ((uint)archetypeId >= (uint)_archetypes.Count)
			throw new ArgumentOutOfRangeException(nameof(archetypeId));

		return _archetypes[archetypeId];
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	internal void ResolveAccessorLocation(
		Entity entity,
		int    typeId,
		Type   componentType,
		ref int cachedArchetypeId,
		ref int cachedColumnIndex,
		out ArchetypeStorage archetype,
		out int chunkIndex,
		out int rowIndex)
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
		rowIndex = location.RowIndex;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	internal bool TryGetComponentForAccessor<T>(
		Entity entity,
		int    typeId,
		ref int cachedArchetypeId,
		ref int cachedColumnIndex,
		out T component)
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

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	internal bool HasComponentForAccessor(
		Entity entity,
		int    typeId,
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

		var low = 0;
		var high = chunkMatchCount - 1;
		while (low <= high)
		{
			int middle = low + ((high - low) >> 1);
			var match = chunkMatches[middle];
			int start = match.EntityStartIndex;
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
			var match = chunkMatches[i];
			var chunk = _archetypes[match.ArchetypeId].GetChunk(match.ChunkIndex);
			int rowEnd = match.RowStart + match.Count;
			for (var row = match.RowStart; row < rowEnd; row++)
			{
				int entityId = chunk.EntityIds[row];
				destination[writeIndex++] = new(entityId, _versionByEntityId[entityId]);
			}
		}

		if (writeIndex != entityCount)
			throw new InvalidOperationException("Query entity materialization count mismatch.");
	}

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

		int typeId1 = GetOrCreateComponentTypeId<T1>();
		var cachedArchetypeId = int.MinValue;
		ArchetypeStorage? cachedArchetype = null;
		var cachedColumnIndex1 = -1;
		for (var i = 0; i < chunkMatchCount; i++)
		{
			var match = _queryChunkMatches[i];
			if (match.ArchetypeId != cachedArchetypeId)
			{
				cachedArchetypeId = match.ArchetypeId;
				cachedArchetype = _archetypes[match.ArchetypeId];
				cachedColumnIndex1 = cachedArchetype.GetColumnIndexOrNegative(typeId1);
				if (cachedColumnIndex1 < 0)
					throw new KeyNotFoundException(
						$"Type id '{typeId1}' does not exist in archetype '{match.ArchetypeId}'."
					);
			}

			if (match.Count == 0)
				continue;

			var chunk = cachedArchetype!.GetChunkUnchecked(match.ChunkIndex);
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

		int typeId1 = GetOrCreateComponentTypeId<T1>();
		int typeId2 = GetOrCreateComponentTypeId<T2>();
		var cachedArchetypeId = int.MinValue;
		ArchetypeStorage? cachedArchetype = null;
		var cachedColumnIndex1 = -1;
		var cachedColumnIndex2 = -1;
		for (var i = 0; i < chunkMatchCount; i++)
		{
			var match = _queryChunkMatches[i];
			if (match.ArchetypeId != cachedArchetypeId)
			{
				cachedArchetypeId = match.ArchetypeId;
				cachedArchetype = _archetypes[match.ArchetypeId];
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

			var chunk = cachedArchetype!.GetChunkUnchecked(match.ChunkIndex);
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

		int typeId1 = GetOrCreateComponentTypeId<T1>();
		int typeId2 = GetOrCreateComponentTypeId<T2>();
		int typeId3 = GetOrCreateComponentTypeId<T3>();
		var cachedArchetypeId = int.MinValue;
		ArchetypeStorage? cachedArchetype = null;
		var cachedColumnIndex1 = -1;
		var cachedColumnIndex2 = -1;
		var cachedColumnIndex3 = -1;
		for (var i = 0; i < chunkMatchCount; i++)
		{
			var match = _queryChunkMatches[i];
			if (match.ArchetypeId != cachedArchetypeId)
			{
				cachedArchetypeId = match.ArchetypeId;
				cachedArchetype = _archetypes[match.ArchetypeId];
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

			var chunk = cachedArchetype!.GetChunkUnchecked(match.ChunkIndex);
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

		int typeId1 = GetOrCreateComponentTypeId<T1>();
		var cachedArchetypeId = int.MinValue;
		ArchetypeStorage? cachedArchetype = null;
		var cachedColumnIndex1 = -1;
		for (var i = 0; i < chunkMatchCount; i++)
		{
			var match = _queryChunkMatches[i];
			if (match.ArchetypeId != cachedArchetypeId)
			{
				cachedArchetypeId = match.ArchetypeId;
				cachedArchetype = _archetypes[match.ArchetypeId];
				cachedColumnIndex1 = cachedArchetype.GetColumnIndexOrNegative(typeId1);
				if (cachedColumnIndex1 < 0)
					throw new KeyNotFoundException(
						$"Type id '{typeId1}' does not exist in archetype '{match.ArchetypeId}'."
					);
			}

			if (match.Count == 0)
				continue;

			var chunk = cachedArchetype!.GetChunkUnchecked(match.ChunkIndex);
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

		int typeId1 = GetOrCreateComponentTypeId<T1>();
		int typeId2 = GetOrCreateComponentTypeId<T2>();
		var cachedArchetypeId = int.MinValue;
		ArchetypeStorage? cachedArchetype = null;
		var cachedColumnIndex1 = -1;
		var cachedColumnIndex2 = -1;
		for (var i = 0; i < chunkMatchCount; i++)
		{
			var match = _queryChunkMatches[i];
			if (match.ArchetypeId != cachedArchetypeId)
			{
				cachedArchetypeId = match.ArchetypeId;
				cachedArchetype = _archetypes[match.ArchetypeId];
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

			var chunk = cachedArchetype!.GetChunkUnchecked(match.ChunkIndex);
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

		int typeId1 = GetOrCreateComponentTypeId<T1>();
		int typeId2 = GetOrCreateComponentTypeId<T2>();
		int typeId3 = GetOrCreateComponentTypeId<T3>();
		var cachedArchetypeId = int.MinValue;
		ArchetypeStorage? cachedArchetype = null;
		var cachedColumnIndex1 = -1;
		var cachedColumnIndex2 = -1;
		var cachedColumnIndex3 = -1;
		for (var i = 0; i < chunkMatchCount; i++)
		{
			var match = _queryChunkMatches[i];
			if (match.ArchetypeId != cachedArchetypeId)
			{
				cachedArchetypeId = match.ArchetypeId;
				cachedArchetype = _archetypes[match.ArchetypeId];
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

			var chunk = cachedArchetype!.GetChunkUnchecked(match.ChunkIndex);
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

	private int FillQueryResults(CompiledQueryPlan plan, out int chunkMatchCount)
	{
		int count = 0;
		chunkMatchCount = 0;
		int matchCount = GetOrRefreshMatchingArchetypes(plan);
		var matches = plan.MatchingArchetypeIds;
		for (var i = 0; i < matchCount; i++)
		{
			var archetype = _archetypes[matches[i]];
			for (var chunkIndex = 0; chunkIndex < archetype.ChunkCount; chunkIndex++)
			{
				var chunk = archetype.GetChunk(chunkIndex);
				if (chunk.Count == 0)
					continue;

				int remaining = _queryEntities.Length - count;
				if (remaining <= 0)
				{
					_queryOverflowCount++;
					if (_config.OverflowPolicy == WorldV2OverflowPolicy.FailFast)
						throw new InvalidOperationException(
							$"Query result capacity '{_config.QueryResultCapacity}' was exceeded."
						);

					goto done;
				}

				int rowsToAppend = Math.Min(remaining, chunk.Count);
				if (!TryAppendQueryChunk(
						new(matches[i], chunkIndex, 0, rowsToAppend, count),
						ref chunkMatchCount
					))
					goto done;

				count += rowsToAppend;
				if (rowsToAppend < chunk.Count)
				{
					_queryOverflowCount++;
					if (_config.OverflowPolicy == WorldV2OverflowPolicy.FailFast)
						throw new InvalidOperationException(
							$"Query result capacity '{_config.QueryResultCapacity}' was exceeded."
						);

					goto done;
				}
			}
		}

done:
		if (count > _queryHighWatermark)
			_queryHighWatermark = count;
		return count;
	}

	private int GetOrRefreshMatchingArchetypes(CompiledQueryPlan plan)
	{
		if (plan.ArchetypeCacheVersion == _archetypeVersion)
			return plan.MatchingArchetypeCount;

		var matches = plan.MatchingArchetypeIds;
		if (matches.Length < _archetypes.Count)
			plan.MatchingArchetypeIds = matches = new int[_archetypes.Count];

		var count = 0;
		for (var i = 0; i < _archetypes.Count; i++)
		{
			if (!ArchetypeMatches(plan, _archetypes[i].MaskWords))
				continue;
			matches[count++] = i;
		}

		plan.MatchingArchetypeCount = count;
		plan.ArchetypeCacheVersion = _archetypeVersion;
		return count;
	}

	private bool ArchetypeMatches(CompiledQueryPlan plan, ulong[] archetypeMaskWords)
	{
		for (var i = 0; i < plan.AllMaskWordIndices.Length; i++)
		{
			int wordIndex = plan.AllMaskWordIndices[i];
			ulong allWord = plan.AllMaskWords[wordIndex];
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

	private bool TryAppendQueryChunk(in QueryChunkMatch chunkMatch, ref int chunkMatchCount)
	{
		if (chunkMatchCount >= _queryChunkMatches.Length)
		{
			_queryOverflowCount++;
			if (_config.OverflowPolicy == WorldV2OverflowPolicy.FailFast)
				throw new InvalidOperationException(
					$"Query result capacity '{_config.QueryResultCapacity}' was exceeded."
				);

			return false;
		}

		_queryChunkMatches[chunkMatchCount++] = chunkMatch;
		return true;
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
			return;
		}

		int targetArchetypeId = GetOrCreateAddTransition(sourceArchetype, typeId);
		MoveEntityToArchetypeWithSet(entityId, location, sourceArchetype, targetArchetypeId, typeId, in component);
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

	private void MoveEntityToArchetype(int entityId, EntityLocation sourceLocation, ArchetypeStorage sourceArchetype, int targetArchetypeId)
	{
		if (targetArchetypeId == sourceArchetype.Id)
			return;

		var targetArchetype = _archetypes[targetArchetypeId];
		var sourceTargetColumnPairs = GetOrCreateTransitionCopyMap(sourceArchetype, targetArchetype);
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
			UpdateMovedEntityLocation(sourceArchetype.Id, sourceLocation.ChunkIndex, sourceLocation.RowIndex, movedEntityId);
	}

	private void MoveEntityToArchetypeWithSet<T>(
		int entityId,
		EntityLocation sourceLocation,
		ArchetypeStorage sourceArchetype,
		int targetArchetypeId,
		int setTypeId,
		in T setComponent)
		where T : struct
	{
		if (targetArchetypeId == sourceArchetype.Id)
		{
			ref var current = ref sourceArchetype.GetRef<T>(sourceLocation.ChunkIndex, sourceLocation.RowIndex, setTypeId);
			current = setComponent;
			return;
		}

		var targetArchetype = _archetypes[targetArchetypeId];
		var sourceTargetColumnPairs = GetOrCreateTransitionCopyMap(sourceArchetype, targetArchetype);
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

	private void MoveEntityToArchetypeWithSetKnownTransition<T>(
		int entityId,
		EntityLocation sourceLocation,
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
		_locationByEntityId[entityId] = new(targetArchetype.Id, targetChunkIndex, targetRowIndex);

		if (sourceArchetype.RemoveAt(sourceLocation.ChunkIndex, sourceLocation.RowIndex, out int movedEntityId))
			UpdateMovedEntityLocation(sourceArchetype.Id, sourceLocation.ChunkIndex, sourceLocation.RowIndex, movedEntityId);
	}

	private int[] GetOrCreateTransitionCopyMap(ArchetypeStorage sourceArchetype, ArchetypeStorage targetArchetype)
	{
		long pairKey = ((long)sourceArchetype.Id << 32) | (uint)targetArchetype.Id;
		if (_transitionCopyMapByPair.TryGetValue(pairKey, out var existing))
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

	private void UpdateMovedEntityLocation(int archetypeId, int chunkIndex, int rowIndex, int movedEntityId)
	{
		var moved = _locationByEntityId[movedEntityId];
		_locationByEntityId[movedEntityId] = new(archetypeId, chunkIndex, rowIndex);
		if (moved.ArchetypeId != archetypeId || moved.ChunkIndex != chunkIndex)
			throw new InvalidOperationException("Moved entity location update is inconsistent.");
	}

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

	private ref T GetComponentRefUnchecked<T>(int entityId, int typeId) where T : struct
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

	private bool TryGetComponentUnchecked<T>(int entityId, int typeId, out T component) where T : struct
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

	private static int GetMaskWordCount(int componentTypeCapacity) =>
		Math.Max(1, (componentTypeCapacity + 63) / 64);

	private void ThrowIfDirectIterationUnavailable<TSpec>(QueryHandle<TSpec> handle)
		where TSpec : struct, ICompiledQuerySpec
	{
		if (_activeQueryCursors > 0)
			throw new InvalidOperationException("Direct query iteration cannot run while a query cursor is active.");
		if (!ReferenceEquals(handle.Plan.Owner, this))
			throw new InvalidOperationException("Query handle belongs to a different world.");
	}

	private void EnsureAlive(Entity entity)
	{
		if (!IsAliveUnchecked(entity))
			throw new InvalidOperationException($"Entity '{entity.Id}:{entity.Version}' is not alive.");
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private bool IsAliveUnchecked(Entity entity)
	{
		return entity.Id >= 0 &&
			   entity.Id < _aliveByEntityId.Length &&
			   _aliveByEntityId[entity.Id] &&
			   _versionByEntityId[entity.Id] == entity.Version;
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

	private void ThrowIfDisposed()
	{
		if (_disposed)
			throw new ObjectDisposedException(nameof(WorldV2));
	}

	private static bool ContainsReferences(Type type)
	{
		lock (ContainsReferencesSync)
		{
			if (ContainsReferencesByType.TryGetValue(type, out bool cached))
				return cached;

			bool contains = (bool)(ContainsReferencesMethod.MakeGenericMethod(type).Invoke(null, null) ?? false);
			ContainsReferencesByType[type] = contains;
			return contains;
		}
	}

	private static bool ContainsReferencesGeneric<T>() where T : struct =>
		RuntimeHelpers.IsReferenceOrContainsReferences<T>();

	private static class ComponentTypeIdCache<T> where T : struct
	{
		public static int OwnerInstanceId;
		public static int TypeId;
	}
}
