namespace Bezoro.ECS.Internal.Fixed;

internal readonly struct ArchetypeTypeSetKey(int[] typeIds)
{
	public int[] TypeIds { get; } = typeIds;
}

internal sealed class ArchetypeTypeSetKeyComparer : IEqualityComparer<ArchetypeTypeSetKey>
{
	public static ArchetypeTypeSetKeyComparer Instance { get; } = new();

	public bool Equals(ArchetypeTypeSetKey x, ArchetypeTypeSetKey y)
	{
		if (ReferenceEquals(x.TypeIds, y.TypeIds))
			return true;

		var xIds = x.TypeIds;
		var yIds = y.TypeIds;
		if (xIds.Length != yIds.Length)
			return false;

		for (var i = 0; i < xIds.Length; i++)
		{
			if (xIds[i] != yIds[i])
				return false;
		}

		return true;
	}

	public int GetHashCode(ArchetypeTypeSetKey obj)
	{
		var hash = new HashCode();
		var ids = obj.TypeIds;
		for (var i = 0; i < ids.Length; i++)
			hash.Add(ids[i]);

		return hash.ToHashCode();
	}
}
