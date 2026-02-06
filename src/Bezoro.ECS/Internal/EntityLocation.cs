namespace Bezoro.ECS.Internal;

internal readonly struct EntityLocation
{
	public static readonly EntityLocation Empty = new(-1, -1);

	public EntityLocation(int archetypeId, int rowIndex)
	{
		ArchetypeId = archetypeId;
		RowIndex = rowIndex;
	}

	public int ArchetypeId { get; }
	public int RowIndex { get; }

	public bool IsValid => ArchetypeId >= 0 && RowIndex >= 0;
}
