using System.Runtime.CompilerServices;

namespace Bezoro.Core;

/// <summary>
///     Helper class for robust floating-point number comparisons.
///     Handles precision issues inherent in floating-point arithmetic.
/// </summary>
public static class FloatComparer
{
	/// <summary>
	///     Default absolute epsilon for double comparisons.
	/// </summary>
	public const double DEFAULT_DOUBLE_EPSILON = 1e-15;

	/// <summary>
	///     Default relative epsilon for double relative comparisons.
	/// </summary>
	public const double DEFAULT_DOUBLE_RELATIVE_EPSILON = 1e-14;
	/// <summary>
	///     Default absolute epsilon for float comparisons.
	/// </summary>
	public const float DEFAULT_FLOAT_EPSILON = 1e-6f;

	/// <summary>
	///     Default relative epsilon for relative comparisons.
	/// </summary>
	public const float DEFAULT_RELATIVE_EPSILON = 1e-5f;

	/// <summary>
	///     Determines if two float values are equal using absolute epsilon comparison.
	/// </summary>
	/// <param name="a">First float value</param>
	/// <param name="b">Second float value</param>
	/// <param name="epsilon">Absolute epsilon tolerance (default: 1e-6f)</param>
	/// <returns>True if the values are considered equal within the epsilon tolerance</returns>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static bool AreEqual(float a, float b, float epsilon = DEFAULT_FLOAT_EPSILON)
	{
		// Handle exact matches and short-circuit for non-finite inputs
		if (a == b) return true;
		if (!float.IsFinite(a) || !float.IsFinite(b)) return false;

		return MathF.Abs(a - b) <= epsilon;
	}

	/// <summary>
	///     Determines if two double values are equal using absolute epsilon comparison.
	/// </summary>
	/// <param name="a">First double value</param>
	/// <param name="b">Second double value</param>
	/// <param name="epsilon">Absolute epsilon tolerance (default: 1e-15)</param>
	/// <returns>True if the values are considered equal within the epsilon tolerance</returns>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static bool AreEqual(double a, double b, double epsilon = DEFAULT_DOUBLE_EPSILON)
	{
		// Handle exact matches, infinities, and NaN
		if (a == b) return true;

		// Handle NaN cases
		if (double.IsNaN(a) || double.IsNaN(b)) return false;

		// Handle infinity cases
		if (double.IsInfinity(a) || double.IsInfinity(b)) return false;

		return Math.Abs(a - b) <= epsilon;
	}

	/// <summary>
	///     Determines if two float values are equal using relative epsilon comparison.
	///     Better for comparing larger numbers.
	/// </summary>
	/// <param name="a">First float value</param>
	/// <param name="b">Second float value</param>
	/// <param name="relativeEpsilon">Relative epsilon tolerance (default: 1e-5f)</param>
	/// <returns>True if the values are considered equal within the relative tolerance</returns>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static bool AreEqualRelative(float a, float b, float relativeEpsilon = DEFAULT_RELATIVE_EPSILON)
	{
		// Handle exact matches, infinities, and NaN
		if (a == b) return true;

		// Handle NaN cases
		if (float.IsNaN(a) || float.IsNaN(b)) return false;

		// Handle infinity cases
		if (float.IsInfinity(a) || float.IsInfinity(b)) return false;

		// Compute in double precision to reduce rounding error and add a ULP-sized cushion at this scale
		double da     = a;
		double db     = b;
		double diff   = Math.Abs(da - db);
		double maxAbs = Math.Max(Math.Abs(da), Math.Abs(db));
		double thresh = relativeEpsilon * maxAbs;

		// float machine epsilon near 1.0f
		const double FLOAT_MACHINE_EPS = 1.1920928955078125e-07;
		double       cushion           = FLOAT_MACHINE_EPS * maxAbs;

		return diff <= thresh + cushion;
	}

	/// <summary>
	///     Determines if two double values are equal using relative epsilon comparison.
	///     Better for comparing larger numbers.
	/// </summary>
	/// <param name="a">First double value</param>
	/// <param name="b">Second double value</param>
	/// <param name="relativeEpsilon">Relative epsilon tolerance (default: 1e-14)</param>
	/// <returns>True if the values are considered equal within the relative tolerance</returns>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static bool AreEqualRelative(double a, double b, double relativeEpsilon = DEFAULT_DOUBLE_RELATIVE_EPSILON)
	{
		// Handle exact matches, infinities, and NaN
		if (a == b) return true;

		// Handle NaN cases
		if (double.IsNaN(a) || double.IsNaN(b)) return false;

		// Handle infinity cases
		if (double.IsInfinity(a) || double.IsInfinity(b)) return false;

		double diff   = Math.Abs(a - b);
		double maxAbs = Math.Max(Math.Abs(a), Math.Abs(b));
		double thresh = relativeEpsilon * maxAbs;

		// double machine epsilon near 1.0
		const double DOUBLE_MACHINE_EPS = 2.2204460492503131e-16;
		double       cushion            = DOUBLE_MACHINE_EPS * maxAbs;

		return diff <= thresh + cushion;
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
		float absoluteEpsilon = DEFAULT_FLOAT_EPSILON,
		float relativeEpsilon = DEFAULT_RELATIVE_EPSILON)
	{
		// Handle exact matches, infinities, and NaN
		if (a == b) return true;

		// Handle NaN cases
		if (float.IsNaN(a) || float.IsNaN(b)) return false;

		// Handle infinity cases
		if (float.IsInfinity(a) || float.IsInfinity(b)) return false;

		float diff   = MathF.Abs(a - b);
		float maxAbs = Math.Max(MathF.Abs(a), MathF.Abs(b));

		// Use absolute epsilon for numbers close to zero
		if (maxAbs <= absoluteEpsilon) return diff <= absoluteEpsilon;

		// Use relative epsilon for larger numbers (in double precision with a ULP-sized cushion)
		{
			double       ddiff             = diff;
			double       dmaxAbs           = maxAbs;
			double       dThresh           = relativeEpsilon * dmaxAbs;
			const double FLOAT_MACHINE_EPS = 1.1920928955078125e-07;
			double       cushion           = FLOAT_MACHINE_EPS * dmaxAbs;

			return ddiff <= dThresh + cushion;
		}
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
		double absoluteEpsilon = DEFAULT_DOUBLE_EPSILON,
		double relativeEpsilon = DEFAULT_DOUBLE_RELATIVE_EPSILON)
	{
		// Handle exact matches, infinities, and NaN
		if (a == b) return true;

		// Handle NaN cases
		if (double.IsNaN(a) || double.IsNaN(b)) return false;

		// Handle infinity cases
		if (double.IsInfinity(a) || double.IsInfinity(b)) return false;

		double diff   = Math.Abs(a - b);
		double maxAbs = Math.Max(Math.Abs(a), Math.Abs(b));

		// Use absolute epsilon for numbers close to zero
		if (maxAbs <= absoluteEpsilon) return diff <= absoluteEpsilon;

		// Use relative epsilon for larger numbers with a ULP-sized cushion
		{
			double       dThresh            = relativeEpsilon * maxAbs;
			const double DOUBLE_MACHINE_EPS = 2.2204460492503131e-16;
			double       cushion            = DOUBLE_MACHINE_EPS * maxAbs;
			return diff <= dThresh + cushion;
		}
	}

	/// <summary>
	///     Determines if the first float is greater than the second.
	/// </summary>
	public static bool IsGreaterThan(float a, float b, float epsilon = DEFAULT_FLOAT_EPSILON) =>
		a > b && !AreEqual(a, b, epsilon);

	/// <summary>
	///     Determines if the first double is greater than the second.
	/// </summary>
	public static bool IsGreaterThan(double a, double b, double epsilon = DEFAULT_DOUBLE_EPSILON) =>
		a > b && !AreEqual(a, b, epsilon);

	/// <summary>
	///     Determines if the first float is greater than or equal to the second.
	/// </summary>
	public static bool IsGreaterThanOrEqual(float a, float b, float epsilon = DEFAULT_FLOAT_EPSILON) =>
		a > b || AreEqual(a, b, epsilon);

	/// <summary>
	///     Determines if the first double is greater than or equal to the second.
	/// </summary>
	public static bool IsGreaterThanOrEqual(double a, double b, double epsilon = DEFAULT_DOUBLE_EPSILON) =>
		a > b || AreEqual(a, b, epsilon);

	/// <summary>
	///     Determines if the first float is less than the second.
	/// </summary>
	public static bool IsLessThan(float a, float b, float epsilon = DEFAULT_FLOAT_EPSILON) =>
		a < b && !AreEqual(a, b, epsilon);

	/// <summary>
	///     Determines if the first double is less than the second.
	/// </summary>
	public static bool IsLessThan(double a, double b, double epsilon = DEFAULT_DOUBLE_EPSILON) =>
		a < b && !AreEqual(a, b, epsilon);

	/// <summary>
	///     Determines if the first float is less than or equal to the second.
	/// </summary>
	public static bool IsLessThanOrEqual(float a, float b, float epsilon = DEFAULT_FLOAT_EPSILON) =>
		a < b || AreEqual(a, b, epsilon);

	/// <summary>
	///     Determines if the first double is less than or equal to the second.
	/// </summary>
	public static bool IsLessThanOrEqual(double a, double b, double epsilon = DEFAULT_DOUBLE_EPSILON) =>
		a < b || AreEqual(a, b, epsilon);

	/// <summary>
	///     Determines if a float value is effectively zero.
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static bool IsZero(float value, float epsilon = DEFAULT_FLOAT_EPSILON) =>
		MathF.Abs(value) <= epsilon;

	/// <summary>
	///     Determines if a double value is effectively zero.
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static bool IsZero(double value, double epsilon = DEFAULT_DOUBLE_EPSILON) =>
		Math.Abs(value) <= epsilon;

	/// <summary>
	///     Clamps a double value to be within the specified range.
	/// </summary>
	public static double Clamp(double value, double min, double max)
	{
		if (value < min) return min;

		return value > max ? max : value;
	}

	/// <summary>
	///     Clamps a float value to be within the specified range.
	/// </summary>
	public static float Clamp(float value, float min, float max)
	{
		if (value < min) return min;

		return value > max ? max : value;
	}

	/// <summary>
	///     Returns the sign of a float value, treating values close to zero as zero.
	/// </summary>
	public static int Sign(float value, float epsilon = DEFAULT_FLOAT_EPSILON) =>
		IsZero(value, epsilon) ? 0 : Math.Sign(value);

	/// <summary>
	///     Returns the sign of a double value, treating values close to zero as zero.
	/// </summary>
	public static int Sign(double value, double epsilon = DEFAULT_DOUBLE_EPSILON) =>
		IsZero(value, epsilon) ? 0 : Math.Sign(value);
}
