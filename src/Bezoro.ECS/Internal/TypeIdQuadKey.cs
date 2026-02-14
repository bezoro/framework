namespace Bezoro.ECS.Internal;

internal readonly struct TypeIdQuadKey : IEquatable<TypeIdQuadKey>
{
	public TypeIdQuadKey(int first, int second, int third, int fourth)
	{
		SortPair(ref first,  ref second);
		SortPair(ref third,  ref fourth);
		SortPair(ref first,  ref third);
		SortPair(ref second, ref fourth);
		SortPair(ref second, ref third);

		First  = first;
		Second = second;
		Third  = third;
		Fourth = fourth;
	}

	public int First { get; }

	public int Fourth { get; }

	public int Second { get; }

	public int Third { get; }

	#region Equality

	public bool Equals(TypeIdQuadKey other) =>
		First == other.First &&
		Second == other.Second &&
		Third == other.Third &&
		Fourth == other.Fourth;

	public override bool Equals(object? obj) => obj is TypeIdQuadKey other && Equals(other);

	public override int GetHashCode() => HashCode.Combine(First, Second, Third, Fourth);

	#endregion

	private static void SortPair(ref int left, ref int right)
	{
		if (left <= right) return;

		(left, right) = (right, left);
	}
}
