using System.Runtime.CompilerServices;
using Bezoro.Core.Types.Exceptions;

namespace Bezoro.Core.Extensions;

/// <summary>
///     Provides extension methods for <see cref="float" /> values to enforce minimum and maximum bounds.
///     Throws specific exceptions when constraints are violated.
/// </summary>
public static class FloatExtensions
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
	public static bool IsBetween(this float value, float min, float max) => value >= min && value <= max;

	/// <summary>
	///     Ensures the floating point value is greater than the specified minimum.
	///     Throws if the value is less than or equal to <paramref name="min" />.
	/// </summary>
	/// <param name="f">The float value to test.</param>
	/// <param name="min">The minimum exclusive lower bound.</param>
	/// <returns>The float value if it is valid.</returns>
	/// <exception cref="ValueTooSmallException">
	///     Thrown if <paramref name="f" /> is less than or equal to
	///     <paramref name="min" />.
	/// </exception>
	public static float ThrowIfLessOrEqualThan(this float f, float min)
	{
		if (f <= min)
			throw new ValueTooSmallException(f, min);

		return f;
	}

	/// <summary>
	///     Ensures the floating point value is greater than or equal to the specified minimum.
	///     Throws if the value is less than <paramref name="min" />.
	/// </summary>
	/// <param name="f">The float value to test.</param>
	/// <param name="min">The minimum inclusive lower bound.</param>
	/// <returns>The float value if it is valid.</returns>
	/// <exception cref="ValueTooSmallException">Thrown if <paramref name="f" /> is less than <paramref name="min" />.</exception>
	public static float ThrowIfLessThan(this float f, float min)
	{
		if (f < min)
			throw new ValueTooSmallException(f, min);

		return f;
	}

	/// <summary>
	///     Ensures the floating point value is less than the specified maximum.
	///     Throws if the value is greater than or equal to <paramref name="max" />.
	/// </summary>
	/// <param name="f">The float value to test.</param>
	/// <param name="max">The maximum exclusive upper bound.</param>
	/// <returns>The float value if it is valid.</returns>
	/// <exception cref="ValueTooLargeException">
	///     Thrown if <paramref name="f" /> is greater than or equal to
	///     <paramref name="max" />.
	/// </exception>
	public static float ThrowIfOverOrEqualThan(this float f, float max)
	{
		if (f >= max)
			throw new ValueTooLargeException(f, max);

		return f;
	}

	/// <summary>
	///     Ensures the floating point value is less than or equal to the specified maximum.
	///     Throws if the value is greater than <paramref name="max" />.
	/// </summary>
	/// <param name="f">The float value to test.</param>
	/// <param name="max">The maximum inclusive upper bound.</param>
	/// <returns>The float value if it is valid.</returns>
	/// <exception cref="ValueTooLargeException">Thrown if <paramref name="f" /> is greater than <paramref name="max" />.</exception>
	public static float ThrowIfOverThan(this float f, float max)
	{
		if (f > max)
			throw new ValueTooLargeException(f, max);

		return f;
	}
}
