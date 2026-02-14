namespace Bezoro.ECS.Internal.V2;

internal readonly struct EntityLocation(int archetypeId, int chunkIndex, int rowIndex)
{
	public static EntityLocation Invalid { get; } = new(-1, -1, -1);

	public int ArchetypeId { get; } = archetypeId;

	public int ChunkIndex { get; } = chunkIndex;

	public int RowIndex { get; } = rowIndex;

	public bool IsValid => ArchetypeId >= 0;
}
