namespace Bezoro.ECS.Internal.Fixed;

internal readonly struct ArchetypeTypeSetKey(int[] typeIds)
{
	public int[] TypeIds { get; } = typeIds;
}
