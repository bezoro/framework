using System;
using Bezoro.Core.Extensions;
using Bezoro.Core.Types.Exceptions;
using FluentAssertions;
using JetBrains.Annotations;
using Xunit;

namespace Bezoro.Core.Tests.Extensions;

[TestSubject(typeof(FloatExtensions))]
public class FloatExtensionsTests
{
	[Fact]
	public void ThrowIfLessOrEqualThan_WhenCalled_ShouldNotThrowAndReturnNaN_WhenValueIsNaN()
	{
		float result = float.NaN.ThrowIfLessOrEqualThan(0f);
		float.IsNaN(result).Should().BeTrue();
	}

	[Theory]
	[InlineData(1.1f, 1f)]
	[InlineData(2f,   1f)]
	public void ThrowIfLessOrEqualThan_WhenCalled_ShouldNotThrowAndReturnValue_WhenValueIsGreaterThanMin(
		float value,
		float min)
	{
		float result = value.ThrowIfLessOrEqualThan(min);
		result.Should().Be(value);
	}

	[Theory]
	[InlineData(1f, 1f)]
	[InlineData(0f, 1f)]
	public void ThrowIfLessOrEqualThan_WhenCalled_ShouldThrow_WhenValueIsLessOrEqualThanMin(float value, float min) =>
		value.Invoking(v => v.ThrowIfLessOrEqualThan(min)).Should().Throw<ValueTooSmallException>();

	[Fact]
	public void ThrowIfLessThan_WhenCalled_ShouldNotThrowAndReturnNaN_WhenValueIsNaN()
	{
		float result = float.NaN.ThrowIfLessThan(0f);
		float.IsNaN(result).Should().BeTrue();
	}

	[Theory]
	[InlineData(1f, 1f)] // equal boundary
	[InlineData(2f, 1f)] // greater than
	public void ThrowIfLessThan_WhenCalled_ShouldNotThrowAndReturnValue_WhenValueIsEqualOrGreaterThanMin(
		float value,
		float min)
	{
		float result = value.ThrowIfLessThan(min);
		result.Should().Be(value);
	}

	[Theory]
	[InlineData(0f,  1f)]
	[InlineData(-1f, 0f)]
	public void ThrowIfLessThan_WhenCalled_ShouldThrow_WhenValueIsLessThanMin(float value, float min) =>
		value.Invoking(v => v.ThrowIfLessThan(min)).Should().Throw<ValueTooSmallException>();

	[Fact]
	public void ThrowIfOverOrEqualThan_WhenCalled_ShouldNotThrowAndReturnNaN_WhenValueIsNaN()
	{
		float result = float.NaN.ThrowIfOverOrEqualThan(0f);
		float.IsNaN(result).Should().BeTrue();
	}

	[Theory]
	[InlineData(1.9f, 2f)]
	[InlineData(-10f, 2f)]
	public void ThrowIfOverOrEqualThan_WhenCalled_ShouldNotThrowAndReturnValue_WhenValueIsLessThanMax(
		float value,
		float max)
	{
		float result = value.ThrowIfOverOrEqualThan(max);
		result.Should().Be(value);
	}

	[Theory]
	[InlineData(2f, 2f)]
	[InlineData(3f, 2f)]
	public void ThrowIfOverOrEqualThan_WhenCalled_ShouldThrow_WhenValueIsOverOrEqualToMax(float value, float max) =>
		value.Invoking(v => v.ThrowIfOverOrEqualThan(max)).Should().Throw<ValueTooLargeException>();

	[Fact]
	public void ThrowIfOverThan_WhenCalled_ShouldNotThrowAndReturnNaN_WhenValueIsNaN()
	{
		float result = float.NaN.ThrowIfOverThan(0f);
		float.IsNaN(result).Should().BeTrue();
	}

	[Theory]
	[InlineData(2f, 2f)] // equal boundary
	[InlineData(1f, 2f)] // less than
	public void ThrowIfOverThan_WhenCalled_ShouldNotThrowAndReturnValue_WhenValueIsEqualOrLessThanMax(
		float value,
		float max)
	{
		float result = value.ThrowIfOverThan(max);
		result.Should().Be(value);
	}

	[Theory]
	[InlineData(3f,   2f)]
	[InlineData(0.1f, 0f)]
	public void ThrowIfOverThan_WhenCalled_ShouldThrow_WhenValueIsOverThanMax(float value, float max) =>
		value.Invoking(v => v.ThrowIfOverThan(max)).Should().Throw<ValueTooLargeException>();

	#region IsBetween

	[Theory]
	[InlineData(-0.1f, 0f, 1f)]
	[InlineData(1.1f,  0f, 1f)]
	public void IsBetween_WhenCalled_ShouldReturnFalse_WhenOutsideBounds(float value, float min, float max) =>
		value.IsBetween(min, max).Should().BeFalse();

	[Theory]
	[InlineData(0f,   0f, 1f)]
	[InlineData(1f,   0f, 1f)]
	[InlineData(0.5f, 0f, 1f)]
	public void IsBetween_WhenCalled_ShouldReturnTrue_WhenWithinInclusiveBounds(float value, float min, float max) =>
		value.IsBetween(min, max).Should().BeTrue();

	#endregion

	#region Map

	[Fact]
	public void Map_WhenCalled_ShouldMapLinearly_FromZeroToTen_IntoZeroToHundred()
	{
		5f.Map(0f, 10f, 0f, 100f).Should().Be(50f);
	}

	[Fact]
	public void Map_WhenCalled_ShouldHandleNegativeSourceRange()
	{
		0f.Map(-1f, 1f, 0f, 10f).Should().Be(5f);
	}

	[Fact]
	public void Map_WhenCalled_ShouldHandleReversedTargetRange()
	{
		0.25f.Map(0f, 1f, 10f, 0f).Should().Be(7.5f);
	}

	[Fact]
	public void Map_WhenCalled_ShouldReturnConstant_WhenTargetRangeIsConstant()
	{
		7f.Map(0f, 10f, 5f, 5f).Should().Be(5f);
	}

	[Fact]
	public void Map_WhenCalled_ShouldThrowArgumentException_WhenSourceRangeIsZero()
	{
		2f.Invoking(v => v.Map(2f, 2f, 0f, 10f))
		  .Should().Throw<ArgumentException>()
		  .WithMessage("*Source range cannot be zero*");
	}

	#endregion
}
