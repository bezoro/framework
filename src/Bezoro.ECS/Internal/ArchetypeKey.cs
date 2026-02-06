namespace Bezoro.ECS.Internal;

internal readonly struct ArchetypeKey : IEquatable<ArchetypeKey>
{
	private readonly int _hashCode;

	public ArchetypeKey(int[] typeIds)
	{
		TypeIds   = typeIds ?? throw new ArgumentNullException(nameof(typeIds));
		_hashCode = ComputeHashCode(typeIds);
	}

	public int[] TypeIds { get; }

	#region Equality

	public bool Equals(ArchetypeKey other)
	{
		if (_hashCode != other._hashCode) return false;
		if (ReferenceEquals(TypeIds, other.TypeIds)) return true;
		if (TypeIds.Length != other.TypeIds.Length) return false;

		for (var i = 0; i < TypeIds.Length; i++)
		{
			if (TypeIds[i] != other.TypeIds[i])
				return false;
		}

		return true;
	}

	public override bool Equals(object? obj) =>
		obj is ArchetypeKey other && Equals(other);

	public override int GetHashCode() => _hashCode;

	#endregion

	private static int ComputeHashCode(int[] typeIds)
	{
		var hash = new HashCode();
		for (var i = 0; i < typeIds.Length; i++)
			hash.Add(typeIds[i]);

		return hash.ToHashCode();
	}
}
