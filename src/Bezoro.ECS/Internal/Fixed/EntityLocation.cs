namespace Bezoro.ECS.Internal.Fixed;

internal readonly struct EntityLocation(int archetypeId, int chunkIndex, int rowIndex)
{
	public static EntityLocation Invalid { get; } = new(-1, -1, -1);

	public bool IsValid => ArchetypeId >= 0;

	public int ArchetypeId { get; } = archetypeId;

	public int ChunkIndex { get; } = chunkIndex;

	public int RowIndex { get; } = rowIndex;
}
