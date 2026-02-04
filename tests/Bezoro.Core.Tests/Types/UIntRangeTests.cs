using Bezoro.Core.Types;
using FluentAssertions;
using JetBrains.Annotations;
using Xunit;

namespace Bezoro.Core.Tests.Types;

[TestSubject(typeof(UIntRange))]
public class UIntRangeConstructorTests
{
	[Fact]
	public void WhenCurrentExceedsMax_ShouldClampToMax()
	{
		var range = new UIntRange(100u, 150u);

		range.Max.Should().Be(100u);
		range.Current.Should().Be(100u);
		range.Min.Should().Be(0u);
	}

	[Fact]
	public void WhenUsingSingleValue_ShouldSetCurrentMaxAndDefaultMin()
	{
		var range = new UIntRange(42u);

		range.Max.Should().Be(42u);
		range.Current.Should().Be(42u);
		range.Min.Should().Be(0u);
	}

	[Fact]
	public void WhenMinIsProvided_ShouldClampCurrentToMin()
	{
		var range = new UIntRange(max: 10u, current: 3u, min: 5u);

		range.Min.Should().Be(5u);
		range.Max.Should().Be(10u);
		range.Current.Should().Be(5u);
	}

	[Fact]
	public void WhenMaxIsZero_ShouldClampCurrentToZero()
	{
		var range = new UIntRange(0u, 25u);

		range.Max.Should().Be(0u);
		range.Current.Should().Be(0u);
	}
}

[TestSubject(typeof(UIntRange))]
public class UIntRangeCurrentTests
{
	[Fact]
	public void WhenDecreasingBeyondCurrent_ShouldClampToZero()
	{
		var range = new UIntRange(100u, 10u);

		range = range.Decrease(20u);

		range.Current.Should().Be(0u);
	}

	[Fact]
	public void WhenDecreasingBeyondMin_ShouldClampToMin()
	{
		var range = new UIntRange(max: 10u, current: 6u, min: 5u);

		range = range.Decrease(2u);

		range.Current.Should().Be(5u);
	}

	[Fact]
	public void WhenRestoringPastMax_ShouldClampToMax()
	{
		var range = new UIntRange(100u, 90u);

		range = range.Restore(20u);

		range.Current.Should().Be(100u);
	}
}

[TestSubject(typeof(UIntRange))]
public class UIntRangeMaxUpdateTests
{
	[Fact]
	public void DecreaseMax_WhenBeyondZero_ShouldClampCurrentAndMaxToZero()
	{
		var range = new UIntRange(100u, 80u);

		range = range.DecreaseMax(200u, MaxValueUpdateMode.ClampCurrent);

		range.Max.Should().Be(0u);
		range.Current.Should().Be(0u);
	}

	[Fact]
	public void DecreaseMax_WhenBeyondMin_ShouldClampCurrentAndMaxToMin()
	{
		var range = new UIntRange(max: 10u, current: 8u, min: 5u);

		range = range.DecreaseMax(10u, MaxValueUpdateMode.ClampCurrent);

		range.Max.Should().Be(5u);
		range.Current.Should().Be(5u);
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
	public void SetMaxPreservePercentage_ShouldScaleCurrent()
	{
		var range = new UIntRange(100u, 25u);

		range = range.SetMax(200u, MaxValueUpdateMode.PreservePercentage);

		range.Max.Should().Be(200u);
		range.Current.Should().Be(50u);
	}

	[Fact]
	public void SetMaxPreservePercentage_WithZeroMax_ShouldClampCurrent()
	{
		var range = new UIntRange(0u, 0u);

		range = range.SetMax(10u, MaxValueUpdateMode.PreservePercentage);

		range.Max.Should().Be(10u);
		range.Current.Should().Be(0u);
	}
}
