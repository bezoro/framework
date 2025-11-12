using System;

namespace Bezoro.Core.Common.Extensions;

/// <summary>
/// Provides extension methods for <see cref="float"/> values to enforce minimum and maximum bounds.
/// Throws specific exceptions when constraints are violated.
/// </summary>
public static class FloatExtensions
{
    /// <summary>
    /// Ensures the floating point value is greater than the specified minimum.
    /// Throws if the value is less than or equal to <paramref name="min"/>.
    /// </summary>
    /// <param name="f">The float value to test.</param>
    /// <param name="min">The minimum exclusive lower bound.</param>
    /// <returns>The float value if it is valid.</returns>
    /// <exception cref="ValueTooSmallException">Thrown if <paramref name="f"/> is less than or equal to <paramref name="min"/>.</exception>
    public static float ThrowIfLessOrEqualThan(this float f, float min)
    {
        if (f <= min)
            throw new ValueTooSmallException(f, min);

        return f;
    }

    /// <summary>
    /// Ensures the floating point value is greater than or equal to the specified minimum.
    /// Throws if the value is less than <paramref name="min"/>.
    /// </summary>
    /// <param name="f">The float value to test.</param>
    /// <param name="min">The minimum inclusive lower bound.</param>
    /// <returns>The float value if it is valid.</returns>
    /// <exception cref="ValueTooSmallException">Thrown if <paramref name="f"/> is less than <paramref name="min"/>.</exception>
    public static float ThrowIfLessThan(this float f, float min)
    {
        if (f < min)
            throw new ValueTooSmallException(f, min);

        return f;
    }

    /// <summary>
    /// Ensures the floating point value is less than the specified maximum.
    /// Throws if the value is greater than or equal to <paramref name="max"/>.
    /// </summary>
    /// <param name="f">The float value to test.</param>
    /// <param name="max">The maximum exclusive upper bound.</param>
    /// <returns>The float value if it is valid.</returns>
    /// <exception cref="ValueTooLargeException">Thrown if <paramref name="f"/> is greater than or equal to <paramref name="max"/>.</exception>
    public static float ThrowIfOverOrEqualThan(this float f, float max)
    {
        if (f >= max)
            throw new ValueTooLargeException(f, max);

        return f;
    }

    /// <summary>
    /// Ensures the floating point value is less than or equal to the specified maximum.
    /// Throws if the value is greater than <paramref name="max"/>.
    /// </summary>
    /// <param name="f">The float value to test.</param>
    /// <param name="max">The maximum inclusive upper bound.</param>
    /// <returns>The float value if it is valid.</returns>
    /// <exception cref="ValueTooLargeException">Thrown if <paramref name="f"/> is greater than <paramref name="max"/>.</exception>
    public static float ThrowIfOverThan(this float f, float max)
    {
        if (f > max)
            throw new ValueTooLargeException(f, max);

        return f;
    }
}

/// <summary>
/// Exception thrown when a value exceeds the configured maximum.
/// </summary>
/// <remarks>
/// The exception message includes the offending value and the permitted maximum.
/// </remarks>
public class ValueTooLargeException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ValueTooLargeException"/> class.
    /// </summary>
    /// <param name="value">The value that exceeded the maximum limit.</param>
    /// <param name="max">The maximum allowed value.</param>
    public ValueTooLargeException(float value, float max)
        : base($"Value '{value}' is greater than the maximum allowed value '{max}'.")
    {
    }
}

/// <summary>
/// Exception thrown when a value is smaller than the configured minimum.
/// </summary>
/// <remarks>
/// The exception message includes the offending value and the permitted minimum.
/// </remarks>
public class ValueTooSmallException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ValueTooSmallException"/> class.
    /// </summary>
    /// <param name="value">The value that was too small.</param>
    /// <param name="min">The minimum permitted value.</param>
    public ValueTooSmallException(float value, float min)
        : base($"Value '{value}' is smaller than the minimum allowed value '{min}'.")
    {
    }
}
