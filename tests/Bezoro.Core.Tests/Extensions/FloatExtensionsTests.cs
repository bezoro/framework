using Bezoro.Core.Extensions;
using FluentAssertions;
using JetBrains.Annotations;
using Xunit;

using Bezoro.Core.Types.Exceptions;
namespace Bezoro.Core.Tests.Common.Extensions;

[TestSubject(typeof(FloatExtensions))]
public static class FloatExtensionsTests
{
	public class Unit
	{
		[Fact]
		public void ThrowIfLessOrEqualThan_ShouldNotThrowAndReturnNaN_WhenValueIsNaN()
		{
			float result = float.NaN.ThrowIfLessOrEqualThan(0f);
			float.IsNaN(result).Should().BeTrue();
		}

		[Theory]
		[InlineData(1.1f, 1f)]
		[InlineData(2f,   1f)]
		public void ThrowIfLessOrEqualThan_ShouldNotThrowAndReturnValue_WhenValueIsGreaterThanMin(
			float value,
			float min)
		{
			float result = value.ThrowIfLessOrEqualThan(min);
			result.Should().Be(value);
		}

		[Theory]
		[InlineData(1f, 1f)]
		[InlineData(0f, 1f)]
		public void ThrowIfLessOrEqualThan_ShouldThrow_WhenValueIsLessOrEqualThanMin(float value, float min) =>
			value.Invoking(v => v.ThrowIfLessOrEqualThan(min)).Should().Throw<ValueTooSmallException>();

		[Fact]
		public void ThrowIfLessThan_ShouldNotThrowAndReturnNaN_WhenValueIsNaN()
		{
			float result = float.NaN.ThrowIfLessThan(0f);
			float.IsNaN(result).Should().BeTrue();
		}

		[Theory]
		[InlineData(1f, 1f)] // equal boundary
		[InlineData(2f, 1f)] // greater than
		public void ThrowIfLessThan_ShouldNotThrowAndReturnValue_WhenValueIsEqualOrGreaterThanMin(
			float value,
			float min)
		{
			float result = value.ThrowIfLessThan(min);
			result.Should().Be(value);
		}

		[Theory]
		[InlineData(0f,  1f)]
		[InlineData(-1f, 0f)]
		public void ThrowIfLessThan_ShouldThrow_WhenValueIsLessThanMin(float value, float min) =>
			value.Invoking(v => v.ThrowIfLessThan(min)).Should().Throw<ValueTooSmallException>();

		[Fact]
		public void ThrowIfOverOrEqualThan_ShouldNotThrowAndReturnNaN_WhenValueIsNaN()
		{
			float result = float.NaN.ThrowIfOverOrEqualThan(0f);
			float.IsNaN(result).Should().BeTrue();
		}

		[Theory]
		[InlineData(1.9f, 2f)]
		[InlineData(-10f, 2f)]
		public void ThrowIfOverOrEqualThan_ShouldNotThrowAndReturnValue_WhenValueIsLessThanMax(float value, float max)
		{
			float result = value.ThrowIfOverOrEqualThan(max);
			result.Should().Be(value);
		}

		[Theory]
		[InlineData(2f, 2f)]
		[InlineData(3f, 2f)]
		public void ThrowIfOverOrEqualThan_ShouldThrow_WhenValueIsOverOrEqualToMax(float value, float max) =>
			value.Invoking(v => v.ThrowIfOverOrEqualThan(max)).Should().Throw<ValueTooLargeException>();

		[Fact]
		public void ThrowIfOverThan_ShouldNotThrowAndReturnNaN_WhenValueIsNaN()
		{
			float result = float.NaN.ThrowIfOverThan(0f);
			float.IsNaN(result).Should().BeTrue();
		}

		[Theory]
		[InlineData(2f, 2f)] // equal boundary
		[InlineData(1f, 2f)] // less than
		public void ThrowIfOverThan_ShouldNotThrowAndReturnValue_WhenValueIsEqualOrLessThanMax(float value, float max)
		{
			float result = value.ThrowIfOverThan(max);
			result.Should().Be(value);
		}

		[Theory]
		[InlineData(3f,   2f)]
		[InlineData(0.1f, 0f)]
		public void ThrowIfOverThan_ShouldThrow_WhenValueIsOverThanMax(float value, float max) =>
			value.Invoking(v => v.ThrowIfOverThan(max)).Should().Throw<ValueTooLargeException>();
	}
}


