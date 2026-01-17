using Bezoro.Core.Helpers;
using FluentAssertions;
using JetBrains.Annotations;
using Xunit;

namespace Bezoro.Core.Tests.Common.Helpers;

[TestSubject(typeof(FloatHelper))]
public static class FloatHelperTests
{
	public class Unit
	{
		[Theory]
		[InlineData(-0.1f, 0f, 1f)]
		[InlineData(1.1f,  0f, 1f)]
		public void IsWithinRange_ShouldReturnFalse_WhenOutsideBounds(float value, float min, float max) =>
			value.IsWithinRange(min, max).Should().BeFalse();

		[Theory]
		[InlineData(0f,   0f, 1f)]
		[InlineData(1f,   0f, 1f)]
		[InlineData(0.5f, 0f, 1f)]
		public void IsWithinRange_ShouldReturnTrue_WhenWithinInclusiveBounds(float value, float min, float max) =>
			value.IsWithinRange(min, max).Should().BeTrue();

		[Fact]
		public void Map_ShouldHandleNegativeSourceRange()
		{
			0f.Map(-1f, 1f, 0f, 10f).Should().Be(5f);
		}

		[Fact]
		public void Map_ShouldHandleReversedTargetRange()
		{
			0.25f.Map(0f, 1f, 10f, 0f).Should().Be(7.5f);
		}

		[Fact]
		public void Map_ShouldMapLinearly_FromZeroToTen_IntoZeroToHundred()
		{
			5f.Map(0f, 10f, 0f, 100f).Should().Be(50f);
		}

		[Fact]
		public void Map_ShouldReturnConstant_WhenTargetRangeIsConstant()
		{
			7f.Map(0f, 10f, 5f, 5f).Should().Be(5f);
		}

		[Fact]
		public void Map_ShouldReturnNaN_WhenSourceRangeDegenerate_AndValueEqualsMin()
		{
			float result = 2f.Map(2f, 2f, 0f, 10f);
			float.IsNaN(result).Should().BeTrue();
		}

		[Fact]
		public void Map_ShouldReturnNegativeInfinity_WhenSourceRangeDegenerate_AndValueBelowMin()
		{
			float result = 1f.Map(2f, 2f, 0f, 10f);
			float.IsNegativeInfinity(result).Should().BeTrue();
		}

		[Fact]
		public void Map_ShouldReturnPositiveInfinity_WhenSourceRangeDegenerate_AndValueAboveMin()
		{
			float result = 3f.Map(2f, 2f, 0f, 10f);
			float.IsPositiveInfinity(result).Should().BeTrue();
		}
	}
}
