using System;
using FluentAssertions;
using JetBrains.Annotations;
using Xunit;

namespace Bezoro.Core.Tests;

[TestSubject(typeof(FloatComparer))]
public static class FloatComparerTests
{
	public class Unit
	{
		[Fact]
		public void AreEqual_Double_ShouldBeFalse_WhenDiffGreaterThanEpsilon()
		{
			double eps = FloatComparer.DEFAULT_DOUBLE_EPSILON;
			FloatComparer.AreEqual(0d, eps * 2).Should().BeFalse();
		}

		[Fact]
		public void AreEqual_Double_ShouldBeTrue_WhenDiffEqualsEpsilon_Inclusive()
		{
			double eps = FloatComparer.DEFAULT_DOUBLE_EPSILON;
			FloatComparer.AreEqual(0d, eps).Should().BeTrue();
		}

		[Fact]
		public void AreEqual_Double_ShouldReturnFalse_WhenEitherIsNaNOrInfinity()
		{
			FloatComparer.AreEqual(double.NaN,              1d).Should().BeFalse();
			FloatComparer.AreEqual(1d,                      double.NaN).Should().BeFalse();
			FloatComparer.AreEqual(double.PositiveInfinity, 1d).Should().BeFalse();
			FloatComparer.AreEqual(1d,                      double.NegativeInfinity).Should().BeFalse();
		}

		[Fact]
		public void AreEqual_Double_ShouldReturnTrue_ForExactEquality()
		{
			FloatComparer.AreEqual(1.23d, 1.23d).Should().BeTrue();
		}

		[Fact]
		public void AreEqual_Float_ShouldBeFalse_WhenDiffGreaterThanEpsilon()
		{
			float eps = FloatComparer.DEFAULT_FLOAT_EPSILON;
			FloatComparer.AreEqual(0f, eps * 2).Should().BeFalse();
		}

		[Fact]
		public void AreEqual_Float_ShouldBeTrue_WhenDiffEqualsEpsilon_Inclusive()
		{
			float eps = FloatComparer.DEFAULT_FLOAT_EPSILON;
			FloatComparer.AreEqual(0f, eps).Should().BeTrue();
		}

		[Fact]
		public void AreEqual_Float_ShouldReturnFalse_WhenEitherIsNaNOrInfinity()
		{
			FloatComparer.AreEqual(float.NaN,              1f).Should().BeFalse();
			FloatComparer.AreEqual(1f,                     float.NaN).Should().BeFalse();
			FloatComparer.AreEqual(float.PositiveInfinity, 1f).Should().BeFalse();
			FloatComparer.AreEqual(1f,                     float.NegativeInfinity).Should().BeFalse();
		}

		[Fact]
		public void AreEqual_Float_ShouldReturnTrue_ForExactEquality()
		{
			FloatComparer.AreEqual(1.23f, 1.23f).Should().BeTrue();
		}

		[Fact]
		public void AreEqualRelative_Double_ShouldReturnFalse_WhenBeyondRelativeTolerance()
		{
			var    a    = 1_000_000d;
			double rEps = FloatComparer.DEFAULT_DOUBLE_RELATIVE_EPSILON;
			double b    = a + rEps * a * 2;

			FloatComparer.AreEqualRelative(a, b).Should().BeFalse();
		}

		[Fact]
		public void AreEqualRelative_Double_ShouldReturnTrue_AtRelativeBoundary()
		{
			// Use a value near 1.0 to avoid rounding artifacts at tight relative epsilons
			var    a    = 10_000_000d;
			double rEps = FloatComparer.DEFAULT_DOUBLE_RELATIVE_EPSILON;
			double b    = a + rEps * a;

			FloatComparer.AreEqualRelative(a, b).Should().BeTrue();
		}

		[Fact]
		public void AreEqualRelative_Float_ShouldReturnFalse_WhenBeyondRelativeTolerance()
		{
			var   a    = 1000f;
			float rEps = FloatComparer.DEFAULT_RELATIVE_EPSILON;
			float b    = a + rEps * a * 2;

			FloatComparer.AreEqualRelative(a, b).Should().BeFalse();
		}

		[Fact]
		public void AreEqualRelative_Float_ShouldReturnTrue_AtRelativeBoundary()
		{
			// Use 1.0f to reduce rounding error at the boundary of relative epsilon
			var   a    = 10_000_000f;
			float rEps = FloatComparer.DEFAULT_RELATIVE_EPSILON;
			float b    = a + rEps * a;

			FloatComparer.AreEqualRelative(a, b).Should().BeTrue();
		}

		[Fact]
		public void AreEqualRelative_Float_ShouldReturnTrue_ForZeros()
		{
			FloatComparer.AreEqualRelative(0f, 0f).Should().BeTrue();
		}

		[Fact]
		public void AreEqualRobust_Double_ShouldUseAbsoluteEpsilon_NearZero_BoundaryInclusive()
		{
			double eps = FloatComparer.DEFAULT_DOUBLE_EPSILON;

			FloatComparer.AreEqualRobust(0d, eps).Should().BeTrue();
			FloatComparer.AreEqualRobust(0d, eps * 2).Should().BeFalse();
		}

		[Fact]
		public void AreEqualRobust_Double_ShouldUseRelativeEpsilon_ForLargerNumbers_BoundaryInclusive()
		{
			var    a     = 1e8d;
			double rEps  = FloatComparer.DEFAULT_DOUBLE_RELATIVE_EPSILON;
			double delta = rEps * Math.Abs(a);

			FloatComparer.AreEqualRobust(a, a + delta).Should().BeTrue();
			FloatComparer.AreEqualRobust(a, a + delta * 2).Should().BeFalse();
		}

		[Fact]
		public void AreEqualRobust_Float_ShouldHandleNaNAndInfinity_AsFalse()
		{
			FloatComparer.AreEqualRobust(float.NaN, 1f).Should().BeFalse();
			FloatComparer.AreEqualRobust(1f,        float.PositiveInfinity).Should().BeFalse();
		}

		[Fact]
		public void AreEqualRobust_Float_ShouldUseAbsoluteEpsilon_NearZero_BoundaryInclusive()
		{
			float eps = FloatComparer.DEFAULT_FLOAT_EPSILON;

			FloatComparer.AreEqualRobust(0f, eps).Should().BeTrue();
			FloatComparer.AreEqualRobust(0f, eps * 2).Should().BeFalse();
		}

		[Fact]
		public void AreEqualRobust_Float_ShouldUseRelativeEpsilon_ForLargerNumbers_BoundaryInclusive()
		{
			var   a     = 12345f;
			float rEps  = FloatComparer.DEFAULT_RELATIVE_EPSILON;
			float delta = rEps * Math.Abs(a);

			FloatComparer.AreEqualRobust(a, a + delta).Should().BeTrue();
			FloatComparer.AreEqualRobust(a, a + delta * 2).Should().BeFalse();
		}

		[Fact]
		public void Clamp_Double_ShouldClampBelowAndAbove_OrReturnValueInside()
		{
			FloatComparer.Clamp(-1d,  0d, 1d).Should().Be(0d);
			FloatComparer.Clamp(2d,   0d, 1d).Should().Be(1d);
			FloatComparer.Clamp(0.5d, 0d, 1d).Should().Be(0.5d);
		}

		[Fact]
		public void Clamp_Float_ShouldClampBelowAndAbove_OrReturnValueInside()
		{
			FloatComparer.Clamp(-1f,  0f, 1f).Should().Be(0f);
			FloatComparer.Clamp(2f,   0f, 1f).Should().Be(1f);
			FloatComparer.Clamp(0.5f, 0f, 1f).Should().Be(0.5f);
		}

		[Fact]
		public void IsGreaterThan_Double_ShouldBeFalse_WhenWithinEpsilon()
		{
			double eps = FloatComparer.DEFAULT_DOUBLE_EPSILON;
			FloatComparer.IsGreaterThan(1d + eps / 2, 1d).Should().BeFalse();
		}

		[Fact]
		public void IsGreaterThan_Double_ShouldBeTrue_WhenDifferenceExceedsEpsilon()
		{
			double eps = FloatComparer.DEFAULT_DOUBLE_EPSILON;
			FloatComparer.IsGreaterThan(1d + eps * 10, 1d).Should().BeTrue();
		}

		[Fact]
		public void IsGreaterThan_Float_ShouldBeFalse_WhenWithinEpsilon()
		{
			float eps = FloatComparer.DEFAULT_FLOAT_EPSILON;
			FloatComparer.IsGreaterThan(1f + eps / 2, 1f).Should().BeFalse();
		}

		[Fact]
		public void IsGreaterThan_Float_ShouldBeTrue_WhenDifferenceExceedsEpsilon()
		{
			float eps = FloatComparer.DEFAULT_FLOAT_EPSILON;
			FloatComparer.IsGreaterThan(1f + eps * 10, 1f).Should().BeTrue();
		}

		[Fact]
		public void IsGreaterThanOrEqual_Double_ShouldBeFalse_WhenLessBeyondEpsilon()
		{
			double eps = FloatComparer.DEFAULT_DOUBLE_EPSILON;
			FloatComparer.IsGreaterThanOrEqual(1d, 1d + eps * 10).Should().BeFalse();
		}

		[Fact]
		public void IsGreaterThanOrEqual_Double_ShouldBeTrue_WhenEqualWithinEpsilon()
		{
			double eps = FloatComparer.DEFAULT_DOUBLE_EPSILON;
			FloatComparer.IsGreaterThanOrEqual(1d, 1d + eps / 2).Should().BeTrue();
		}

		[Fact]
		public void IsGreaterThanOrEqual_Float_ShouldBeFalse_WhenLessBeyondEpsilon()
		{
			float eps = FloatComparer.DEFAULT_FLOAT_EPSILON;
			FloatComparer.IsGreaterThanOrEqual(1f, 1f + eps * 10).Should().BeFalse();
		}

		[Fact]
		public void IsGreaterThanOrEqual_Float_ShouldBeTrue_WhenEqualWithinEpsilon()
		{
			float eps = FloatComparer.DEFAULT_FLOAT_EPSILON;
			FloatComparer.IsGreaterThanOrEqual(1f, 1f + eps / 2).Should().BeTrue();
		}

		[Fact]
		public void IsLessThan_Double_ShouldBeFalse_WhenWithinEpsilon()
		{
			double eps = FloatComparer.DEFAULT_DOUBLE_EPSILON;
			FloatComparer.IsLessThan(1d, 1d + eps / 2).Should().BeFalse();
		}

		[Fact]
		public void IsLessThan_Double_ShouldBeTrue_WhenDifferenceExceedsEpsilon()
		{
			double eps = FloatComparer.DEFAULT_DOUBLE_EPSILON;
			FloatComparer.IsLessThan(1d, 1d + eps * 10).Should().BeTrue();
		}

		[Fact]
		public void IsLessThan_Float_ShouldBeFalse_WhenWithinEpsilon()
		{
			float eps = FloatComparer.DEFAULT_FLOAT_EPSILON;
			FloatComparer.IsLessThan(1f, 1f + eps / 2).Should().BeFalse();
		}

		[Fact]
		public void IsLessThan_Float_ShouldBeTrue_WhenDifferenceExceedsEpsilon()
		{
			float eps = FloatComparer.DEFAULT_FLOAT_EPSILON;
			FloatComparer.IsLessThan(1f, 1f + eps * 10).Should().BeTrue();
		}

		[Fact]
		public void IsLessThanOrEqual_Double_ShouldBeFalse_WhenGreaterBeyondEpsilon()
		{
			double eps = FloatComparer.DEFAULT_DOUBLE_EPSILON;
			FloatComparer.IsLessThanOrEqual(1d + eps * 10, 1d).Should().BeFalse();
		}

		[Fact]
		public void IsLessThanOrEqual_Double_ShouldBeTrue_WhenEqualWithinEpsilon()
		{
			double eps = FloatComparer.DEFAULT_DOUBLE_EPSILON;
			FloatComparer.IsLessThanOrEqual(1d + eps / 2, 1d).Should().BeTrue();
		}

		[Fact]
		public void IsLessThanOrEqual_Float_ShouldBeFalse_WhenGreaterBeyondEpsilon()
		{
			float eps = FloatComparer.DEFAULT_FLOAT_EPSILON;
			FloatComparer.IsLessThanOrEqual(1f + eps * 10, 1f).Should().BeFalse();
		}

		[Fact]
		public void IsLessThanOrEqual_Float_ShouldBeTrue_WhenEqualWithinEpsilon()
		{
			float eps = FloatComparer.DEFAULT_FLOAT_EPSILON;
			FloatComparer.IsLessThanOrEqual(1f + eps / 2, 1f).Should().BeTrue();
		}

		[Fact]
		public void IsZero_Double_ShouldBeFalse_OutsideBoundary()
		{
			double eps = FloatComparer.DEFAULT_DOUBLE_EPSILON;
			FloatComparer.IsZero(eps * 2).Should().BeFalse();
		}

		[Fact]
		public void IsZero_Double_ShouldBeTrue_AtBoundaryAndZero()
		{
			double eps = FloatComparer.DEFAULT_DOUBLE_EPSILON;
			FloatComparer.IsZero(0d).Should().BeTrue();
			FloatComparer.IsZero(eps).Should().BeTrue();
		}

		[Fact]
		public void IsZero_Float_ShouldBeFalse_OutsideBoundary()
		{
			float eps = FloatComparer.DEFAULT_FLOAT_EPSILON;
			FloatComparer.IsZero(eps * 2).Should().BeFalse();
		}

		[Fact]
		public void IsZero_Float_ShouldBeTrue_AtBoundaryAndZero()
		{
			float eps = FloatComparer.DEFAULT_FLOAT_EPSILON;
			FloatComparer.IsZero(0f).Should().BeTrue();
			FloatComparer.IsZero(eps).Should().BeTrue();
		}

		[Fact]
		public void Sign_Double_ShouldReturn0NearZero_ElseMathSign()
		{
			double eps = FloatComparer.DEFAULT_DOUBLE_EPSILON;
			FloatComparer.Sign(0d).Should().Be(0);
			FloatComparer.Sign(eps).Should().Be(0);
			FloatComparer.Sign(eps * 2).Should().Be(1);
			FloatComparer.Sign(-eps * 2).Should().Be(-1);
		}

		[Fact]
		public void Sign_Float_ShouldReturn0NearZero_ElseMathSign()
		{
			float eps = FloatComparer.DEFAULT_FLOAT_EPSILON;
			FloatComparer.Sign(0f).Should().Be(0);
			FloatComparer.Sign(eps).Should().Be(0);
			FloatComparer.Sign(eps * 2).Should().Be(1);
			FloatComparer.Sign(-eps * 2).Should().Be(-1);
		}
	}
}
