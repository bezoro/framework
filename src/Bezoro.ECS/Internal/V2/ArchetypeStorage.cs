namespace Bezoro.ECS.Internal.V2;

internal sealed class ArchetypeStorage
{
	private const int UnknownTransition = int.MinValue;

	private readonly int[]   _addTransitionByTypeId;
	private readonly int[]   _columnIndexByTypeId;
	private readonly bool[]  _columnIsManagedLane;
	private readonly int[]   _removeTransitionByTypeId;
	private readonly Type[]  _columnTypes;
	private readonly int     _chunkCapacity;
	private          Chunk[] _chunks;
	private          int     _chunkCount;
	private          int     _firstAvailableChunkIndex;

	public ArchetypeStorage(
		int    id,
		int[]  typeIds,
		ulong[] maskWords,
		Type[] columnTypes,
		bool[] columnIsManagedLane,
		int    componentTypeCapacity,
		int    chunkCapacity)
	{
		Id = id;
		TypeIds = typeIds;
		MaskWords = maskWords;
		_columnTypes = columnTypes;
		_columnIsManagedLane = columnIsManagedLane;
		_chunkCapacity = chunkCapacity;

		_columnIndexByTypeId = new int[componentTypeCapacity];
		Array.Fill(_columnIndexByTypeId, -1);
		for (var i = 0; i < typeIds.Length; i++)
			_columnIndexByTypeId[typeIds[i]] = i;

		_addTransitionByTypeId = new int[componentTypeCapacity];
		_removeTransitionByTypeId = new int[componentTypeCapacity];
		Array.Fill(_addTransitionByTypeId, UnknownTransition);
		Array.Fill(_removeTransitionByTypeId, UnknownTransition);

		_chunks = new Chunk[4];
	}

	public int Id { get; }

	public int EntityCount { get; private set; }

	public ulong[] MaskWords { get; }

	public int[] TypeIds { get; }

	public int ChunkCount => _chunkCount;

	public bool HasType(int typeId) =>
		(uint)typeId < (uint)_columnIndexByTypeId.Length &&
		_columnIndexByTypeId[typeId] >= 0;

	public int GetKnownAddTransition(int typeId) =>
		(uint)typeId < (uint)_addTransitionByTypeId.Length
			? _addTransitionByTypeId[typeId]
			: UnknownTransition;

	public int GetKnownRemoveTransition(int typeId) =>
		(uint)typeId < (uint)_removeTransitionByTypeId.Length
			? _removeTransitionByTypeId[typeId]
			: UnknownTransition;

	public void SetKnownAddTransition(int typeId, int targetArchetypeId)
	{
		if ((uint)typeId >= (uint)_addTransitionByTypeId.Length)
			return;

		_addTransitionByTypeId[typeId] = targetArchetypeId;
	}

	public void SetKnownRemoveTransition(int typeId, int targetArchetypeId)
	{
		if ((uint)typeId >= (uint)_removeTransitionByTypeId.Length)
			return;

		_removeTransitionByTypeId[typeId] = targetArchetypeId;
	}

	public bool TryGetColumnIndex(int typeId, out int columnIndex)
	{
		if ((uint)typeId < (uint)_columnIndexByTypeId.Length)
		{
			columnIndex = _columnIndexByTypeId[typeId];
			return columnIndex >= 0;
		}

		columnIndex = -1;
		return false;
	}

	public Chunk GetChunk(int chunkIndex)
	{
		if ((uint)chunkIndex >= (uint)_chunkCount)
			throw new ArgumentOutOfRangeException(nameof(chunkIndex));

		return _chunks[chunkIndex];
	}

	public Chunk GetChunkUnchecked(int chunkIndex) => _chunks[chunkIndex];

	public void AllocateRow(int entityId, out int chunkIndex, out int rowIndex)
	{
		_ = ReserveRows(1, out chunkIndex, out rowIndex);
		_chunks[chunkIndex].EntityIds[rowIndex] = entityId;
	}

	public int ReserveRows(int requestedCount, out int chunkIndex, out int rowStart)
	{
		if (requestedCount <= 0)
			throw new ArgumentOutOfRangeException(nameof(requestedCount));

		for (var i = _firstAvailableChunkIndex; i < _chunkCount; i++)
		{
			var candidate = _chunks[i];
			int available = _chunkCapacity - candidate.Count;
			if (available <= 0)
				continue;

			rowStart = candidate.Count;
			int reserved = Math.Min(requestedCount, available);
			candidate.Count += reserved;
			EntityCount += reserved;
			chunkIndex = i;
			if (candidate.Count >= _chunkCapacity && i == _firstAvailableChunkIndex)
			{
				_firstAvailableChunkIndex = i + 1;
				while (_firstAvailableChunkIndex < _chunkCount &&
					   _chunks[_firstAvailableChunkIndex].Count >= _chunkCapacity)
					_firstAvailableChunkIndex++;
			}

			return reserved;
		}

		if (_chunkCount == _chunks.Length)
			Array.Resize(ref _chunks, _chunks.Length * 2);

		var chunk = new Chunk(_chunkCapacity, _columnTypes);
		_chunks[_chunkCount] = chunk;
		chunkIndex = _chunkCount++;
		rowStart = 0;
		int reservedRows = Math.Min(requestedCount, _chunkCapacity);
		chunk.Count = reservedRows;
		EntityCount += reservedRows;
		_firstAvailableChunkIndex = chunkIndex;
		if (chunk.Count >= _chunkCapacity)
		{
			_firstAvailableChunkIndex++;
			while (_firstAvailableChunkIndex < _chunkCount &&
				   _chunks[_firstAvailableChunkIndex].Count >= _chunkCapacity)
				_firstAvailableChunkIndex++;
		}

		return reservedRows;
	}

	public bool RemoveAt(int chunkIndex, int rowIndex, out int movedEntityId)
	{
		var chunk = GetChunk(chunkIndex);
		if ((uint)rowIndex >= (uint)chunk.Count)
			throw new ArgumentOutOfRangeException(nameof(rowIndex));

		var lastRowIndex = chunk.Count - 1;
		movedEntityId = -1;
		if (rowIndex != lastRowIndex)
		{
			movedEntityId = chunk.EntityIds[lastRowIndex];
			chunk.EntityIds[rowIndex] = movedEntityId;

			for (var c = 0; c < chunk.Columns.Length; c++)
				Array.Copy(chunk.Columns[c], lastRowIndex, chunk.Columns[c], rowIndex, 1);
		}

		for (var c = 0; c < chunk.Columns.Length; c++)
		{
			if (_columnIsManagedLane[c])
				Array.Clear(chunk.Columns[c], lastRowIndex, 1);
		}

		chunk.EntityIds[lastRowIndex] = 0;
		chunk.Count--;
		EntityCount--;
		if (chunk.Count < _chunkCapacity && chunkIndex < _firstAvailableChunkIndex)
			_firstAvailableChunkIndex = chunkIndex;
		return movedEntityId >= 0;
	}

	public void ClearChunk(int chunkIndex)
	{
		var chunk = GetChunk(chunkIndex);
		if (chunk.Count == 0)
			return;

		int count = chunk.Count;
		for (var c = 0; c < chunk.Columns.Length; c++)
		{
			if (_columnIsManagedLane[c])
				Array.Clear(chunk.Columns[c], 0, count);
		}

		Array.Clear(chunk.EntityIds, 0, count);
		chunk.Count = 0;
		EntityCount -= count;
		if (chunkIndex < _firstAvailableChunkIndex)
			_firstAvailableChunkIndex = chunkIndex;
	}

	public void FinalizeChunkCompaction(int chunkIndex, int newCount)
	{
		var chunk = GetChunk(chunkIndex);
		int oldCount = chunk.Count;
		if ((uint)newCount > (uint)oldCount)
			throw new ArgumentOutOfRangeException(nameof(newCount));

		if (newCount == oldCount)
			return;

		int removedCount = oldCount - newCount;
		for (var c = 0; c < chunk.Columns.Length; c++)
		{
			if (_columnIsManagedLane[c])
				Array.Clear(chunk.Columns[c], newCount, removedCount);
		}

		Array.Clear(chunk.EntityIds, newCount, removedCount);
		chunk.Count = newCount;
		EntityCount -= removedCount;
		if (newCount < _chunkCapacity && chunkIndex < _firstAvailableChunkIndex)
			_firstAvailableChunkIndex = chunkIndex;
	}

	public void CopySharedColumnsFrom(
		ArchetypeStorage sourceArchetype,
		Chunk            sourceChunk,
		int              sourceRowIndex,
		Chunk            targetChunk,
		int              targetRowIndex)
	{
		for (var i = 0; i < TypeIds.Length; i++)
		{
			int typeId = TypeIds[i];
			if (!sourceArchetype.TryGetColumnIndex(typeId, out int sourceColumnIndex))
				continue;

			Array.Copy(
				sourceChunk.Columns[sourceColumnIndex],
				sourceRowIndex,
				targetChunk.Columns[i],
				targetRowIndex,
				1
			);
		}
	}

	public void CopySharedColumnsFromWithPairs(
		Chunk sourceChunk,
		int   sourceRowIndex,
		Chunk targetChunk,
		int   targetRowIndex,
		int[] sourceTargetColumnPairs)
	{
		CopySharedColumnsFromWithPairs(
			sourceChunk,
			sourceRowIndex,
			targetChunk,
			targetRowIndex,
			1,
			sourceTargetColumnPairs
		);
	}

	public void CopySharedColumnsFromWithPairs(
		Chunk sourceChunk,
		int   sourceRowIndex,
		Chunk targetChunk,
		int   targetRowIndex,
		int   rowCount,
		int[] sourceTargetColumnPairs)
	{
		if (rowCount == 0)
			return;

		for (var i = 0; i < sourceTargetColumnPairs.Length; i += 2)
		{
			int sourceColumnIndex = sourceTargetColumnPairs[i];
			int targetColumnIndex = sourceTargetColumnPairs[i + 1];

			Array.Copy(
				sourceChunk.Columns[sourceColumnIndex],
				sourceRowIndex,
				targetChunk.Columns[targetColumnIndex],
				targetRowIndex,
				rowCount
			);
		}
	}

	public ref T GetRef<T>(int chunkIndex, int rowIndex, int typeId) where T : struct
	{
		if (!TryGetColumnIndex(typeId, out int columnIndex))
			throw new KeyNotFoundException($"Type id '{typeId}' does not exist in archetype '{Id}'.");

		var chunk = GetChunk(chunkIndex);
		if ((uint)rowIndex >= (uint)chunk.Count)
			throw new ArgumentOutOfRangeException(nameof(rowIndex));

		return ref ((T[])chunk.Columns[columnIndex])[rowIndex];
	}

	public bool TryGetValue<T>(int chunkIndex, int rowIndex, int typeId, out T value) where T : struct
	{
		value = default;
		if (!TryGetColumnIndex(typeId, out int columnIndex))
			return false;

		var chunk = GetChunk(chunkIndex);
		if ((uint)rowIndex >= (uint)chunk.Count)
			return false;

		value = ((T[])chunk.Columns[columnIndex])[rowIndex];
		return true;
	}

	public T[] GetColumn<T>(int chunkIndex, int typeId) where T : struct
	{
		if (!TryGetColumnIndex(typeId, out int columnIndex))
			throw new KeyNotFoundException($"Type id '{typeId}' does not exist in archetype '{Id}'.");

		return (T[])GetChunk(chunkIndex).Columns[columnIndex];
	}

	public T[] GetColumnByIndex<T>(int chunkIndex, int columnIndex) where T : struct
	{
		var chunk = GetChunk(chunkIndex);
		if ((uint)columnIndex >= (uint)chunk.Columns.Length)
			throw new ArgumentOutOfRangeException(nameof(columnIndex));

		return (T[])chunk.Columns[columnIndex];
	}

	public T[] GetColumnByIndex<T>(Chunk chunk, int columnIndex) where T : struct
	{
		if ((uint)columnIndex >= (uint)chunk.Columns.Length)
			throw new ArgumentOutOfRangeException(nameof(columnIndex));

		return (T[])chunk.Columns[columnIndex];
	}

	public void Clear()
	{
		for (var chunkIndex = 0; chunkIndex < _chunkCount; chunkIndex++)
		{
			var chunk = _chunks[chunkIndex];
			if (chunk.Count == 0)
				continue;

			for (var columnIndex = 0; columnIndex < chunk.Columns.Length; columnIndex++)
			{
				if (_columnIsManagedLane[columnIndex])
					Array.Clear(chunk.Columns[columnIndex], 0, chunk.Count);
			}

			Array.Clear(chunk.EntityIds, 0, chunk.Count);
			chunk.Count = 0;
		}

		EntityCount = 0;
		_firstAvailableChunkIndex = 0;
	}

	internal sealed class Chunk
	{
		public Chunk(int chunkCapacity, Type[] columnTypes)
		{
			EntityIds = new int[chunkCapacity];
			Columns = new Array[columnTypes.Length];
			for (var i = 0; i < columnTypes.Length; i++)
				Columns[i] = Array.CreateInstance(columnTypes[i], chunkCapacity);
		}

		public Array[] Columns { get; }

		public int Count { get; set; }

		public int[] EntityIds { get; }
	}
}
