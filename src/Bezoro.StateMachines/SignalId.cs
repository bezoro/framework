using System.Globalization;

namespace Bezoro.StateMachines;

/// <summary>Identifies a zero-based input signal within a particular machine definition.</summary>
/// <remarks>The default value identifies signal zero. Identifiers do not establish membership in a definition.</remarks>
public readonly struct SignalId : IEquatable<SignalId>
{
    /// <summary>Creates a signal identifier from a nonnegative integer.</summary>
    public SignalId(int value)
    {
        if (value < 0) throw new ArgumentOutOfRangeException(nameof(value));
        Value = value;
    }

    /// <summary>Gets the zero-based integer.</summary>
    public int Value { get; }

    /// <inheritdoc />
    public bool Equals(SignalId other) => Value == other.Value;
    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is SignalId other && Equals(other);
    /// <inheritdoc />
    public override int GetHashCode() => Value;
    /// <inheritdoc />
    public override string ToString() => Value.ToString(CultureInfo.InvariantCulture);
    /// <summary>Compares identifiers.</summary>
    public static bool operator ==(SignalId left, SignalId right) => left.Equals(right);
    /// <summary>Compares identifiers.</summary>
    public static bool operator !=(SignalId left, SignalId right) => !left.Equals(right);
}
