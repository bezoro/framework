namespace Bezoro.Core.Common.Extensions;

/// <summary>
/// Provides extension methods for <see cref="int"/> values, enabling math utilities, state queries, and range validations.
/// </summary>
public static class IntExtensions
{
	/// <summary>
	/// Determines whether the integer is even.
	/// </summary>
	/// <param name="value">The integer value.</param>
	/// <returns><c>true</c> if the value is even; otherwise, <c>false</c>.</returns>
	public static bool IsEven(this int value) => value % 2 == 0;

	/// <summary>
	/// Determines whether the integer is negative.
	/// </summary>
	/// <param name="value">The integer value.</param>
	/// <returns><c>true</c> if the value is less than zero; otherwise, <c>false</c>.</returns>
	public static bool IsNegative(this int value) => value < 0;

	/// <summary>
	/// Determines whether the integer is odd.
	/// </summary>
	/// <param name="value">The integer value.</param>
	/// <returns><c>true</c> if the value is odd; otherwise, <c>false</c>.</returns>
	public static bool IsOdd(this int value) => value % 2 != 0;

	/// <summary>
	/// Determines whether the integer is positive (greater than zero).
	/// </summary>
	/// <param name="value">The integer value.</param>
	/// <returns><c>true</c> if the value is greater than zero; otherwise, <c>false</c>.</returns>
	public static bool IsPositive(this int value) => value > 0;

	/// <summary>
	/// Determines whether the integer is zero.
	/// </summary>
	/// <param name="value">The integer value.</param>
	/// <returns><c>true</c> if the value equals zero; otherwise, <c>false</c>.</returns>
	public static bool IsZero(this int value) => value == 0;

	/// <summary>
	/// Returns the absolute (non-negative) value of the given integer.
	/// </summary>
	/// <param name="value">The integer value.</param>
	/// <returns>The absolute value.</returns>
	public static int Abs(this int value) => value < 0 ? -value : value;

	/// <summary>
	/// Restricts an integer to the specified minimum and maximum bounds.
	/// </summary>
	/// <param name="value">The value to clamp.</param>
	/// <param name="min">The minimum value to return.</param>
	/// <param name="max">The maximum value to return.</param>
	/// <returns>The value if within range; otherwise, <paramref name="min"/> or <paramref name="max"/> as appropriate.</returns>
	public static int Clamp(this int value, int min, int max) => value < min ? min : value > max ? max : value;

	/// <summary>
	/// Restricts an integer to not exceed a maximum value.
	/// </summary>
	/// <param name="value">The value to clamp.</param>
	/// <param name="max">The maximum value to return.</param>
	/// <returns>The value if less than or equal to <paramref name="max"/>; otherwise, <paramref name="max"/>.</returns>
	public static int ClampMax(this int value, int max) => value > max ? max : value;

	/// <summary>
	/// Restricts an integer to not be less than a minimum value.
	/// </summary>
	/// <param name="value">The value to clamp.</param>
	/// <param name="min">The minimum value to return.</param>
	/// <returns>The value if greater than or equal to <paramref name="min"/>; otherwise, <paramref name="min"/>.</returns>
	public static int ClampMin(this int value, int min) => value < min ? min : value;

	/// <summary>
	/// Rounds an integer to the nearest multiple of the specified value.
	/// </summary>
	/// <param name="value">The value to round.</param>
	/// <param name="nearest">The multiple to round to.</param>
	/// <returns>The value rounded to the nearest multiple of <paramref name="nearest"/>.</returns>
	public static int RoundToNearest(this int value, int nearest) => (value + nearest / 2) / nearest * nearest;

	/// <summary>
	/// Returns the sign of the integer.
	/// </summary>
	/// <param name="value">The integer value.</param>
	/// <returns>-1 if the value is negative; 1 otherwise (including zero).</returns>
	public static int Sign(this int value) => value < 0 ? -1 : 1;

	/// <summary>
	/// Throws a <see cref="ValueTooSmallException"/> if <paramref name="value"/> is less than <paramref name="min"/>.
	/// </summary>
	/// <param name="value">The integer value to check.</param>
	/// <param name="min">The minimum allowed value.</param>
	/// <returns>The validated integer value.</returns>
	/// <exception cref="ValueTooSmallException">Thrown if <paramref name="value"/> is less than <paramref name="min"/>.</exception>
	public static int ThrowIfLessThan(this int value, int min)
	{
		if (value < min) throw new ValueTooSmallException(value, min);
		return value;
	}

	/// <summary>
	/// Throws a <see cref="ValueTooLargeException"/> if <paramref name="value"/> is greater than <paramref name="max"/>.
	/// </summary>
	/// <param name="value">The integer value to check.</param>
	/// <param name="max">The maximum allowed value.</param>
	/// <returns>The validated integer value.</returns>
	/// <exception cref="ValueTooLargeException">Thrown if <paramref name="value"/> is greater than <paramref name="max"/>.</exception>
	public static int ThrowIfMoreThan(this int value, int max)
	{
		if (value > max) throw new ValueTooLargeException(value, max);
		return value;
	}
}
