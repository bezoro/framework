namespace Bezoro.ECS.Internal.Fixed;

internal sealed class ArchetypeTypeSetKeyComparer : IEqualityComparer<ArchetypeTypeSetKey>
{
	public static ArchetypeTypeSetKeyComparer Instance { get; } = new();

	#region Equality

	public bool Equals(ArchetypeTypeSetKey x, ArchetypeTypeSetKey y)
	{
		if (ReferenceEquals(x.TypeIds, y.TypeIds))
			return true;

		int[] xIds = x.TypeIds;
		int[] yIds = y.TypeIds;
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
		var   hash = new HashCode();
		int[] ids  = obj.TypeIds;
		for (var i = 0; i < ids.Length; i++)
			hash.Add(ids[i]);

		return hash.ToHashCode();
	}

	#endregion
}

internal readonly struct ArchetypeTypeSetKey(int[] typeIds)
{
	public int[] TypeIds { get; } = typeIds;
}
