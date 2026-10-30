namespace Bezoro.Core.Numerics;

/// <summary>Pure scalar operations with finite inputs and explicit interpolation and time policies.</summary>
/// <remarks>Floating-point rounding and transcendental results can differ between runtimes and processors.</remarks>
public static class ScalarMath
{
	private const double HalfConvergenceExponent = 0.6931471805599453;
	private const double MinimumNormalDouble = 2.2250738585072014E-308;

	/// <summary>Wraps a finite value into [min, max), preserving values already inside the interval.</summary>
	/// <remarks>The interval must be increasing with a finite representable width. A rounded upper endpoint maps to min.</remarks>
	/// <exception cref="ArgumentOutOfRangeException">An input is not finite, or the interval has no positive finite width.</exception>
	public static double Wrap(double value, double min, double max)
	{
		ValidateFinite(value, nameof(value));
		ValidateFinite(min, nameof(min));
		ValidateFinite(max, nameof(max));
		double width = max - min;
		if (!(width > 0) || double.IsInfinity(width))
			throw new ArgumentOutOfRangeException(nameof(max), "The interval must have a positive finite width.");
		if (value >= min && value < max) return value;
		if (value == max) return min;

		// Reducing both operands first avoids overflowing value - min.
		double position = PositiveRemainder(value, width);
		double start = PositiveRemainder(min, width);
		double offset = position - start;
		if (offset < 0) offset += width;
		double result = min + offset;
		return result >= min && result < max ? result : min;
	}

	/// <summary>Wraps a finite value into [min, max), using double intermediates.</summary>
	/// <remarks>The interval must be increasing. A rounded upper endpoint maps to min.</remarks>
	/// <exception cref="ArgumentOutOfRangeException">An input is not finite, or min is not less than max.</exception>
	public static float Wrap(float value, float min, float max)
	{
		float result = (float)Wrap((double)value, min, max);
		return result < max ? result : min;
	}

	/// <summary>Moves toward target by at most maxDelta without passing it.</summary>
	/// <exception cref="ArgumentOutOfRangeException">An input is not finite, or maxDelta is negative.</exception>
	public static double MoveTowards(double current, double target, double maxDelta)
	{
		ValidateFinite(current, nameof(current));
		ValidateFinite(target, nameof(target));
		ValidateNonnegative(maxDelta, nameof(maxDelta));
		if (current == target || maxDelta == 0) return current;
		if (Math.Abs(target - current) <= maxDelta) return target;
		return current < target ? current + maxDelta : current - maxDelta;
	}

	/// <summary>Moves toward target by at most maxDelta, using double intermediates and never passing target.</summary>
	/// <exception cref="ArgumentOutOfRangeException">An input is not finite, or maxDelta is negative.</exception>
	public static float MoveTowards(float current, float target, float maxDelta) =>
		(float)MoveTowards((double)current, target, maxDelta);

	/// <summary>Interpolates between finite endpoints, clamping amount to [0, 1].</summary>
	/// <remarks>Exact endpoint amounts return the original endpoints. Opposite-sign endpoints do not overflow their difference.</remarks>
	/// <exception cref="ArgumentOutOfRangeException">An input is not finite.</exception>
	public static double LerpClamped(double from, double to, double amount)
	{
		ValidateFinite(from, nameof(from));
		ValidateFinite(to, nameof(to));
		ValidateFinite(amount, nameof(amount));
		return LerpFinite(from, to, amount);
	}

	/// <summary>Interpolates between finite endpoints with double intermediates, clamping amount to [0, 1].</summary>
	/// <exception cref="ArgumentOutOfRangeException">An input is not finite.</exception>
	public static float LerpClamped(float from, float to, float amount) =>
		(float)LerpClamped((double)from, to, amount);

	/// <summary>Returns a value's fractional position between the endpoints, clamped to [0, 1]. Reversed intervals are supported.</summary>
	/// <exception cref="ArgumentOutOfRangeException">An input is not finite.</exception>
	/// <exception cref="ArgumentException">The endpoints are equal.</exception>
	public static double InverseLerpClamped(double value, double from, double to)
	{
		ValidateFinite(value, nameof(value));
		ValidateFinite(from, nameof(from));
		ValidateFinite(to, nameof(to));
		if (from == to) throw new ArgumentException("The source interval must have distinct endpoints.", nameof(to));
		if (from < to)
		{
			if (value <= from) return 0;
			if (value >= to) return 1;
		}
		else
		{
			if (value >= from) return 0;
			if (value <= to) return 1;
		}

		double width = to - from;
		double amount = double.IsInfinity(width)
			? (value * 0.5 - from * 0.5) / (to * 0.5 - from * 0.5)
			: (value - from) / width;
		return amount <= 0 ? 0 : amount >= 1 ? 1 : amount;
	}

	/// <summary>Returns a value's fractional position with double intermediates, clamped to [0, 1].</summary>
	/// <exception cref="ArgumentOutOfRangeException">An input is not finite.</exception>
	/// <exception cref="ArgumentException">The endpoints are equal.</exception>
	public static float InverseLerpClamped(float value, float from, float to) =>
		(float)InverseLerpClamped((double)value, from, to);

	/// <summary>Maps a finite value between intervals, clamping its source position. Either interval may be reversed.</summary>
	/// <exception cref="ArgumentOutOfRangeException">An input is not finite.</exception>
	/// <exception cref="ArgumentException">The source endpoints are equal.</exception>
	public static double RemapClamped(double value, double fromMin, double fromMax, double toMin, double toMax)
	{
		double amount = InverseLerpClamped(value, fromMin, fromMax);
		return LerpClamped(toMin, toMax, amount);
	}

	/// <summary>Maps a finite value between intervals with double intermediates, clamping its source position.</summary>
	/// <exception cref="ArgumentOutOfRangeException">An input is not finite.</exception>
	/// <exception cref="ArgumentException">The source endpoints are equal.</exception>
	public static float RemapClamped(float value, float fromMin, float fromMax, float toMin, float toMax) =>
		(float)RemapClamped((double)value, fromMin, fromMax, toMin, toMax);

	/// <summary>Approaches target exponentially at a nonnegative convergence rate per unit of elapsed time.</summary>
	/// <remarks>
	/// Computes interpolation by 1 - exp(-rate * elapsedTime). A rate or elapsed time of zero preserves current.
	/// For a fixed target and rate, splitting elapsed time gives the same mathematical trajectory within rounding error.
	/// An overflowing rate/time product reaches target. Evaluation preserves small initial changes and late-stage decay
	/// with a series, remaining-weight interpolation, and rescaling when a product or exponential underflows.
	/// </remarks>
	/// <exception cref="ArgumentOutOfRangeException">An input is not finite, or rate or elapsedTime is negative.</exception>
	public static double Damp(double current, double target, double rate, double elapsedTime)
	{
		ValidateFinite(current, nameof(current));
		ValidateFinite(target, nameof(target));
		ValidateNonnegative(rate, nameof(rate));
		ValidateNonnegative(elapsedTime, nameof(elapsedTime));
		if (current == target || rate == 0 || elapsedTime == 0) return current;
		double exponent = rate * elapsedTime;
		if (exponent < MinimumNormalDouble)
		{
			// Scale before multiplying the factors together so subnormal exponent
			// quantization cannot erase a representable endpoint contribution.
			double larger = Math.Max(rate, elapsedTime);
			double smaller = Math.Min(rate, elapsedTime);
			double correction = ScaleProduct(target, larger, smaller) - ScaleProduct(current, larger, smaller);
			double result = current + correction;
			return Math.Min(Math.Max(result, Math.Min(current, target)), Math.Max(current, target));
		}
		// Beyond half convergence, interpolate from the target using the remaining weight.
		// Subtracting that weight from one would erase representable late-stage changes.
		if (exponent > HalfConvergenceExponent)
		{
			double decay = Math.Exp(-exponent);
			if (decay < MinimumNormalDouble && !double.IsInfinity(exponent))
			{
				double halfDecay = Math.Exp(-exponent * 0.5);
				return LerpFinite(target, LerpFinite(target, current, halfDecay), halfDecay);
			}
			return LerpFinite(target, current, decay);
		}
		double amount = exponent < 0.00001
			? exponent * (1 + exponent * (-0.5 + exponent * (1.0 / 6 + exponent * (-1.0 / 24 + exponent / 120))))
			: 1 - Math.Exp(-exponent);
		return LerpFinite(current, target, amount);
	}

	/// <summary>Approaches target exponentially with double intermediates and a rate per unit of elapsed time.</summary>
	/// <exception cref="ArgumentOutOfRangeException">An input is not finite, or rate or elapsedTime is negative.</exception>
	public static float Damp(float current, float target, float rate, float elapsedTime) =>
		(float)Damp((double)current, target, rate, elapsedTime);

	private static double ScaleProduct(double value, double larger, double smaller) =>
		larger > 1 && Math.Abs(value) >= double.MaxValue / larger
			? value * smaller * larger
			: value * larger * smaller;

	private static double PositiveRemainder(double value, double modulus)
	{
		double remainder = value % modulus;
		if (remainder < 0) remainder += modulus;
		return remainder < modulus ? remainder : 0;
	}

	private static double LerpFinite(double from, double to, double amount)
	{
		if (amount <= 0) return from;
		if (amount >= 1) return to;
		// Same-sign subtraction stays finite; opposite-sign weighted terms stay bounded.
		double result = (from < 0) != (to < 0)
			? (1 - amount) * from + amount * to
			: from + (to - from) * amount;
		double min = Math.Min(from, to);
		double max = Math.Max(from, to);
		return result < min ? min : result > max ? max : result;
	}

	private static void ValidateFinite(double value, string parameterName)
	{
		if (double.IsNaN(value) || double.IsInfinity(value))
			throw new ArgumentOutOfRangeException(parameterName, "The value must be finite.");
	}

	private static void ValidateNonnegative(double value, string parameterName)
	{
		ValidateFinite(value, parameterName);
		if (value < 0) throw new ArgumentOutOfRangeException(parameterName, "The value must not be negative.");
	}
}
