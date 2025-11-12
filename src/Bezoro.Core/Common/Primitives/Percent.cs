using System;
using System.Diagnostics;

namespace Bezoro.Core.Common.Primitives;

/// <summary>
///     Represents a percentage value constrained between 0 and 100 (inclusive).
/// </summary>
[DebuggerDisplay("{Value}%")]
public readonly struct Percent : IEquatable<Percent>, IComparable<Percent>
{
    /// <summary>
    ///     Initializes a new instance of <see cref="Percent"/> with the specified value.
    /// </summary>
    /// <param name="value">The percentage value (0–100).</param>
    /// <exception cref="ArgumentOutOfRangeException">
    ///     Thrown when <paramref name="value"/> is greater than 100.
    /// </exception>
    public Percent(byte value)
    {
        if (value > 100)
            throw new ArgumentOutOfRangeException(nameof(value), "Percentage must be between 0 and 100.");

        Value = value;
    }

    /// <summary>
    ///     Gets the percentage value (0–100).
    /// </summary>
    public byte Value { get; }

    /// <summary>
    ///     Represents 100%.
    /// </summary>
    public static Percent Full => new(100);

    /// <summary>
    ///     Represents 50%.
    /// </summary>
    public static Percent Half => new(50);

    /// <summary>
    ///     Represents 90%.
    /// </summary>
    public static Percent Ninety => new(90);

    /// <summary>
    ///     Represents 25%.
    /// </summary>
    public static Percent Quarter => new(25);

    /// <summary>
    ///     Represents 75%.
    /// </summary>
    public static Percent ThreeQuarters => new(75);

    /// <summary>
    ///     Represents 0%.
    /// </summary>
    public static Percent Zero => new(0);

    /// <summary>
    ///     Indicates whether two <see cref="Percent"/> values are equal.
    /// </summary>
    /// <param name="left">The first percent.</param>
    /// <param name="right">The second percent.</param>
    /// <returns><c>true</c> if the values are equal; otherwise <c>false</c>.</returns>
    public static bool operator ==(Percent left, Percent right) => left.Equals(right);

    /// <summary>
    ///     Explicitly casts a <see cref="byte"/> value to a <see cref="Percent"/>.
    /// </summary>
    /// <param name="value">The byte value to convert (0–100).</param>
    /// <returns>A <see cref="Percent"/> instance representing the given value.</returns>
    public static explicit operator Percent(byte value) => new(value);

    /// <summary>
    ///     Determines whether one <see cref="Percent"/> is greater than another.
    /// </summary>
    /// <param name="left">The first percent.</param>
    /// <param name="right">The second percent.</param>
    /// <returns><c>true</c> if <paramref name="left"/> is greater than <paramref name="right"/>; otherwise, <c>false</c>.</returns>
    public static bool operator >(Percent left, Percent right) => left.Value > right.Value;

    /// <summary>
    ///     Determines whether one <see cref="Percent"/> is greater than or equal to another.
    /// </summary>
    /// <param name="left">The first percent.</param>
    /// <param name="right">The second percent.</param>
    /// <returns><c>true</c> if <paramref name="left"/> is greater than or equal to <paramref name="right"/>; otherwise, <c>false</c>.</returns>
    public static bool operator >=(Percent left, Percent right) => left.Value >= right.Value;

    /// <summary>
    ///     Implicitly converts a <see cref="Percent"/> to a <see cref="byte"/> representing its value.
    /// </summary>
    /// <param name="percent">The <see cref="Percent"/> instance.</param>
    /// <returns>The <see cref="byte"/> percent value (0–100).</returns>
    public static implicit operator byte(Percent percent) => percent.Value;

    /// <summary>
    ///     Indicates whether two <see cref="Percent"/> values are not equal.
    /// </summary>
    /// <param name="left">The first percent.</param>
    /// <param name="right">The second percent.</param>
    /// <returns><c>true</c> if the values are not equal; otherwise <c>false</c>.</returns>
    public static bool operator !=(Percent left, Percent right) => !left.Equals(right);

    /// <summary>
    ///     Determines whether one <see cref="Percent"/> is less than another.
    /// </summary>
    /// <param name="left">The first percent.</param>
    /// <param name="right">The second percent.</param>
    /// <returns><c>true</c> if <paramref name="left"/> is less than <paramref name="right"/>; otherwise, <c>false</c>.</returns>
    public static bool operator <(Percent left, Percent right) => left.Value < right.Value;

    /// <summary>
    ///     Determines whether one <see cref="Percent"/> is less than or equal to another.
    /// </summary>
    /// <param name="left">The first percent.</param>
    /// <param name="right">The second percent.</param>
    /// <returns><c>true</c> if <paramref name="left"/> is less than or equal to <paramref name="right"/>; otherwise, <c>false</c>.</returns>
    public static bool operator <=(Percent left, Percent right) => left.Value <= right.Value;

    #region Equality

    /// <summary>
    ///     Compares the current <see cref="Percent"/> to another <see cref="Percent"/>.
    /// </summary>
    /// <param name="other">A <see cref="Percent"/> to compare with this instance.</param>
    /// <returns>
    ///     A value that indicates the relative order of the objects being compared.
    ///     Less than zero: This instance is less than <paramref name="other"/>.
    ///     Zero: This instance is equal to <paramref name="other"/>.
    ///     Greater than zero: This instance is greater than <paramref name="other"/>.
    /// </returns>
    public int CompareTo(Percent other) => Value.CompareTo(other.Value);

    /// <summary>
    ///     Indicates whether the current <see cref="Percent"/> is equal to another <see cref="Percent"/>.
    /// </summary>
    /// <param name="other">A <see cref="Percent"/> to compare with this instance.</param>
    /// <returns><c>true</c> if equal; otherwise, <c>false</c>.</returns>
    public bool Equals(Percent other) => Value == other.Value;

    /// <summary>
    ///     Determines whether the specified object is equal to the current <see cref="Percent"/>.
    /// </summary>
    /// <param name="obj">The object to compare to.</param>
    /// <returns><c>true</c> if <paramref name="obj"/> is a <see cref="Percent"/> with the same value; otherwise, <c>false</c>.</returns>
    public override bool Equals(object? obj) => obj is Percent other && Equals(other);

    /// <summary>
    ///     Returns the hash code for this instance.
    /// </summary>
    /// <returns>A 32-bit signed integer hash code.</returns>
    public override int GetHashCode() => Value.GetHashCode();

    #endregion

    /// <summary>
    ///     Returns a string representation of this <see cref="Percent"/> in "%" notation (e.g., "42%").
    /// </summary>
    /// <returns>The percent value as a string with a trailing percent sign.</returns>
    public override string ToString() => $"{Value}%";

    /// <summary>
    ///     Converts the percentage to a decimal ratio (0.0–1.0).
    /// </summary>
    /// <returns>The percent value as a float in the range 0.0 to 1.0.</returns>
    public float ToRatio() => Value / 100f;
}
