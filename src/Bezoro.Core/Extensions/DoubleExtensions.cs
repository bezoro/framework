using System.Runtime.CompilerServices;
using Bezoro.Core.Helpers;

namespace Bezoro.Core.Extensions;

/// <summary>
///     Provides extension methods for <see cref="double" /> values including bounds checking
///     and approximate floating-point comparisons via <see cref="DoubleComparer" />.
/// </summary>
public static class DoubleExtensions
{
	/// <summary>
	///     Determines whether this value is approximately equal to <paramref name="other" /> using robust comparison.
	/// </summary>
	/// <param name="value">The value to compare.</param>
	/// <param name="other">The value to compare against.</param>
	/// <param name="absoluteEpsilon">Absolute epsilon tolerance. Must be non-negative.</param>
	/// <param name="relativeEpsilon">Relative epsilon tolerance. Must be non-negative.</param>
	/// <returns><c>true</c> if the values are considered equal within tolerance; otherwise, <c>false</c>.</returns>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static bool ApproxEquals(
		this double value,
		double other,
		double absoluteEpsilon = DoubleComparer.DEFAULT_DOUBLE_EPSILON,
		double relativeEpsilon = DoubleComparer.DEFAULT_DOUBLE_RELATIVE_EPSILON) =>
		DoubleComparer.AreEqualRobust(value, other, absoluteEpsilon, relativeEpsilon);

	/// <summary>
	///     Determines whether this value is approximately zero.
	/// </summary>
	/// <param name="value">The value to check.</param>
	/// <param name="epsilon">Absolute epsilon tolerance. Must be non-negative.</param>
	/// <returns><c>true</c> if the value is effectively zero within tolerance; otherwise, <c>false</c>.</returns>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static bool IsApproxZero(this double value, double epsilon = DoubleComparer.DEFAULT_DOUBLE_EPSILON) =>
		DoubleComparer.IsZero(value, epsilon);

	/// <summary>
	///     Determines whether this value is approximately greater than <paramref name="other" />.
	/// </summary>
	/// <param name="value">The value to compare.</param>
	/// <param name="other">The value to compare against.</param>
	/// <param name="absoluteEpsilon">Absolute epsilon tolerance. Must be non-negative.</param>
	/// <param name="relativeEpsilon">Relative epsilon tolerance. Must be non-negative.</param>
	/// <returns><c>true</c> if this value is strictly greater than <paramref name="other" /> beyond tolerance.</returns>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static bool IsApproxGreaterThan(
		this double value,
		double other,
		double absoluteEpsilon = DoubleComparer.DEFAULT_DOUBLE_EPSILON,
		double relativeEpsilon = DoubleComparer.DEFAULT_DOUBLE_RELATIVE_EPSILON) =>
		DoubleComparer.IsGreaterThan(value, other, absoluteEpsilon, relativeEpsilon);

	/// <summary>
	///     Determines whether this value is approximately greater than or equal to <paramref name="other" />.
	/// </summary>
	/// <param name="value">The value to compare.</param>
	/// <param name="other">The value to compare against.</param>
	/// <param name="absoluteEpsilon">Absolute epsilon tolerance. Must be non-negative.</param>
	/// <param name="relativeEpsilon">Relative epsilon tolerance. Must be non-negative.</param>
	/// <returns><c>true</c> if this value is greater than or approximately equal to <paramref name="other" />.</returns>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static bool IsApproxGreaterThanOrEqual(
		this double value,
		double other,
		double absoluteEpsilon = DoubleComparer.DEFAULT_DOUBLE_EPSILON,
		double relativeEpsilon = DoubleComparer.DEFAULT_DOUBLE_RELATIVE_EPSILON) =>
		DoubleComparer.IsGreaterThanOrEqual(value, other, absoluteEpsilon, relativeEpsilon);

	/// <summary>
	///     Determines whether this value is approximately less than <paramref name="other" />.
	/// </summary>
	/// <param name="value">The value to compare.</param>
	/// <param name="other">The value to compare against.</param>
	/// <param name="absoluteEpsilon">Absolute epsilon tolerance. Must be non-negative.</param>
	/// <param name="relativeEpsilon">Relative epsilon tolerance. Must be non-negative.</param>
	/// <returns><c>true</c> if this value is strictly less than <paramref name="other" /> beyond tolerance.</returns>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static bool IsApproxLessThan(
		this double value,
		double other,
		double absoluteEpsilon = DoubleComparer.DEFAULT_DOUBLE_EPSILON,
		double relativeEpsilon = DoubleComparer.DEFAULT_DOUBLE_RELATIVE_EPSILON) =>
		DoubleComparer.IsLessThan(value, other, absoluteEpsilon, relativeEpsilon);

	/// <summary>
	///     Determines whether this value is approximately less than or equal to <paramref name="other" />.
	/// </summary>
	/// <param name="value">The value to compare.</param>
	/// <param name="other">The value to compare against.</param>
	/// <param name="absoluteEpsilon">Absolute epsilon tolerance. Must be non-negative.</param>
	/// <param name="relativeEpsilon">Relative epsilon tolerance. Must be non-negative.</param>
	/// <returns><c>true</c> if this value is less than or approximately equal to <paramref name="other" />.</returns>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static bool IsApproxLessThanOrEqual(
		this double value,
		double other,
		double absoluteEpsilon = DoubleComparer.DEFAULT_DOUBLE_EPSILON,
		double relativeEpsilon = DoubleComparer.DEFAULT_DOUBLE_RELATIVE_EPSILON) =>
		DoubleComparer.IsLessThanOrEqual(value, other, absoluteEpsilon, relativeEpsilon);

	/// <summary>
	///     Returns the sign of this value, treating values approximately zero as zero.
	/// </summary>
	/// <param name="value">The value to evaluate.</param>
	/// <param name="epsilon">Absolute epsilon tolerance. Must be non-negative.</param>
	/// <returns>-1, 0, or 1 indicating the approximate sign of the value.</returns>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static int ApproxSign(this double value, double epsilon = DoubleComparer.DEFAULT_DOUBLE_EPSILON) =>
		DoubleComparer.Sign(value, epsilon);

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
	public static bool IsBetween(this double value, double min, double max) => value >= min && value <= max;
}
