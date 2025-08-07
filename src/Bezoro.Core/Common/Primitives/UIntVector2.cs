using System;
using System.Numerics;

namespace Bezoro.Core.Common.Primitives;

/// <summary>
///     A simple 2D vector of unsigned integers.
/// </summary>
public readonly struct UIntVector2 : IEquatable<UIntVector2>
{
	public UIntVector2(uint x, uint y)
	{
		X = x;
		Y = y;
	}

	public uint X { get; }
	public uint Y { get; }

	public static bool operator ==(UIntVector2 a, UIntVector2 b) => a.Equals(b);
	public static bool operator !=(UIntVector2 a, UIntVector2 b) => !a.Equals(b);

	#region Equality

	#region Interface Implementations

	public bool Equals(UIntVector2 other) => X == other.X && Y == other.Y;

	#endregion

	public override bool Equals(object? obj) => obj is UIntVector2 v && Equals(v);
	public override int  GetHashCode()       => HashCode.Combine(X, Y);

	#endregion

	/// <summary>
	///     Optionally convert from a floating‐point Vector2 if you really need to.
	///     Throws if not a whole, non-negative number.
	/// </summary>
	public static UIntVector2 FromVector2(Vector2 v)
	{
		if (v.X < 0 || v.Y < 0 || v.X % 1 != 0 || v.Y % 1 != 0)
			throw new ArgumentException("Vector2 must be non-negative whole values", nameof(v));

		return new((uint)v.X, (uint)v.Y);
	}

	public override string ToString() => $"({X}, {Y})";
}
