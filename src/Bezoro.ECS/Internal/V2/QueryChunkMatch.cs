namespace Bezoro.ECS.Internal.V2;

internal readonly struct QueryChunkMatch(
	int archetypeId,
	int chunkIndex,
	int rowStart,
	int count,
	int entityStartIndex
)
{
	public int ArchetypeId { get; } = archetypeId;

	public int ChunkIndex { get; } = chunkIndex;

	public int RowStart { get; } = rowStart;

	public int Count { get; } = count;

	public int EntityStartIndex { get; } = entityStartIndex;
}
