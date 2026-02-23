using System;
using Bezoro.Core.Helpers;
using FluentAssertions;
using JetBrains.Annotations;
using Xunit;

namespace Bezoro.Core.Tests.Helpers;

[TestSubject(typeof(DoubleComparer))]
public class DoubleComparerTests
{
	[Fact]
	public void AreEqual_WhenDouble_ShouldBeFalse_WhenDiffGreaterThanEpsilon()
	{
		double eps = DoubleComparer.DEFAULT_DOUBLE_EPSILON;
		DoubleComparer.AreEqual(0d, eps * 2).Should().BeFalse();
	}

	[Fact]
	public void AreEqual_WhenDouble_ShouldBeTrue_WhenDiffEqualsEpsilon_Inclusive()
	{
		double eps = DoubleComparer.DEFAULT_DOUBLE_EPSILON;
		DoubleComparer.AreEqual(0d, eps).Should().BeTrue();
	}

	[Fact]
	public void AreEqual_WhenDouble_ShouldReturnFalse_ForDifferentInfinities()
	{
		DoubleComparer.AreEqual(double.PositiveInfinity, double.NegativeInfinity).Should().BeFalse();
		DoubleComparer.AreEqual(double.PositiveInfinity, 1d).Should().BeFalse();
		DoubleComparer.AreEqual(1d,                      double.NegativeInfinity).Should().BeFalse();
	}

	[Fact]
	public void AreEqual_WhenDouble_ShouldReturnFalse_WhenEitherIsNaN()
	{
		DoubleComparer.AreEqual(double.NaN, 1d).Should().BeFalse();
		DoubleComparer.AreEqual(1d,         double.NaN).Should().BeFalse();
		DoubleComparer.AreEqual(double.NaN, double.NaN).Should().BeFalse();
	}

	[Fact]
	public void AreEqual_WhenDouble_ShouldReturnTrue_ForEqualInfinities()
	{
		DoubleComparer.AreEqual(double.PositiveInfinity, double.PositiveInfinity).Should().BeTrue();
		DoubleComparer.AreEqual(double.NegativeInfinity, double.NegativeInfinity).Should().BeTrue();
	}

	[Fact]
	public void AreEqual_WhenDouble_ShouldReturnTrue_ForExactEquality()
	{
		DoubleComparer.AreEqual(1.23d, 1.23d).Should().BeTrue();
	}

	[Fact]
	public void AreEqual_WhenDouble_ShouldThrow_WhenEpsilonIsNegative()
	{
		Action act = () => DoubleComparer.AreEqual(1d, 2d, -0.1d);
		act.Should().Throw<ArgumentOutOfRangeException>();
	}

	[Fact]
	public void AreEqualRelative_WhenDouble_ShouldReturnFalse_WhenBeyondRelativeTolerance()
	{
		var    a    = 1_000_000d;
		double rEps = DoubleComparer.DEFAULT_DOUBLE_RELATIVE_EPSILON;
		double b    = a + rEps * a * 2;

		DoubleComparer.AreEqualRelative(a, b).Should().BeFalse();
	}

	[Fact]
	public void AreEqualRelative_WhenDouble_ShouldReturnTrue_AtRelativeBoundary()
	{
		// Use a value near 1.0 to avoid rounding artifacts at tight relative epsilons
		var    a    = 10_000_000d;
		double rEps = DoubleComparer.DEFAULT_DOUBLE_RELATIVE_EPSILON;
		double b    = a + rEps * a;

		DoubleComparer.AreEqualRelative(a, b).Should().BeTrue();
	}

	[Fact]
	public void AreEqualRelative_WhenDouble_ShouldThrow_WhenEpsilonIsNegative()
	{
		Action act = () => DoubleComparer.AreEqualRelative(1d, 2d, -0.1d);
		act.Should().Throw<ArgumentOutOfRangeException>();
	}

	[Fact]
	public void AreEqualRelative_WhenDouble_ShouldUseFallback_ForNearZeroValues()
	{
		// Near-zero values should use absolute epsilon fallback
		DoubleComparer.AreEqualRelative(0d, 1e-16d).Should().BeTrue();  // Within absolute epsilon
		DoubleComparer.AreEqualRelative(0d, 1e-14d).Should().BeFalse(); // Beyond absolute epsilon
	}

	[Fact]
	public void AreEqualRobust_WhenDouble_ShouldThrow_WhenEpsilonIsNegative()
	{
		Action act1 = () => DoubleComparer.AreEqualRobust(1d, 2d, -0.1d, 0.1d);
		Action act2 = () => DoubleComparer.AreEqualRobust(1d, 2d, 0.1d,  -0.1d);
		act1.Should().Throw<ArgumentOutOfRangeException>();
		act2.Should().Throw<ArgumentOutOfRangeException>();
	}

	[Fact]
	public void AreEqualRobust_WhenDouble_ShouldUseAbsoluteEpsilon_NearZero_BoundaryInclusive()
	{
		double eps = DoubleComparer.DEFAULT_DOUBLE_EPSILON;

		DoubleComparer.AreEqualRobust(0d, eps).Should().BeTrue();
		DoubleComparer.AreEqualRobust(0d, eps * 2).Should().BeFalse();
	}

	[Fact]
	public void AreEqualRobust_WhenDouble_ShouldUseRelativeEpsilon_ForLargerNumbers_BoundaryInclusive()
	{
		var    a     = 1e8d;
		double rEps  = DoubleComparer.DEFAULT_DOUBLE_RELATIVE_EPSILON;
		double delta = rEps * Math.Abs(a);

		DoubleComparer.AreEqualRobust(a, a + delta).Should().BeTrue();
		DoubleComparer.AreEqualRobust(a, a + delta * 2).Should().BeFalse();
	}

	[Fact]
	public void IsGreaterThan_WhenDouble_ShouldBeFalse_WhenWithinEpsilon()
	{
		double eps = DoubleComparer.DEFAULT_DOUBLE_EPSILON;
		DoubleComparer.IsGreaterThan(1d + eps / 2, 1d).Should().BeFalse();
	}

	[Fact]
	public void IsGreaterThan_WhenDouble_ShouldBeTrue_WhenDifferenceExceedsEpsilon()
	{
		// Use larger numbers to exceed relative epsilon threshold
		var    a    = 1000d;
		double rEps = DoubleComparer.DEFAULT_DOUBLE_RELATIVE_EPSILON;
		DoubleComparer.IsGreaterThan(a + rEps * a * 10, a).Should().BeTrue();
	}

	[Fact]
	public void IsGreaterThanOrEqual_WhenDouble_ShouldBeFalse_WhenLessBeyondEpsilon()
	{
		// Use larger numbers to exceed relative epsilon threshold
		var    a    = 1000d;
		double rEps = DoubleComparer.DEFAULT_DOUBLE_RELATIVE_EPSILON;
		DoubleComparer.IsGreaterThanOrEqual(a, a + rEps * a * 10).Should().BeFalse();
	}

	[Fact]
	public void IsGreaterThanOrEqual_WhenDouble_ShouldBeTrue_WhenEqualWithinEpsilon()
	{
		double eps = DoubleComparer.DEFAULT_DOUBLE_EPSILON;
		DoubleComparer.IsGreaterThanOrEqual(1d, 1d + eps / 2).Should().BeTrue();
	}

	[Fact]
	public void IsLessThan_WhenDouble_ShouldBeFalse_WhenWithinEpsilon()
	{
		double eps = DoubleComparer.DEFAULT_DOUBLE_EPSILON;
		DoubleComparer.IsLessThan(1d, 1d + eps / 2).Should().BeFalse();
	}

	[Fact]
	public void IsLessThan_WhenDouble_ShouldBeTrue_WhenDifferenceExceedsEpsilon()
	{
		// Use larger numbers to exceed relative epsilon threshold
		var    a    = 1000d;
		double rEps = DoubleComparer.DEFAULT_DOUBLE_RELATIVE_EPSILON;
		DoubleComparer.IsLessThan(a, a + rEps * a * 10).Should().BeTrue();
	}

	[Fact]
	public void IsLessThanOrEqual_WhenDouble_ShouldBeFalse_WhenGreaterBeyondEpsilon()
	{
		// Use larger numbers to exceed relative epsilon threshold
		var    a    = 1000d;
		double rEps = DoubleComparer.DEFAULT_DOUBLE_RELATIVE_EPSILON;
		DoubleComparer.IsLessThanOrEqual(a + rEps * a * 10, a).Should().BeFalse();
	}

	[Fact]
	public void IsLessThanOrEqual_WhenDouble_ShouldBeTrue_WhenEqualWithinEpsilon()
	{
		double eps = DoubleComparer.DEFAULT_DOUBLE_EPSILON;
		DoubleComparer.IsLessThanOrEqual(1d + eps / 2, 1d).Should().BeTrue();
	}

	[Fact]
	public void IsZero_WhenDouble_ShouldBeFalse_OutsideBoundary()
	{
		double eps = DoubleComparer.DEFAULT_DOUBLE_EPSILON;
		DoubleComparer.IsZero(eps * 2).Should().BeFalse();
	}

	[Fact]
	public void IsZero_WhenDouble_ShouldBeTrue_AtBoundaryAndZero()
	{
		double eps = DoubleComparer.DEFAULT_DOUBLE_EPSILON;
		DoubleComparer.IsZero(0d).Should().BeTrue();
		DoubleComparer.IsZero(eps).Should().BeTrue();
	}

	[Fact]
	public void Sign_WhenDouble_ShouldReturn0NearZero_ElseMathSign()
	{
		double eps = DoubleComparer.DEFAULT_DOUBLE_EPSILON;
		DoubleComparer.Sign(0d).Should().Be(0);
		DoubleComparer.Sign(eps).Should().Be(0);
		DoubleComparer.Sign(eps * 2).Should().Be(1);
		DoubleComparer.Sign(-eps * 2).Should().Be(-1);
	}
}
