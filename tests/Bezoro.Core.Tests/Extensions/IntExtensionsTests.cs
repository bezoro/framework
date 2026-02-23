using Bezoro.Core.Extensions;
using Bezoro.Core.Types.Exceptions;
using FluentAssertions;
using JetBrains.Annotations;
using Xunit;

namespace Bezoro.Core.Tests.Extensions;

[TestSubject(typeof(IntExtensions))]
public class IntExtensionsTests
{
	[Theory]
	[InlineData(-1,   1)]
	[InlineData(-100, 100)]
	public void Abs_WhenCalled_ShouldReturnPositiveValue_WhenValueIsNegative(int value, int expected) =>
		value.Abs().Should().Be(expected);

	[Theory]
	[InlineData(1)]
	[InlineData(100)]
	public void Abs_WhenCalled_ShouldReturnSameValue_WhenValueIsPositive(int value) =>
		value.Abs().Should().Be(value);

	[Theory]
	[InlineData(5,  0, 10, 5)]
	[InlineData(-1, 0, 10, 0)]
	[InlineData(11, 0, 10, 10)]
	public void Clamp_WhenCalled_ShouldReturnClampedValue(int value, int min, int max, int expected) =>
		value.Clamp(min, max).Should().Be(expected);

	[Theory]
	[InlineData(5,  10, 5)]
	[InlineData(15, 10, 10)]
	public void ClampMax_WhenCalled_ShouldReturnClampedValue(int value, int max, int expected) =>
		value.ClampMax(max).Should().Be(expected);

	[Theory]
	[InlineData(5,  10, 10)]
	[InlineData(15, 10, 15)]
	public void ClampMin_WhenCalled_ShouldReturnClampedValue(int value, int min, int expected) =>
		value.ClampMin(min).Should().Be(expected);

	[Theory]
	[InlineData(1)]
	[InlineData(3)]
	[InlineData(-1)]
	public void IsEven_WhenCalled_ShouldReturnFalse_WhenValueIsOdd(int value) => value.IsEven().Should().BeFalse();

	[Theory]
	[InlineData(2)]
	[InlineData(4)]
	[InlineData(0)]
	public void IsEven_WhenCalled_ShouldReturnTrue_WhenValueIsEven(int value) => value.IsEven().Should().BeTrue();

	[Theory]
	[InlineData(0)]
	[InlineData(1)]
	public void IsNegative_WhenCalled_ShouldReturnFalse_WhenValueIsZeroOrPositive(int value) =>
		value.IsNegative().Should().BeFalse();

	[Theory]
	[InlineData(-1)]
	[InlineData(-100)]
	public void IsNegative_WhenCalled_ShouldReturnTrue_WhenValueIsNegative(int value) =>
		value.IsNegative().Should().BeTrue();

	[Theory]
	[InlineData(2)]
	[InlineData(4)]
	[InlineData(0)]
	public void IsOdd_WhenCalled_ShouldReturnFalse_WhenValueIsEven(int value) => value.IsOdd().Should().BeFalse();

	[Theory]
	[InlineData(1)]
	[InlineData(3)]
	public void IsOdd_WhenCalled_ShouldReturnTrue_WhenValueIsOdd(int value) => value.IsOdd().Should().BeTrue();

	[Theory]
	[InlineData(0)]
	[InlineData(-1)]
	public void IsPositive_WhenCalled_ShouldReturnFalse_WhenValueIsZeroOrNegative(int value) =>
		value.IsPositive().Should().BeFalse();

	[Theory]
	[InlineData(1)]
	[InlineData(100)]
	public void IsPositive_WhenCalled_ShouldReturnTrue_WhenValueIsPositive(int value) =>
		value.IsPositive().Should().BeTrue();

	[Theory]
	[InlineData(1)]
	[InlineData(-1)]
	public void IsZero_WhenCalled_ShouldReturnFalse_WhenValueIsNotZero(int value) => value.IsZero().Should().BeFalse();

	[Fact]
	public void IsZero_WhenCalled_ShouldReturnTrue_WhenValueIsZero() => 0.IsZero().Should().BeTrue();

	[Theory]
	[InlineData(14, 5, 15)]
	[InlineData(11, 5, 10)]
	public void RoundToNearest_WhenCalled_ShouldReturnRoundedValue(int value, int nearest, int expected) =>
		value.RoundToNearest(nearest).Should().Be(expected);

	[Theory]
	[InlineData(-1, -1)]
	[InlineData(1,  1)]
	public void Sign_WhenCalled_ShouldReturnCorrectSign(int value, int expected) =>
		value.Sign().Should().Be(expected);

	[Theory]
	[InlineData(5,  0)]
	[InlineData(10, 5)]
	public void ThrowIfLessThan_WhenCalled_ShouldNotThrow_WhenValueIsNotLessThanMin(int value, int min) =>
		value.Invoking(v => v.ThrowIfLessThan(min)).Should().NotThrow();

	[Theory]
	[InlineData(-1, 0)]
	[InlineData(4,  5)]
	public void ThrowIfLessThan_WhenCalled_ShouldThrow_WhenValueIsLessThanMin(int value, int min) =>
		value.Invoking(v => v.ThrowIfLessThan(min)).Should().Throw<ValueTooSmallException>();

	[Theory]
	[InlineData(0, 5)]
	[InlineData(5, 10)]
	public void ThrowIfMoreThan_WhenCalled_ShouldNotThrow_WhenValueIsNotMoreThanMax(int value, int max) =>
		value.Invoking(v => v.ThrowIfMoreThan(max)).Should().NotThrow();

	[Theory]
	[InlineData(1, 0)]
	[InlineData(6, 5)]
	public void ThrowIfMoreThan_WhenCalled_ShouldThrow_WhenValueIsMoreThanMax(int value, int max) =>
		value.Invoking(v => v.ThrowIfMoreThan(max)).Should().Throw<ValueTooLargeException>();
}
