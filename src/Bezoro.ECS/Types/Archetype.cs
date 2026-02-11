using Bezoro.ECS.Internal;
using Bezoro.ECS.Services;

namespace Bezoro.ECS.Types;

/// <summary>
///     Represents a unique set of component types stored together in chunked SoA layout.
/// </summary>
public sealed class Archetype
{
	private readonly Dictionary<int, Archetype> _addEdges    = new();
	private readonly Dictionary<int, Archetype> _removeEdges = new();
	private          int                        _firstAvailableChunkIndex;

	internal Archetype(World owner, int id, int[] typeIds, Type[] types, int chunkCapacity)
	{
		Owner         = owner;
		Id            = id;
		TypeIds       = typeIds;
		Types         = types;
		ChunkCapacity = chunkCapacity;
		Chunks        = [];
		TypeIndexById = BuildTypeIndex(typeIds);
	}

	/// <summary>
	///     Gets the number of entities each chunk can store.
	/// </summary>
	public int ChunkCapacity { get; }

	/// <summary>
	///     Gets the unique identifier for this archetype within its world.
	/// </summary>
	public int Id { get; }

	/// <summary>
	///     Gets the component types included in this archetype.
	/// </summary>
	public IReadOnlyList<Type> ComponentTypes => Types;

	internal int[] TypeIds { get; }

	internal int[] TypeIndexById { get; }

	internal List<Chunk> Chunks { get; }

	internal Type[] Types { get; }

	internal World Owner { get; }

	internal bool ContainsAll(int[] requiredTypeIds)
	{
		var i = 0;
		var j = 0;

		while (i < TypeIds.Length && j < requiredTypeIds.Length)
		{
			int current = TypeIds[i];
			int needed  = requiredTypeIds[j];

			if (current == needed)
			{
				i++;
				j++;
				continue;
			}

			if (current < needed)
			{
				i++;
				continue;
			}

			return false;
		}

		return j == requiredTypeIds.Length;
	}

	internal bool ContainsAny(ReadOnlySpan<int> typeIds)
	{
		var i = 0;
		var j = 0;

		while (i < TypeIds.Length && j < typeIds.Length)
		{
			int current = TypeIds[i];
			int check   = typeIds[j];

			if (current == check)
				return true;

			if (current < check)
				i++;
			else
				j++;
		}

		return false;
	}

	internal bool TryGetAddEdge(int typeId, out Archetype archetype) => _addEdges.TryGetValue(typeId, out archetype!);

	internal bool TryGetRemoveEdge(int typeId, out Archetype archetype) =>
		_removeEdges.TryGetValue(typeId, out archetype!);

	internal Chunk GetOrCreateChunkWithSpace(out int chunkIndex)
	{
		for (int i = _firstAvailableChunkIndex; i < Chunks.Count; i++)
		{
			var chunk = Chunks[i];
			if (chunk.Count < ChunkCapacity)
			{
				_firstAvailableChunkIndex = i;
				chunkIndex                = i;
				return chunk;
			}
		}

		var newChunk = new Chunk(Types, ChunkCapacity);
		Chunks.Add(newChunk);
		chunkIndex                = Chunks.Count - 1;
		_firstAvailableChunkIndex = chunkIndex;
		return newChunk;
	}

	internal int GetTypeIndex(int typeId)
	{
		if (typeId < 0 || typeId >= TypeIndexById.Length) return -1;

		return TypeIndexById[typeId];
	}

	internal void ClearChunks()
	{
		for (var i = 0; i < Chunks.Count; i++)
			Chunks[i].DisposeColumns();

		Chunks.Clear();
		_firstAvailableChunkIndex = 0;
	}

	internal void NotifyChunkFreed(int chunkIndex)
	{
		if (chunkIndex < _firstAvailableChunkIndex)
			_firstAvailableChunkIndex = chunkIndex;
	}

	internal void SetAddEdge(int typeId, Archetype archetype) => _addEdges[typeId] = archetype;

	internal void SetRemoveEdge(int typeId, Archetype archetype) => _removeEdges[typeId] = archetype;

	private static int[] BuildTypeIndex(int[] typeIds)
	{
		int maxId = -1;
		for (var i = 0; i < typeIds.Length; i++)
		{
			if (typeIds[i] > maxId)
				maxId = typeIds[i];
		}

		if (maxId < 0) return [];

		var index = new int[maxId + 1];
		Array.Fill(index, -1);

		for (var i = 0; i < typeIds.Length; i++)
			index[typeIds[i]] = i;

		return index;
	}
}
