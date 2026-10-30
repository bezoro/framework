using System.Runtime.CompilerServices;
using Bezoro.Core.Types.Exceptions;

namespace Bezoro.Core.Extensions;

/// <summary>
///     Provides extension methods for <see cref="int" /> values, enabling math utilities, state queries, and range
///     validations.
/// </summary>
public static class IntExtensions
{
	/// <summary>
	///     Determines whether <paramref name="value" /> is within the inclusive range defined by <paramref name="min" /> and
	///     <paramref name="max" />.
	/// </summary>
	/// <param name="value">The value to check.</param>
	/// <param name="min">The minimum (inclusive) value.</param>
	/// <param name="max">The maximum (inclusive) value.</param>
	/// <returns>
	///     <c>true</c> if <paramref name="value" /> is greater than or equal to <paramref name="min" /> and less than or
	///     equal to <paramref name="max" />; otherwise, <c>false</c>.
	/// </returns>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static bool IsBetween(this int value, int min, int max) => value >= min && value <= max;

	/// <summary>
	///     Determines whether the integer is even.
	/// </summary>
	/// <param name="value">The integer value.</param>
	/// <returns><c>true</c> if the value is even; otherwise, <c>false</c>.</returns>
	public static bool IsEven(this int value) => value % 2 == 0;

	/// <summary>
	///     Determines whether the integer is negative.
	/// </summary>
	/// <param name="value">The integer value.</param>
	/// <returns><c>true</c> if the value is less than zero; otherwise, <c>false</c>.</returns>
	public static bool IsNegative(this int value) => value < 0;

	/// <summary>
	///     Determines whether the integer is odd.
	/// </summary>
	/// <param name="value">The integer value.</param>
	/// <returns><c>true</c> if the value is odd; otherwise, <c>false</c>.</returns>
	public static bool IsOdd(this int value) => value % 2 != 0;

	/// <summary>
	///     Determines whether the integer is positive (greater than zero).
	/// </summary>
	/// <param name="value">The integer value.</param>
	/// <returns><c>true</c> if the value is greater than zero; otherwise, <c>false</c>.</returns>
	public static bool IsPositive(this int value) => value > 0;

	/// <summary>
	///     Determines whether the integer is zero.
	/// </summary>
	/// <param name="value">The integer value.</param>
	/// <returns><c>true</c> if the value equals zero; otherwise, <c>false</c>.</returns>
	public static bool IsZero(this int value) => value == 0;

	/// <summary>
	///     Returns the absolute (non-negative) value of the given integer.
	/// </summary>
	/// <param name="value">The integer value.</param>
	/// <returns>The absolute value.</returns>
	/// <exception cref="OverflowException">The value is <see cref="int.MinValue" />.</exception>
	public static int Abs(this int value) => Math.Abs(value);

	/// <summary>
	///     Restricts an integer to the specified minimum and maximum bounds.
	/// </summary>
	/// <param name="value">The value to clamp.</param>
	/// <param name="min">The minimum value to return.</param>
	/// <param name="max">The maximum value to return.</param>
	/// <returns>The value if within range; otherwise, <paramref name="min" /> or <paramref name="max" /> as appropriate.</returns>
	public static int Clamp(this int value, int min, int max) => value < min ? min : value > max ? max : value;

	/// <summary>
	///     Restricts an integer to not exceed a maximum value.
	/// </summary>
	/// <param name="value">The value to clamp.</param>
	/// <param name="max">The maximum value to return.</param>
	/// <returns>The value if less than or equal to <paramref name="max" />; otherwise, <paramref name="max" />.</returns>
	public static int ClampMax(this int value, int max) => value > max ? max : value;

	/// <summary>
	///     Restricts an integer to not be less than a minimum value.
	/// </summary>
	/// <param name="value">The value to clamp.</param>
	/// <param name="min">The minimum value to return.</param>
	/// <returns>The value if greater than or equal to <paramref name="min" />; otherwise, <paramref name="min" />.</returns>
	public static int ClampMin(this int value, int min) => value < min ? min : value;

	/// <summary>
	///     Rounds an integer to the nearest multiple of the specified value.
	/// </summary>
	/// <param name="value">The value to round.</param>
	/// <param name="nearest">The positive multiple to round to.</param>
	/// <returns>The nearest multiple, with exact midpoints rounded away from zero.</returns>
	/// <exception cref="ArgumentOutOfRangeException">The multiple is not positive.</exception>
	/// <exception cref="OverflowException">The nearest multiple is outside the Int32 range.</exception>
	public static int RoundToNearest(this int value, int nearest)
	{
		if (nearest <= 0) throw new ArgumentOutOfRangeException(nameof(nearest));
		long magnitude = Math.Abs((long)value);
		long rounded = (magnitude + nearest / 2L) / nearest * nearest;
		return checked((int)(value < 0 ? -rounded : rounded));
	}

	/// <summary>
	///     Returns the sign of the integer.
	/// </summary>
	/// <param name="value">The integer value.</param>
	/// <returns>-1 if the value is negative; 1 otherwise (including zero).</returns>
	public static int Sign(this int value) => value < 0 ? -1 : 1;

	/// <summary>
	///     Throws a <see cref="ValueTooSmallException" /> if <paramref name="value" /> is less than <paramref name="min" />.
	/// </summary>
	/// <param name="value">The integer value to check.</param>
	/// <param name="min">The minimum allowed value.</param>
	/// <returns>The validated integer value.</returns>
	/// <exception cref="ValueTooSmallException">Thrown if <paramref name="value" /> is less than <paramref name="min" />.</exception>
	public static int ThrowIfLessThan(this int value, int min)
	{
		if (value < min) throw new ValueTooSmallException(value, min);

		return value;
	}

	/// <summary>
	///     Throws a <see cref="ValueTooLargeException" /> if <paramref name="value" /> is greater than <paramref name="max" />
	///     .
	/// </summary>
	/// <param name="value">The integer value to check.</param>
	/// <param name="max">The maximum allowed value.</param>
	/// <returns>The validated integer value.</returns>
	/// <exception cref="ValueTooLargeException">Thrown if <paramref name="value" /> is greater than <paramref name="max" />.</exception>
	public static int ThrowIfMoreThan(this int value, int max)
	{
		if (value > max) throw new ValueTooLargeException(value, max);

		return value;
	}
}
