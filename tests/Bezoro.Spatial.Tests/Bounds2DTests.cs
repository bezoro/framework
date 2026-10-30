using System.Numerics;
using Xunit;

namespace Bezoro.Spatial.Tests;

public sealed class Bounds2DTests
{
    [Fact]
    public void Default_IsOneInclusivePoint()
    {
        var bounds = default(Bounds2D);
        Assert.True(bounds.Contains(Vector2.Zero));
        Assert.False(bounds.Contains(new Vector2(float.Epsilon, 0)));
        Assert.Equal(0, bounds.Width);
        Assert.Equal(0, bounds.Height);
    }

    [Fact]
    public void ContainsAndIntersects_IncludeEdgesAndCorners()
    {
        var bounds = new Bounds2D(new Vector2(-1, -2), new Vector2(3, 4));
        Assert.True(bounds.Contains(bounds.Minimum));
        Assert.True(bounds.Contains(bounds.Maximum));
        Assert.True(bounds.Contains(new Vector2(-1, 4)));
        Assert.True(bounds.Intersects(new Bounds2D(new Vector2(3, 4), new Vector2(5, 5))));
        Assert.False(bounds.Intersects(new Bounds2D(new Vector2(3.0001f, 4), new Vector2(5, 5))));
        Assert.False(bounds.Contains(new Vector2(float.NaN, 0)));
        Assert.False(bounds.Contains(new Vector2(0, float.PositiveInfinity)));
    }

    [Fact]
    public void WidthAndHeight_DoNotOverflowFiniteFloatEndpoints()
    {
        var bounds = new Bounds2D(new Vector2(-float.MaxValue), new Vector2(float.MaxValue));
        Assert.Equal(2 * (double)float.MaxValue, bounds.Width);
        Assert.Equal(bounds.Width, bounds.Height);
        Assert.True(double.IsFinite(bounds.Width));
    }

    [Theory]
    [InlineData(float.NaN, 0, 1, 1)]
    [InlineData(0, float.PositiveInfinity, 1, 1)]
    [InlineData(0, 0, float.NegativeInfinity, 1)]
    [InlineData(0, 0, 1, float.NaN)]
    [InlineData(2, 0, 1, 1)]
    [InlineData(0, 2, 1, 1)]
    public void Constructor_RejectsInvalidCoordinates(float x0, float y0, float x1, float y1) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => new Bounds2D(new Vector2(x0, y0), new Vector2(x1, y1)));

    [Fact]
    public void Equality_UsesEndpoints()
    {
        var left = new Bounds2D(new Vector2(-2), new Vector2(4));
        var equal = new Bounds2D(new Vector2(-2), new Vector2(4));
        Assert.True(left == equal);
        Assert.False(left != equal);
        Assert.Equal(left.GetHashCode(), equal.GetHashCode());
        Assert.True(left.Equals((object)equal));
        Assert.False(left.Equals(null));
        Assert.False(left == default);
    }
}
