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
	///     Initializes a new instance of <see cref="Percent" /> with the specified value.
	/// </summary>
	/// <param name="value">The percentage value (0–100).</param>
	/// <exception cref="ArgumentOutOfRangeException">Thrown when value is greater than 100.</exception>
	public Percent(byte value)
	{
		if (value > 100)
			throw new ArgumentOutOfRangeException(nameof(value), "Percentage must be between 0 and 100.");

		Value = value;
	}

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
	///     Gets the percentage value (0–100).
	/// </summary>
	public byte Value { get; }

	public static                   bool operator ==(Percent left, Percent right) => left.Equals(right);
	public static explicit operator Percent(byte             value)               => new(value);
	public static                   bool operator >(Percent  left, Percent right) => left.Value > right.Value;
	public static                   bool operator >=(Percent left, Percent right) => left.Value >= right.Value;
	public static implicit operator byte(Percent             percent)             => percent.Value;
	public static                   bool operator !=(Percent left, Percent right) => !left.Equals(right);
	public static                   bool operator <(Percent  left, Percent right) => left.Value < right.Value;
	public static                   bool operator <=(Percent left, Percent right) => left.Value <= right.Value;

	#region Equality

	public int CompareTo(Percent other) => Value.CompareTo(other.Value);

	public          bool Equals(Percent other) => Value == other.Value;
	public override bool Equals(object? obj)   => obj is Percent other && Equals(other);
	public override int  GetHashCode()         => Value.GetHashCode();

	#endregion

	public override string ToString() => $"{Value}%";

	/// <summary>
	///     Converts the percentage to a decimal ratio (0.0–1.0).
	/// </summary>
	public float ToRatio() => Value / 100f;
}
