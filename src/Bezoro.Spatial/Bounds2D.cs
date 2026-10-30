using System.Numerics;

namespace Bezoro.Spatial;

/// <summary>A finite, closed axis-aligned rectangle. Zero width or height is permitted.</summary>
/// <remarks>The default value is the single point at the origin. Boundary points are included.</remarks>
public readonly struct Bounds2D : IEquatable<Bounds2D>
{
    /// <summary>Creates a rectangle with inclusive minimum and maximum coordinates.</summary>
    /// <exception cref="ArgumentOutOfRangeException">A coordinate is not finite, or a minimum exceeds its maximum.</exception>
    public Bounds2D(Vector2 minimum, Vector2 maximum)
    {
        ValidatePoint(minimum, nameof(minimum));
        ValidatePoint(maximum, nameof(maximum));
        if (minimum.X > maximum.X || minimum.Y > maximum.Y)
            throw new ArgumentOutOfRangeException(nameof(maximum), "Maximum coordinates must not precede minimum coordinates.");
        Minimum = minimum;
        Maximum = maximum;
    }

    /// <summary>Gets the inclusive minimum coordinates.</summary>
    public Vector2 Minimum { get; }
    /// <summary>Gets the inclusive maximum coordinates.</summary>
    public Vector2 Maximum { get; }
    /// <summary>Gets the width using binary64 subtraction to avoid overflowing finite binary32 endpoints.</summary>
    public double Width => (double)Maximum.X - Minimum.X;
    /// <summary>Gets the height using binary64 subtraction to avoid overflowing finite binary32 endpoints.</summary>
    public double Height => (double)Maximum.Y - Minimum.Y;

    /// <summary>Tests inclusive containment. Non-finite points are never contained.</summary>
    public bool Contains(Vector2 point) =>
        point.X >= Minimum.X && point.X <= Maximum.X && point.Y >= Minimum.Y && point.Y <= Maximum.Y;

    /// <summary>Tests overlap, including shared edges and single boundary points.</summary>
    public bool Intersects(Bounds2D other) =>
        Maximum.X >= other.Minimum.X && Minimum.X <= other.Maximum.X &&
        Maximum.Y >= other.Minimum.Y && Minimum.Y <= other.Maximum.Y;

    /// <inheritdoc />
    public bool Equals(Bounds2D other) => Minimum.Equals(other.Minimum) && Maximum.Equals(other.Maximum);
    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is Bounds2D other && Equals(other);
    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(Minimum, Maximum);
    /// <summary>Compares the two endpoint pairs.</summary>
    public static bool operator ==(Bounds2D left, Bounds2D right) => left.Equals(right);
    /// <summary>Compares the two endpoint pairs.</summary>
    public static bool operator !=(Bounds2D left, Bounds2D right) => !left.Equals(right);

    internal static void ValidatePoint(Vector2 point, string parameterName)
    {
        if (float.IsNaN(point.X) || float.IsInfinity(point.X) || float.IsNaN(point.Y) || float.IsInfinity(point.Y))
            throw new ArgumentOutOfRangeException(parameterName, "Coordinates must be finite.");
    }
}
