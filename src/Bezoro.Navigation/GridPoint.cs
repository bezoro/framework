namespace Bezoro.Navigation;

/// <summary>An integer coordinate in a navigation grid.</summary>
public readonly struct GridPoint : IEquatable<GridPoint>
{
    /// <summary>Creates a coordinate. Grid APIs validate whether it lies within their bounds.</summary>
    public GridPoint(int x, int y) { X = x; Y = y; }

    /// <summary>Gets the horizontal coordinate.</summary>
    public int X { get; }

    /// <summary>Gets the vertical coordinate.</summary>
    public int Y { get; }

    /// <inheritdoc />
    public bool Equals(GridPoint other) => X == other.X && Y == other.Y;

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is GridPoint other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => unchecked((X * 397) ^ Y);

    /// <inheritdoc />
    public override string ToString() => $"({X}, {Y})";

    /// <summary>Compares coordinates by value.</summary>
    public static bool operator ==(GridPoint left, GridPoint right) => left.Equals(right);

    /// <summary>Compares coordinates by value.</summary>
    public static bool operator !=(GridPoint left, GridPoint right) => !left.Equals(right);
}
