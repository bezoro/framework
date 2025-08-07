using System;

namespace Bezoro.Core;

/// <summary>
///     Helper class for robust floating-point number comparisons.
///     Handles precision issues inherent in floating-point arithmetic.
/// </summary>
public static class FloatComparer
{
	#region Constants

	/// <summary>
	///     Default absolute epsilon for float comparisons.
	/// </summary>
	public const float DefaultFloatEpsilon = 1e-6f;

	/// <summary>
	///     Default absolute epsilon for double comparisons.
	/// </summary>
	public const double DefaultDoubleEpsilon = 1e-15;

	/// <summary>
	///     Default relative epsilon for relative comparisons.
	/// </summary>
	public const float DefaultRelativeEpsilon = 1e-5f;

	/// <summary>
	///     Default relative epsilon for double relative comparisons.
	/// </summary>
	public const double DefaultDoubleRelativeEpsilon = 1e-14;

	#endregion

	#region Float Comparisons

	/// <summary>
	///     Determines if two float values are equal using absolute epsilon comparison.
	/// </summary>
	/// <param name="a">First float value</param>
	/// <param name="b">Second float value</param>
	/// <param name="epsilon">Absolute epsilon tolerance (default: 1e-6f)</param>
	/// <returns>True if the values are considered equal within the epsilon tolerance</returns>
	public static bool AreEqual(float a, float b, float epsilon = DefaultFloatEpsilon)
	{
		// Handle exact matches, infinities, and NaN
		if (a == b) return true;

		// Handle NaN cases
		if (float.IsNaN(a) || float.IsNaN(b)) return false;

		// Handle infinity cases
		if (float.IsInfinity(a) || float.IsInfinity(b)) return false;

		return Math.Abs(a - b) < epsilon;
	}

	/// <summary>
	///     Determines if two float values are equal using relative epsilon comparison.
	///     Better for comparing larger numbers.
	/// </summary>
	/// <param name="a">First float value</param>
	/// <param name="b">Second float value</param>
	/// <param name="relativeEpsilon">Relative epsilon tolerance (default: 1e-5f)</param>
	/// <returns>True if the values are considered equal within the relative tolerance</returns>
	public static bool AreEqualRelative(float a, float b, float relativeEpsilon = DefaultRelativeEpsilon)
	{
		// Handle exact matches, infinities, and NaN
		if (a == b) return true;

		// Handle NaN cases
		if (float.IsNaN(a) || float.IsNaN(b)) return false;

		// Handle infinity cases
		if (float.IsInfinity(a) || float.IsInfinity(b)) return false;

		float diff    = Math.Abs(a - b);
		float largest = Math.Max(Math.Abs(a), Math.Abs(b));

		return diff <= relativeEpsilon * largest;
	}

	/// <summary>
	///     Robust comparison using both absolute and relative epsilon.
	///     Uses absolute epsilon for numbers close to zero, relative epsilon for larger numbers.
	/// </summary>
	/// <param name="a">First float value</param>
	/// <param name="b">Second float value</param>
	/// <param name="absoluteEpsilon">Absolute epsilon tolerance (default: 1e-6f)</param>
	/// <param name="relativeEpsilon">Relative epsilon tolerance (default: 1e-5f)</param>
	/// <returns>True if the values are considered equal</returns>
	public static bool AreEqualRobust(
		float a,
		float b,
		float absoluteEpsilon = DefaultFloatEpsilon,
		float relativeEpsilon = DefaultRelativeEpsilon)
	{
		// Handle exact matches, infinities, and NaN
		if (a == b) return true;

		// Handle NaN cases
		if (float.IsNaN(a) || float.IsNaN(b)) return false;

		// Handle infinity cases
		if (float.IsInfinity(a) || float.IsInfinity(b)) return false;

		float diff = Math.Abs(a - b);

		// Use absolute epsilon for numbers close to zero
		if (Math.Abs(a) < absoluteEpsilon || Math.Abs(b) < absoluteEpsilon) return diff < absoluteEpsilon;

		// Use relative epsilon for larger numbers
		float largest = Math.Max(Math.Abs(a), Math.Abs(b));
		return diff < relativeEpsilon * largest;
	}

	/// <summary>
	///     Determines if the first float is less than the second.
	/// </summary>
	public static bool IsLessThan(float a, float b, float epsilon = DefaultFloatEpsilon) =>
		a < b && !AreEqual(a, b, epsilon);

	/// <summary>
	///     Determines if the first float is greater than the second.
	/// </summary>
	public static bool IsGreaterThan(float a, float b, float epsilon = DefaultFloatEpsilon) =>
		a > b && !AreEqual(a, b, epsilon);

	/// <summary>
	///     Determines if the first float is less than or equal to the second.
	/// </summary>
	public static bool IsLessThanOrEqual(float a, float b, float epsilon = DefaultFloatEpsilon) =>
		a < b || AreEqual(a, b, epsilon);

	/// <summary>
	///     Determines if the first float is greater than or equal to the second.
	/// </summary>
	public static bool IsGreaterThanOrEqual(float a, float b, float epsilon = DefaultFloatEpsilon) =>
		a > b || AreEqual(a, b, epsilon);

	/// <summary>
	///     Determines if a float value is effectively zero.
	/// </summary>
	public static bool IsZero(float value, float epsilon = DefaultFloatEpsilon) =>
		Math.Abs(value) < epsilon;

	#endregion

	#region Double Comparisons

	/// <summary>
	///     Determines if two double values are equal using absolute epsilon comparison.
	/// </summary>
	/// <param name="a">First double value</param>
	/// <param name="b">Second double value</param>
	/// <param name="epsilon">Absolute epsilon tolerance (default: 1e-15)</param>
	/// <returns>True if the values are considered equal within the epsilon tolerance</returns>
	public static bool AreEqual(double a, double b, double epsilon = DefaultDoubleEpsilon)
	{
		// Handle exact matches, infinities, and NaN
		if (a == b) return true;

		// Handle NaN cases
		if (double.IsNaN(a) || double.IsNaN(b)) return false;

		// Handle infinity cases
		if (double.IsInfinity(a) || double.IsInfinity(b)) return false;

		return Math.Abs(a - b) < epsilon;
	}

	/// <summary>
	///     Determines if two double values are equal using relative epsilon comparison.
	///     Better for comparing larger numbers.
	/// </summary>
	/// <param name="a">First double value</param>
	/// <param name="b">Second double value</param>
	/// <param name="relativeEpsilon">Relative epsilon tolerance (default: 1e-14)</param>
	/// <returns>True if the values are considered equal within the relative tolerance</returns>
	public static bool AreEqualRelative(double a, double b, double relativeEpsilon = DefaultDoubleRelativeEpsilon)
	{
		// Handle exact matches, infinities, and NaN
		if (a == b) return true;

		// Handle NaN cases
		if (double.IsNaN(a) || double.IsNaN(b)) return false;

		// Handle infinity cases
		if (double.IsInfinity(a) || double.IsInfinity(b)) return false;

		double diff    = Math.Abs(a - b);
		double largest = Math.Max(Math.Abs(a), Math.Abs(b));

		return diff <= relativeEpsilon * largest;
	}

	/// <summary>
	///     Robust comparison using both absolute and relative epsilon.
	///     Uses absolute epsilon for numbers close to zero, relative epsilon for larger numbers.
	/// </summary>
	/// <param name="a">First double value</param>
	/// <param name="b">Second double value</param>
	/// <param name="absoluteEpsilon">Absolute epsilon tolerance (default: 1e-15)</param>
	/// <param name="relativeEpsilon">Relative epsilon tolerance (default: 1e-14)</param>
	/// <returns>True if the values are considered equal</returns>
	public static bool AreEqualRobust(
		double a,
		double b,
		double absoluteEpsilon = DefaultDoubleEpsilon,
		double relativeEpsilon = DefaultDoubleRelativeEpsilon)
	{
		// Handle exact matches, infinities, and NaN
		if (a == b) return true;

		// Handle NaN cases
		if (double.IsNaN(a) || double.IsNaN(b)) return false;

		// Handle infinity cases
		if (double.IsInfinity(a) || double.IsInfinity(b)) return false;

		double diff = Math.Abs(a - b);

		// Use absolute epsilon for numbers close to zero
		if (Math.Abs(a) < absoluteEpsilon || Math.Abs(b) < absoluteEpsilon) return diff < absoluteEpsilon;

		// Use relative epsilon for larger numbers
		double largest = Math.Max(Math.Abs(a), Math.Abs(b));
		return diff < relativeEpsilon * largest;
	}

	/// <summary>
	///     Determines if the first double is less than the second.
	/// </summary>
	public static bool IsLessThan(double a, double b, double epsilon = DefaultDoubleEpsilon) =>
		a < b && !AreEqual(a, b, epsilon);

	/// <summary>
	///     Determines if the first double is greater than the second.
	/// </summary>
	public static bool IsGreaterThan(double a, double b, double epsilon = DefaultDoubleEpsilon) =>
		a > b && !AreEqual(a, b, epsilon);

	/// <summary>
	///     Determines if the first double is less than or equal to the second.
	/// </summary>
	public static bool IsLessThanOrEqual(double a, double b, double epsilon = DefaultDoubleEpsilon) =>
		a < b || AreEqual(a, b, epsilon);

	/// <summary>
	///     Determines if the first double is greater than or equal to the second.
	/// </summary>
	public static bool IsGreaterThanOrEqual(double a, double b, double epsilon = DefaultDoubleEpsilon) =>
		a > b || AreEqual(a, b, epsilon);

	/// <summary>
	///     Determines if a double value is effectively zero.
	/// </summary>
	public static bool IsZero(double value, double epsilon = DefaultDoubleEpsilon) =>
		Math.Abs(value) < epsilon;

	#endregion

	#region Utility Methods

	/// <summary>
	///     Clamps a float value to be within the specified range.
	/// </summary>
	public static float Clamp(float value, float min, float max)
	{
		if (value < min) return min;

		if (value > max) return max;

		return value;
	}

	/// <summary>
	///     Clamps a double value to be within the specified range.
	/// </summary>
	public static double Clamp(double value, double min, double max)
	{
		if (value < min) return min;

		if (value > max) return max;

		return value;
	}

	/// <summary>
	///     Returns the sign of a float value, treating values close to zero as zero.
	/// </summary>
	public static int Sign(float value, float epsilon = DefaultFloatEpsilon)
	{
		if (IsZero(value, epsilon)) return 0;

		return Math.Sign(value);
	}

	/// <summary>
	///     Returns the sign of a double value, treating values close to zero as zero.
	/// </summary>
	public static int Sign(double value, double epsilon = DefaultDoubleEpsilon)
	{
		if (IsZero(value, epsilon)) return 0;

		return Math.Sign(value);
	}

	#endregion
}
