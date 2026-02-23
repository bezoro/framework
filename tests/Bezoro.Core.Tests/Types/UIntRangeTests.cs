using System;
using Bezoro.Core.Types;
using FluentAssertions;
using JetBrains.Annotations;
using Xunit;

namespace Bezoro.Core.Tests.Types;

[TestSubject(typeof(UIntRange))]
public class UIntRangeConstructorTests
{
	[Fact]
	public void Constructor_WhenCurrentBelowMin_ShouldClampCurrentToMin()
	{
		var range = new UIntRange(10u, 3u, 5u);

		range.Min.Should().Be(5u);
		range.Max.Should().Be(10u);
		range.Current.Should().Be(5u);
	}

	[Fact]
	public void Constructor_WhenCurrentExceedsMax_ShouldClampCurrentToMax()
	{
		var range = new UIntRange(100u, 150u);

		range.Max.Should().Be(100u);
		range.Current.Should().Be(100u);
		range.Min.Should().Be(0u);
	}

	[Fact]
	public void Constructor_WhenMaxBelowMin_ShouldClampMaxAndCurrentToMin()
	{
		var range = new UIntRange(3u, 2u, 5u);

		range.Min.Should().Be(5u);
		range.Max.Should().Be(5u);
		range.Current.Should().Be(5u);
	}

	[Fact]
	public void Constructor_WhenMaxIsZero_ShouldClampCurrentToZero()
	{
		var range = new UIntRange(0u, 25u);

		range.Max.Should().Be(0u);
		range.Current.Should().Be(0u);
	}

	[Fact]
	public void Constructor_WhenSingleValueProvided_ShouldSetCurrentAndMaxAndDefaultMin()
	{
		var range = new UIntRange(42u);

		range.Max.Should().Be(42u);
		range.Current.Should().Be(42u);
		range.Min.Should().Be(0u);
	}
}

[TestSubject(typeof(UIntRange))]
public class UIntRangeCurrentTests
{
	[Fact]
	public void AddToCurrent_WhenAddingLargeValue_ShouldClampToMaxWithoutOverflow()
	{
		var range = new UIntRange(uint.MaxValue, uint.MaxValue - 5u);

		range = range.AddToCurrent(50u);

		range.Current.Should().Be(uint.MaxValue);
		range.Max.Should().Be(uint.MaxValue);
	}

	[Fact]
	public void AddToCurrent_WhenIncreasingPastMax_ShouldClampToMax()
	{
		var range = new UIntRange(100u, 90u);

		range = range.AddToCurrent(20u);

		range.Current.Should().Be(100u);
	}

	[Fact]
	public void AddToCurrent_WhenValueIsZero_ShouldReturnUnchangedRange()
	{
		var range = new UIntRange(10u, 5u, 2u);

		range.AddToCurrent(0u).Should().Be(range);
	}

	[Fact]
	public void AddToCurrent_WhenWithinMax_ShouldIncreaseCurrent()
	{
		var range = new UIntRange(100u, 40u, 10u);

		range = range.AddToCurrent(15u);

		range.Current.Should().Be(55u);
		range.Max.Should().Be(100u);
		range.Min.Should().Be(10u);
	}

	[Fact]
	public void SubtractFromCurrent_WhenCurrentAtMin_ShouldReturnUnchangedRange()
	{
		var range = new UIntRange(10u, 5u, 5u);

		range.SubtractFromCurrent(1u).Should().Be(range);
	}

	[Fact]
	public void SubtractFromCurrent_WhenDecreasingBeyondCurrent_ShouldClampToZero()
	{
		var range = new UIntRange(100u, 10u);

		range = range.SubtractFromCurrent(20u);

		range.Current.Should().Be(0u);
	}

	[Fact]
	public void SubtractFromCurrent_WhenDecreasingBeyondMin_ShouldClampToMin()
	{
		var range = new UIntRange(10u, 6u, 5u);

		range = range.SubtractFromCurrent(2u);

		range.Current.Should().Be(5u);
	}

	[Fact]
	public void SubtractFromCurrent_WhenValueIsZero_ShouldReturnUnchangedRange()
	{
		var range = new UIntRange(10u, 6u, 2u);

		range.SubtractFromCurrent(0u).Should().Be(range);
	}

	[Fact]
	public void SubtractFromCurrent_WhenWithinRange_ShouldDecreaseCurrent()
	{
		var range = new UIntRange(10u, 9u, 2u);

		range = range.SubtractFromCurrent(3u);

		range.Current.Should().Be(6u);
		range.Max.Should().Be(10u);
		range.Min.Should().Be(2u);
	}
}

[TestSubject(typeof(UIntRange))]
public class UIntRangeCurrentUpdateTests
{
	[Fact]
	public void MaximizeCurrent_WhenCalled_ShouldSetCurrentToMax()
	{
		var range = new UIntRange(10u, 3u, 2u);

		range = range.MaximizeCurrent();

		range.Current.Should().Be(10u);
		range.Max.Should().Be(10u);
		range.Min.Should().Be(2u);
	}

	[Theory]
	[InlineData(0u,  2u)]
	[InlineData(7u,  7u)]
	[InlineData(12u, 10u)]
	public void SetCurrent_WhenValueOutsideRange_ShouldClampToBounds(uint value, uint expected)
	{
		var range = new UIntRange(10u, 5u, 2u);

		range = range.SetCurrent(value);

		range.Current.Should().Be(expected);
		range.Max.Should().Be(10u);
		range.Min.Should().Be(2u);
	}

	[Fact]
	public void SetCurrentToMinimum_WhenCalled_ShouldSetCurrentToMin()
	{
		var range = new UIntRange(10u, 7u, 2u);

		range = range.SetCurrentToMinimum();

		range.Current.Should().Be(2u);
		range.Max.Should().Be(10u);
		range.Min.Should().Be(2u);
	}
}

[TestSubject(typeof(UIntRange))]
public class UIntRangeMaxUpdateTests
{
	[Fact]
	public void DecreaseMax_WhenBeyondMin_ShouldClampCurrentAndMaxToMin()
	{
		var range = new UIntRange(10u, 8u, 5u);

		range = range.DecreaseMax(10u, MaxValueUpdateMode.ClampCurrent);

		range.Max.Should().Be(5u);
		range.Current.Should().Be(5u);
	}

	[Fact]
	public void DecreaseMax_WhenBeyondZero_ShouldClampCurrentAndMaxToZero()
	{
		var range = new UIntRange(100u, 80u);

		range = range.DecreaseMax(200u, MaxValueUpdateMode.ClampCurrent);

		range.Max.Should().Be(0u);
		range.Current.Should().Be(0u);
	}

	[Fact]
	public void DecreaseMax_WhenPreservePercentage_ShouldScaleCurrentAndRoundAwayFromZero()
	{
		var range = new UIntRange(100u, 25u);

		range = range.DecreaseMax(50u, MaxValueUpdateMode.PreservePercentage);

		range.Max.Should().Be(50u);
		range.Current.Should().Be(13u);
	}

	[Fact]
	public void DecreaseMax_WhenValueIsZero_ShouldReturnUnchangedRange()
	{
		var range = new UIntRange(10u, 8u, 5u);

		range.DecreaseMax(0u, MaxValueUpdateMode.ClampCurrent).Should().Be(range);
	}

	[Fact]
	public void IncreaseMax_WhenBeyondUIntMax_ShouldSaturate()
	{
		var range = new UIntRange(uint.MaxValue - 1u, 10u);

		range = range.IncreaseMax(10u, MaxValueUpdateMode.ClampCurrent);

		range.Max.Should().Be(uint.MaxValue);
		range.Current.Should().Be(10u);
	}

	[Fact]
	public void IncreaseMax_WhenClampCurrent_ShouldKeepCurrentWithinRange()
	{
		var range = new UIntRange(10u, 7u, 2u);

		range = range.IncreaseMax(5u, MaxValueUpdateMode.ClampCurrent);

		range.Max.Should().Be(15u);
		range.Current.Should().Be(7u);
		range.Min.Should().Be(2u);
	}

	[Fact]
	public void IncreaseMax_WhenPreservePercentage_ShouldScaleCurrent()
	{
		var range = new UIntRange(20u, 5u);

		range = range.IncreaseMax(20u, MaxValueUpdateMode.PreservePercentage);

		range.Max.Should().Be(40u);
		range.Current.Should().Be(10u);
	}

	[Fact]
	public void IncreaseMax_WhenValueIsZero_ShouldReturnUnchangedRange()
	{
		var range = new UIntRange(10u, 6u, 2u);

		range.IncreaseMax(0u, MaxValueUpdateMode.ClampCurrent).Should().Be(range);
	}

	[Fact]
	public void SetMax_WhenClampCurrentAndNewMaxBelowCurrent_ShouldClampCurrent()
	{
		var range = new UIntRange(100u, 80u, 10u);

		range = range.SetMax(50u, MaxValueUpdateMode.ClampCurrent);

		range.Max.Should().Be(50u);
		range.Current.Should().Be(50u);
		range.Min.Should().Be(10u);
	}

	[Fact]
	public void SetMax_WhenModeIsInvalid_ShouldThrow()
	{
		var range = new UIntRange(10u, 5u);

		range.Invoking(r => r.SetMax(10u, (MaxValueUpdateMode)99))
			 .Should()
			 .Throw<ArgumentOutOfRangeException>()
			 .WithParameterName("mode");
	}

	[Fact]
	public void SetMax_WhenOldMaxIsZeroAndPreservePercentage_ShouldKeepCurrentAtZero()
	{
		var range = new UIntRange(0u, 0u);

		range = range.SetMax(10u, MaxValueUpdateMode.PreservePercentage);

		range.Max.Should().Be(10u);
		range.Current.Should().Be(0u);
	}

	[Fact]
	public void SetMax_WhenPreservePercentage_ShouldRoundAwayFromZeroAtMidpoint()
	{
		var range = new UIntRange(4u, 1u);

		range = range.SetMax(2u, MaxValueUpdateMode.PreservePercentage);

		range.Max.Should().Be(2u);
		range.Current.Should().Be(1u);
	}

	[Fact]
	public void SetMax_WhenPreservePercentage_ShouldScaleCurrent()
	{
		var range = new UIntRange(100u, 25u);

		range = range.SetMax(200u, MaxValueUpdateMode.PreservePercentage);

		range.Max.Should().Be(200u);
		range.Current.Should().Be(50u);
	}

	[Fact]
	public void SetMax_WhenValueBelowMinAndPreservePercentage_ShouldClampCurrentToMin()
	{
		var range = new UIntRange(100u, 20u, 10u);

		range = range.SetMax(5u, MaxValueUpdateMode.PreservePercentage);

		range.Max.Should().Be(10u);
		range.Current.Should().Be(10u);
		range.Min.Should().Be(10u);
	}
}

[TestSubject(typeof(UIntRange))]
public class UIntRangePercentageTests
{
	[Fact]
	public void Percentage_WhenCurrentIsQuarterOfMax_ShouldBeTwentyFive()
	{
		var range = new UIntRange(4u, 1u);

		range.Percentage.Value.Should().Be(25);
	}

	[Fact]
	public void Percentage_WhenMaxIsZero_ShouldBeZero()
	{
		var range = new UIntRange(0u, 0u);

		range.Percentage.Value.Should().Be(0);
	}
}
