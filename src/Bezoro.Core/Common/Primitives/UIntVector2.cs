using System;
using System.Numerics;

namespace Bezoro.Core.Common.Primitives;

/// <summary>
///     A simple 2D vector of unsigned integers.
/// </summary>
public readonly record struct UIntVector2(uint X, uint Y)
{
	private const float WHOLE_TOLERANCE = 1e-6f;

	/// <summary>
	///     Explicit conversion from Vector2 to UIntVector2 using the same validation as <see cref="FromVector2" />.
	/// </summary>
	public static explicit operator UIntVector2(Vector2 v) => FromVector2(v);

	/// <summary>
	///     Implicit conversion from UIntVector2 to Vector2.
	/// </summary>
	public static implicit operator Vector2(UIntVector2 v) => v.ToVector2();

	/// <summary>
	///     Attempts to create a UIntVector2 from a floating-point Vector2 without throwing.
	/// </summary>
	public static bool TryFromVector2(Vector2 v, out UIntVector2? result)
	{
		if (IsValidComponent(v.X) && IsValidComponent(v.Y))
		{
			result = new((uint)MathF.Round(v.X), (uint)MathF.Round(v.Y));
			return true;
		}

		result = null;
		return false;
	}

	/// <summary>
	///     Creates a UIntVector2 from a floating-point Vector2.
	///     Throws if components are not finite, not whole numbers, negative, or exceed UInt32.MaxValue.
	/// </summary>
	/// <exception cref="ArgumentOutOfRangeException">
	///     Thrown when one or both components are invalid for conversion to uint.
	/// </exception>
	public static UIntVector2 FromVector2(Vector2 v)
	{
		if (!IsValidComponent(v.X) || !IsValidComponent(v.Y))
			throw new ArgumentOutOfRangeException(
				nameof(v),
				"Vector2 must contain finite, non-negative whole values within the range of UInt32.");

		return new((uint)MathF.Round(v.X), (uint)MathF.Round(v.Y));
	}

	public override string ToString() => $"({X}, {Y})";

	/// <summary>
	///     Converts to a System.Numerics.Vector2.
	/// </summary>
	public Vector2 ToVector2() => new(X, Y);

	private static bool IsValidComponent(float f)
	{
		if (float.IsNaN(f) || float.IsInfinity(f)) return false;
		if (f is < 0f or > uint.MaxValue) return false;

		return MathF.Abs(f - MathF.Round(f)) <= WHOLE_TOLERANCE;
	}
}
