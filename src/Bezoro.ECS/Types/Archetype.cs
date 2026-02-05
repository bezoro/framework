using Bezoro.ECS.Internal;
using Bezoro.ECS.Services;

namespace Bezoro.ECS.Types;

/// <summary>
///     Represents a unique set of component types stored together in chunked SoA layout.
/// </summary>
public sealed class Archetype
{
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

	internal Chunk GetOrCreateChunkWithSpace(out int chunkIndex)
	{
		for (var i = 0; i < Chunks.Count; i++)
		{
			var chunk = Chunks[i];
			if (chunk.Count < ChunkCapacity)
			{
				chunkIndex = i;
				return chunk;
			}
		}

		var newChunk = new Chunk(Types, ChunkCapacity);
		Chunks.Add(newChunk);
		chunkIndex = Chunks.Count - 1;
		return newChunk;
	}

	internal int GetTypeIndex(int typeId)
	{
		if (typeId < 0 || typeId >= TypeIndexById.Length) return -1;

		return TypeIndexById[typeId];
	}

	private static int[] BuildTypeIndex(int[] typeIds)
	{
		int maxId = -1;
		for (var i = 0; i < typeIds.Length; i++)
			if (typeIds[i] > maxId)
				maxId = typeIds[i];

		if (maxId < 0) return [];

		var index = new int[maxId + 1];
		Array.Fill(index, -1);

		for (var i = 0; i < typeIds.Length; i++)
			index[typeIds[i]] = i;

		return index;
	}
}
