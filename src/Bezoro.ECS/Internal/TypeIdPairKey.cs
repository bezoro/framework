namespace Bezoro.ECS.Internal;

internal readonly struct TypeIdPairKey(int first, int second) : IEquatable<TypeIdPairKey>
{
	public int First { get; } = first <= second ? first : second;

	public int Second { get; } = first <= second ? second : first;

	#region Equality

	public bool Equals(TypeIdPairKey other) => First == other.First && Second == other.Second;

	public override bool Equals(object? obj) => obj is TypeIdPairKey other && Equals(other);

	public override int GetHashCode() => HashCode.Combine(First, Second);

	#endregion
}
