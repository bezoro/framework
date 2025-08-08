using System;

namespace Bezoro.Core.Common.Extensions;

public static class FloatExtensions
{
	public static float ThrowIfLessOrEqualThan(this float f, float min)
	{
		if (f <= min)
			throw new ValueTooSmallException(f, min);

		return f;
	}

	public static float ThrowIfLessThan(this float f, float min)
	{
		if (f < min)
			throw new ValueTooSmallException(f, min);

		return f;
	}

	public static float ThrowIfOverOrEqualThan(this float f, float max)
	{
		if (f >= max)
			throw new ValueTooLargeException(f, max);

		return f;
	}

	public static float ThrowIfOverThan(this float f, float max)
	{
		if (f > max)
			throw new ValueTooLargeException(f, max);

		return f;
	}
}

/// <summary>
///     Thrown when a value exceeds the configured maximum.
/// </summary>
public class ValueTooLargeException(float value, float max)
	: Exception($"Value '{value}' is greater than the maximum allowed value '{max}'.");

/// <summary>
///     Thrown when a value is smaller than the configured minimum.
/// </summary>
public class ValueTooSmallException(float value, float min)
	: Exception($"Value '{value}' is smaller than the minimum allowed value '{min}'.");
