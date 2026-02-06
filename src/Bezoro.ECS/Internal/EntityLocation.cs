namespace Bezoro.ECS.Internal;

internal readonly struct EntityLocation(int archetypeId, int rowIndex)
{
	public static readonly EntityLocation Empty = new(-1, -1);

	public bool IsValid => ArchetypeId >= 0 && RowIndex >= 0;

	public int ArchetypeId { get; } = archetypeId;
	public int RowIndex    { get; } = rowIndex;
}
