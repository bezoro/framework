namespace Bezoro.ECS.Internal;

internal readonly struct EntityLocation
{
	public static readonly EntityLocation Empty = new(-1, -1, -1);

	public EntityLocation(int archetypeId, int chunkIndex, int slotIndex)
	{
		ArchetypeId = archetypeId;
		ChunkIndex  = chunkIndex;
		SlotIndex   = slotIndex;
	}

	public int ArchetypeId { get; }
	public int ChunkIndex { get; }
	public int SlotIndex { get; }

	public bool IsValid => ArchetypeId >= 0 && ChunkIndex >= 0 && SlotIndex >= 0;
}
