using System;
using Bezoro.Core.Helpers;
using FluentAssertions;
using JetBrains.Annotations;
using Xunit;

namespace Bezoro.Core.Tests.Helpers;

[TestSubject(typeof(FloatComparer))]
public class FloatComparerTests
{
	[Fact]
	public void AreEqual_WhenFloat_ShouldBeFalse_WhenDiffGreaterThanEpsilon()
	{
		float eps = FloatComparer.DEFAULT_FLOAT_EPSILON;
		FloatComparer.AreEqual(0f, eps * 2).Should().BeFalse();
	}

	[Fact]
	public void AreEqual_WhenFloat_ShouldBeTrue_WhenDiffEqualsEpsilon_Inclusive()
	{
		float eps = FloatComparer.DEFAULT_FLOAT_EPSILON;
		FloatComparer.AreEqual(0f, eps).Should().BeTrue();
	}

	[Fact]
	public void AreEqual_WhenFloat_ShouldReturnFalse_ForDifferentInfinities()
	{
		FloatComparer.AreEqual(float.PositiveInfinity, float.NegativeInfinity).Should().BeFalse();
		FloatComparer.AreEqual(float.PositiveInfinity, 1f).Should().BeFalse();
		FloatComparer.AreEqual(1f,                     float.NegativeInfinity).Should().BeFalse();
	}

	[Fact]
	public void AreEqual_WhenFloat_ShouldReturnFalse_WhenEitherIsNaN()
	{
		FloatComparer.AreEqual(float.NaN, 1f).Should().BeFalse();
		FloatComparer.AreEqual(1f,        float.NaN).Should().BeFalse();
		FloatComparer.AreEqual(float.NaN, float.NaN).Should().BeFalse();
	}

	[Fact]
	public void AreEqual_WhenFloat_ShouldReturnTrue_ForEqualInfinities()
	{
		FloatComparer.AreEqual(float.PositiveInfinity, float.PositiveInfinity).Should().BeTrue();
		FloatComparer.AreEqual(float.NegativeInfinity, float.NegativeInfinity).Should().BeTrue();
	}

	[Fact]
	public void AreEqual_WhenFloat_ShouldReturnTrue_ForExactEquality()
	{
		FloatComparer.AreEqual(1.23f, 1.23f).Should().BeTrue();
	}

	[Fact]
	public void AreEqual_WhenFloat_ShouldThrow_WhenEpsilonIsNegative()
	{
		Action act = () => FloatComparer.AreEqual(1f, 2f, -0.1f);
		act.Should().Throw<ArgumentOutOfRangeException>();
	}

	[Fact]
	public void AreEqualRelative_WhenFloat_ShouldReturnFalse_WhenBeyondRelativeTolerance()
	{
		var   a    = 1000f;
		float rEps = FloatComparer.DEFAULT_FLOAT_RELATIVE_EPSILON;
		float b    = a + rEps * a * 2;

		FloatComparer.AreEqualRelative(a, b).Should().BeFalse();
	}

	[Fact]
	public void AreEqualRelative_WhenFloat_ShouldReturnTrue_AtRelativeBoundary()
	{
		// Use 1.0f to reduce rounding error at the boundary of relative epsilon
		var   a    = 10_000_000f;
		float rEps = FloatComparer.DEFAULT_FLOAT_RELATIVE_EPSILON;
		float b    = a + rEps * a;

		FloatComparer.AreEqualRelative(a, b).Should().BeTrue();
	}

	[Fact]
	public void AreEqualRelative_WhenFloat_ShouldReturnTrue_ForZeros()
	{
		FloatComparer.AreEqualRelative(0f, 0f).Should().BeTrue();
	}

	[Fact]
	public void AreEqualRelative_WhenFloat_ShouldThrow_WhenEpsilonIsNegative()
	{
		Action act = () => FloatComparer.AreEqualRelative(1f, 2f, -0.1f);
		act.Should().Throw<ArgumentOutOfRangeException>();
	}

	[Fact]
	public void AreEqualRelative_WhenFloat_ShouldUseFallback_ForNearZeroValues()
	{
		// Near-zero values should use absolute epsilon fallback
		FloatComparer.AreEqualRelative(0f, 1e-7f).Should().BeTrue();  // Within absolute epsilon
		FloatComparer.AreEqualRelative(0f, 1e-5f).Should().BeFalse(); // Beyond absolute epsilon
	}

	[Fact]
	public void AreEqualRobust_WhenFloat_ShouldHandleInfinity_Correctly()
	{
		FloatComparer.AreEqualRobust(float.PositiveInfinity, float.PositiveInfinity).Should().BeTrue();
		FloatComparer.AreEqualRobust(float.NegativeInfinity, float.NegativeInfinity).Should().BeTrue();
		FloatComparer.AreEqualRobust(float.PositiveInfinity, 1f).Should().BeFalse();
		FloatComparer.AreEqualRobust(1f,                     float.NegativeInfinity).Should().BeFalse();
	}

	[Fact]
	public void AreEqualRobust_WhenFloat_ShouldHandleNaN_AsFalse()
	{
		FloatComparer.AreEqualRobust(float.NaN, 1f).Should().BeFalse();
		FloatComparer.AreEqualRobust(float.NaN, float.NaN).Should().BeFalse();
	}

	[Fact]
	public void AreEqualRobust_WhenFloat_ShouldThrow_WhenEpsilonIsNegative()
	{
		Action act1 = () => FloatComparer.AreEqualRobust(1f, 2f, -0.1f, 0.1f);
		Action act2 = () => FloatComparer.AreEqualRobust(1f, 2f, 0.1f,  -0.1f);
		act1.Should().Throw<ArgumentOutOfRangeException>();
		act2.Should().Throw<ArgumentOutOfRangeException>();
	}

	[Fact]
	public void AreEqualRobust_WhenFloat_ShouldUseAbsoluteEpsilon_NearZero_BoundaryInclusive()
	{
		float eps = FloatComparer.DEFAULT_FLOAT_EPSILON;

		FloatComparer.AreEqualRobust(0f, eps).Should().BeTrue();
		FloatComparer.AreEqualRobust(0f, eps * 2).Should().BeFalse();
	}

	[Fact]
	public void AreEqualRobust_WhenFloat_ShouldUseRelativeEpsilon_ForLargerNumbers_BoundaryInclusive()
	{
		var   a     = 12345f;
		float rEps  = FloatComparer.DEFAULT_FLOAT_RELATIVE_EPSILON;
		float delta = rEps * Math.Abs(a);

		FloatComparer.AreEqualRobust(a, a + delta).Should().BeTrue();
		FloatComparer.AreEqualRobust(a, a + delta * 2).Should().BeFalse();
	}

	[Fact]
	public void IsGreaterThan_WhenFloat_ShouldBeFalse_WhenWithinEpsilon()
	{
		float eps = FloatComparer.DEFAULT_FLOAT_EPSILON;
		FloatComparer.IsGreaterThan(1f + eps / 2, 1f).Should().BeFalse();
	}

	[Fact]
	public void IsGreaterThan_WhenFloat_ShouldBeTrue_WhenDifferenceExceedsEpsilon()
	{
		float eps = FloatComparer.DEFAULT_FLOAT_EPSILON;
		FloatComparer.IsGreaterThan(1f + eps * 100, 1f).Should().BeTrue();
	}

	[Fact]
	public void IsGreaterThanOrEqual_WhenFloat_ShouldBeFalse_WhenLessBeyondEpsilon()
	{
		float eps = FloatComparer.DEFAULT_FLOAT_EPSILON;
		FloatComparer.IsGreaterThanOrEqual(1f, 1f + eps * 100).Should().BeFalse();
	}

	[Fact]
	public void IsGreaterThanOrEqual_WhenFloat_ShouldBeTrue_WhenEqualWithinEpsilon()
	{
		float eps = FloatComparer.DEFAULT_FLOAT_EPSILON;
		FloatComparer.IsGreaterThanOrEqual(1f, 1f + eps / 2).Should().BeTrue();
	}

	[Fact]
	public void IsLessThan_WhenFloat_ShouldBeFalse_WhenWithinEpsilon()
	{
		float eps = FloatComparer.DEFAULT_FLOAT_EPSILON;
		FloatComparer.IsLessThan(1f, 1f + eps / 2).Should().BeFalse();
	}

	[Fact]
	public void IsLessThan_WhenFloat_ShouldBeTrue_WhenDifferenceExceedsEpsilon()
	{
		float eps = FloatComparer.DEFAULT_FLOAT_EPSILON;
		FloatComparer.IsLessThan(1f, 1f + eps * 100).Should().BeTrue();
	}

	[Fact]
	public void IsLessThanOrEqual_WhenFloat_ShouldBeFalse_WhenGreaterBeyondEpsilon()
	{
		float eps = FloatComparer.DEFAULT_FLOAT_EPSILON;
		FloatComparer.IsLessThanOrEqual(1f + eps * 100, 1f).Should().BeFalse();
	}

	[Fact]
	public void IsLessThanOrEqual_WhenFloat_ShouldBeTrue_WhenEqualWithinEpsilon()
	{
		float eps = FloatComparer.DEFAULT_FLOAT_EPSILON;
		FloatComparer.IsLessThanOrEqual(1f + eps / 2, 1f).Should().BeTrue();
	}

	[Fact]
	public void IsZero_WhenFloat_ShouldBeFalse_OutsideBoundary()
	{
		float eps = FloatComparer.DEFAULT_FLOAT_EPSILON;
		FloatComparer.IsZero(eps * 2).Should().BeFalse();
	}

	[Fact]
	public void IsZero_WhenFloat_ShouldBeTrue_AtBoundaryAndZero()
	{
		float eps = FloatComparer.DEFAULT_FLOAT_EPSILON;
		FloatComparer.IsZero(0f).Should().BeTrue();
		FloatComparer.IsZero(eps).Should().BeTrue();
	}

	[Fact]
	public void Sign_WhenFloat_ShouldReturn0NearZero_ElseMathSign()
	{
		float eps = FloatComparer.DEFAULT_FLOAT_EPSILON;
		FloatComparer.Sign(0f).Should().Be(0);
		FloatComparer.Sign(eps).Should().Be(0);
		FloatComparer.Sign(eps * 2).Should().Be(1);
		FloatComparer.Sign(-eps * 2).Should().Be(-1);
	}
}

