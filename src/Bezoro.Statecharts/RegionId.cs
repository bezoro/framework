namespace Bezoro.Statecharts;

/// <summary>Identifies a zero-based region in a chart definition. The default identifies region zero.</summary>
public readonly struct RegionId : IEquatable<RegionId>
{
    /// <summary>Creates an identifier from a nonnegative integer.</summary>
    public RegionId(int value) { if (value < 0) throw new ArgumentOutOfRangeException(nameof(value)); Value = value; }
    /// <summary>Gets the zero-based identifier.</summary>
    public int Value { get; }
    /// <inheritdoc />
    public bool Equals(RegionId other) => Value == other.Value;
    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is RegionId other && Equals(other);
    /// <inheritdoc />
    public override int GetHashCode() => Value;
    /// <summary>Compares two identifiers.</summary>
    public static bool operator ==(RegionId left, RegionId right) => left.Equals(right);
    /// <summary>Compares two identifiers.</summary>
    public static bool operator !=(RegionId left, RegionId right) => !left.Equals(right);
}
