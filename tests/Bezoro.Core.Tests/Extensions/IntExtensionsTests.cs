using Bezoro.Core.Extensions;
using FluentAssertions;
using JetBrains.Annotations;
using Xunit;

using Bezoro.Core.Types.Exceptions;
namespace Bezoro.Core.Tests.Common.Extensions;

[TestSubject(typeof(IntExtensions))]
public static class IntExtensionsTests
{
	public class Unit
	{
		[Theory]
		[InlineData(-1,   1)]
		[InlineData(-100, 100)]
		public void Abs_ShouldReturnPositiveValue_WhenValueIsNegative(int value, int expected) =>
			value.Abs().Should().Be(expected);

		[Theory]
		[InlineData(1)]
		[InlineData(100)]
		public void Abs_ShouldReturnSameValue_WhenValueIsPositive(int value) =>
			value.Abs().Should().Be(value);

		[Theory]
		[InlineData(5,  0, 10, 5)]
		[InlineData(-1, 0, 10, 0)]
		[InlineData(11, 0, 10, 10)]
		public void Clamp_ShouldReturnClampedValue(int value, int min, int max, int expected) =>
			value.Clamp(min, max).Should().Be(expected);

		[Theory]
		[InlineData(5,  10, 5)]
		[InlineData(15, 10, 10)]
		public void ClampMax_ShouldReturnClampedValue(int value, int max, int expected) =>
			value.ClampMax(max).Should().Be(expected);

		[Theory]
		[InlineData(5,  10, 10)]
		[InlineData(15, 10, 15)]
		public void ClampMin_ShouldReturnClampedValue(int value, int min, int expected) =>
			value.ClampMin(min).Should().Be(expected);

		[Theory]
		[InlineData(1)]
		[InlineData(3)]
		[InlineData(-1)]
		public void IsEven_ShouldReturnFalse_WhenValueIsOdd(int value) => value.IsEven().Should().BeFalse();

		[Theory]
		[InlineData(2)]
		[InlineData(4)]
		[InlineData(0)]
		public void IsEven_ShouldReturnTrue_WhenValueIsEven(int value) => value.IsEven().Should().BeTrue();

		[Theory]
		[InlineData(0)]
		[InlineData(1)]
		public void IsNegative_ShouldReturnFalse_WhenValueIsZeroOrPositive(int value) =>
			value.IsNegative().Should().BeFalse();

		[Theory]
		[InlineData(-1)]
		[InlineData(-100)]
		public void IsNegative_ShouldReturnTrue_WhenValueIsNegative(int value) => value.IsNegative().Should().BeTrue();

		[Theory]
		[InlineData(2)]
		[InlineData(4)]
		[InlineData(0)]
		public void IsOdd_ShouldReturnFalse_WhenValueIsEven(int value) => value.IsOdd().Should().BeFalse();

		[Theory]
		[InlineData(1)]
		[InlineData(3)]
		public void IsOdd_ShouldReturnTrue_WhenValueIsOdd(int value) => value.IsOdd().Should().BeTrue();

		[Theory]
		[InlineData(0)]
		[InlineData(-1)]
		public void IsPositive_ShouldReturnFalse_WhenValueIsZeroOrNegative(int value) =>
			value.IsPositive().Should().BeFalse();

		[Theory]
		[InlineData(1)]
		[InlineData(100)]
		public void IsPositive_ShouldReturnTrue_WhenValueIsPositive(int value) => value.IsPositive().Should().BeTrue();

		[Theory]
		[InlineData(1)]
		[InlineData(-1)]
		public void IsZero_ShouldReturnFalse_WhenValueIsNotZero(int value) => value.IsZero().Should().BeFalse();

		[Fact]
		public void IsZero_ShouldReturnTrue_WhenValueIsZero() => 0.IsZero().Should().BeTrue();

		[Theory]
		[InlineData(14, 5, 15)]
		[InlineData(11, 5, 10)]
		public void RoundToNearest_ShouldReturnRoundedValue(int value, int nearest, int expected) =>
			value.RoundToNearest(nearest).Should().Be(expected);

		[Theory]
		[InlineData(-1, -1)]
		[InlineData(1,  1)]
		public void Sign_ShouldReturnCorrectSign(int value, int expected) =>
			value.Sign().Should().Be(expected);

		[Theory]
		[InlineData(5,  0)]
		[InlineData(10, 5)]
		public void ThrowIfLessThan_ShouldNotThrow_WhenValueIsNotLessThanMin(int value, int min) =>
			value.Invoking(v => v.ThrowIfLessThan(min)).Should().NotThrow();

		[Theory]
		[InlineData(-1, 0)]
		[InlineData(4,  5)]
		public void ThrowIfLessThan_ShouldThrow_WhenValueIsLessThanMin(int value, int min) =>
			value.Invoking(v => v.ThrowIfLessThan(min)).Should().Throw<ValueTooSmallException>();

		[Theory]
		[InlineData(0, 5)]
		[InlineData(5, 10)]
		public void ThrowIfMoreThan_ShouldNotThrow_WhenValueIsNotMoreThanMax(int value, int max) =>
			value.Invoking(v => v.ThrowIfMoreThan(max)).Should().NotThrow();

		[Theory]
		[InlineData(1, 0)]
		[InlineData(6, 5)]
		public void ThrowIfMoreThan_ShouldThrow_WhenValueIsMoreThanMax(int value, int max) =>
			value.Invoking(v => v.ThrowIfMoreThan(max)).Should().Throw<ValueTooLargeException>();
	}
}


