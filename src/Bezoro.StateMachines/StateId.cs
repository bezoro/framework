using System.Globalization;

namespace Bezoro.StateMachines;

/// <summary>Identifies a zero-based state within a particular machine definition.</summary>
/// <remarks>The default value identifies state zero. Identifiers do not establish membership in a definition.</remarks>
public readonly struct StateId : IEquatable<StateId>
{
    /// <summary>Creates a state identifier from a nonnegative integer.</summary>
    public StateId(int value)
    {
        if (value < 0) throw new ArgumentOutOfRangeException(nameof(value));
        Value = value;
    }

    /// <summary>Gets the zero-based integer.</summary>
    public int Value { get; }

    /// <inheritdoc />
    public bool Equals(StateId other) => Value == other.Value;
    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is StateId other && Equals(other);
    /// <inheritdoc />
    public override int GetHashCode() => Value;
    /// <inheritdoc />
    public override string ToString() => Value.ToString(CultureInfo.InvariantCulture);
    /// <summary>Compares identifiers.</summary>
    public static bool operator ==(StateId left, StateId right) => left.Equals(right);
    /// <summary>Compares identifiers.</summary>
    public static bool operator !=(StateId left, StateId right) => !left.Equals(right);
}
