namespace Bezoro.Simulation.Attributes;

/// <summary>Identifies a game-defined attribute. Zero is reserved for an uninitialized identifier.</summary>
public readonly struct StatId : IEquatable<StatId>
{
    /// <summary>Creates an identifier from a positive game-defined integer.</summary>
    public StatId(int value)
    {
        if (value <= 0) throw new ArgumentOutOfRangeException(nameof(value), "A stat identifier must be positive.");
        Value = value;
    }

    /// <summary>Gets the game-defined integer, or zero for an uninitialized identifier.</summary>
    public int Value { get; }

    /// <summary>Gets whether this identifier was initialized.</summary>
    public bool IsValid => Value > 0;

    /// <inheritdoc />
    public bool Equals(StatId other) => Value == other.Value;

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is StatId other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => Value;

    /// <inheritdoc />
    public override string ToString() => Value.ToString(System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>Compares identifiers.</summary>
    public static bool operator ==(StatId left, StatId right) => left.Equals(right);

    /// <summary>Compares identifiers.</summary>
    public static bool operator !=(StatId left, StatId right) => !left.Equals(right);
}
