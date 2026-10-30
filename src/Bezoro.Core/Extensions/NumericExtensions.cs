namespace Bezoro.Core.Extensions;

/// <summary>Integer division and remainder operations for positive chunk sizes and cyclic indices.</summary>
public static class NumericExtensions
{
	/// <summary>Divides and rounds toward negative infinity, including negative numerators.</summary>
	/// <exception cref="ArgumentOutOfRangeException">The divisor is not positive.</exception>
	public static int FloorDivide(this int value, int divisor)
	{
		if (divisor <= 0) throw new ArgumentOutOfRangeException(nameof(divisor), "The divisor must be positive.");
		int quotient = value / divisor;
		return value % divisor < 0 ? quotient - 1 : quotient;
	}

	/// <summary>Divides and rounds toward negative infinity, including negative numerators.</summary>
	/// <exception cref="ArgumentOutOfRangeException">The divisor is not positive.</exception>
	public static long FloorDivide(this long value, long divisor)
	{
		if (divisor <= 0) throw new ArgumentOutOfRangeException(nameof(divisor), "The divisor must be positive.");
		long quotient = value / divisor;
		return value % divisor < 0 ? quotient - 1 : quotient;
	}

	/// <summary>Divides and rounds toward positive infinity without adding to the numerator.</summary>
	/// <exception cref="ArgumentOutOfRangeException">The divisor is not positive.</exception>
	public static int CeilingDivide(this int value, int divisor)
	{
		if (divisor <= 0) throw new ArgumentOutOfRangeException(nameof(divisor), "The divisor must be positive.");
		int quotient = value / divisor;
		return value % divisor > 0 ? quotient + 1 : quotient;
	}

	/// <summary>Divides and rounds toward positive infinity without adding to the numerator.</summary>
	/// <exception cref="ArgumentOutOfRangeException">The divisor is not positive.</exception>
	public static long CeilingDivide(this long value, long divisor)
	{
		if (divisor <= 0) throw new ArgumentOutOfRangeException(nameof(divisor), "The divisor must be positive.");
		long quotient = value / divisor;
		return value % divisor > 0 ? quotient + 1 : quotient;
	}

	/// <summary>Returns the remainder in the half-open interval [0, modulus).</summary>
	/// <exception cref="ArgumentOutOfRangeException">The modulus is not positive.</exception>
	public static int EuclideanModulo(this int value, int modulus)
	{
		if (modulus <= 0) throw new ArgumentOutOfRangeException(nameof(modulus), "The modulus must be positive.");
		int remainder = value % modulus;
		return remainder < 0 ? remainder + modulus : remainder;
	}

	/// <summary>Returns the remainder in the half-open interval [0, modulus).</summary>
	/// <exception cref="ArgumentOutOfRangeException">The modulus is not positive.</exception>
	public static long EuclideanModulo(this long value, long modulus)
	{
		if (modulus <= 0) throw new ArgumentOutOfRangeException(nameof(modulus), "The modulus must be positive.");
		long remainder = value % modulus;
		return remainder < 0 ? remainder + modulus : remainder;
	}
}
