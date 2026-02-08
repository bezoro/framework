namespace Bezoro.ECS.Internal;

internal readonly struct TypeIdTripleKey : IEquatable<TypeIdTripleKey>
{
	public TypeIdTripleKey(int first, int second, int third)
	{
		First  = Min(first, second, third);
		Third  = Max(first, second, third);
		Second = first + second + third - First - Third;
	}

	public int First { get; }

	public int Second { get; }

	public int Third { get; }

	public bool Equals(TypeIdTripleKey other) =>
		First == other.First &&
		Second == other.Second &&
		Third == other.Third;

	public override bool Equals(object? obj) => obj is TypeIdTripleKey other && Equals(other);

	public override int GetHashCode() => HashCode.Combine(First, Second, Third);

	private static int Max(int first, int second, int third)
	{
		int max = first > second ? first : second;
		return max > third ? max : third;
	}

	private static int Min(int first, int second, int third)
	{
		int min = first < second ? first : second;
		return min < third ? min : third;
	}
}
