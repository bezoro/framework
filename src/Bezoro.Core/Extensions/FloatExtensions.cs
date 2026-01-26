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
	///     Maps a value from one range to another, preserving proportional distance.
	/// </summary>
	/// <param name="value">The float value to map.</param>
	/// <param name="fromMin">The minimum bound of the source range.</param>
	/// <param name="fromMax">The maximum bound of the source range.</param>
	/// <param name="toMin">The minimum bound of the target range.</param>
	/// <param name="toMax">The maximum bound of the target range.</param>
	/// <returns>
	///     The corresponding value in [<paramref name="toMin" />, <paramref name="toMax" />] that maps linearly from
	///     <paramref name="value" /> in [<paramref name="fromMin" />, <paramref name="fromMax" />].
	/// </returns>
	/// <remarks>
	///     The output is not clamped. If <paramref name="value" /> is outside the source range,
	///     the result will be proportionally outside the target range.
	/// </remarks>
	/// <exception cref="ArgumentException">
	///     Thrown when <paramref name="fromMin" /> equals <paramref name="fromMax" /> (zero-width source range).
	/// </exception>
	public static float Map(this float value, float fromMin, float fromMax, float toMin, float toMax)
	{
		float range = fromMax - fromMin;
		if (range == 0f)
			throw new ArgumentException("Source range cannot be zero (fromMin equals fromMax).", nameof(fromMax));

		return (value - fromMin) / range * (toMax - toMin) + toMin;
	}

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
