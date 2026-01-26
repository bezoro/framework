using Bezoro.GameSystems.HealthSystem.Types;
using FluentAssertions;
using JetBrains.Annotations;
using Xunit;

namespace Bezoro.GameSystems.Tests.HealthSystem;

[TestSubject(typeof(Health))]
public class HealthCurrentHealthTests
{
	[Fact]
	public void WhenDecreasing_ShouldConsumeExcessFirst()
	{
		var health = new Health(100u, 50u, 20u);

		health.DecreaseCurrentHealthBy(10u);

		health.Current.Should().Be(50u);
		health.Excess.Should().Be(10u);
	}

	[Fact]
	public void WhenDecreasingBeyondTotal_ShouldSetCurrentAndExcessToZero()
	{
		var health = new Health(100u, 10u, 5u);

		health.DecreaseCurrentHealthBy(20u);

		health.Current.Should().Be(0u);
		health.Excess.Should().Be(0u);
	}

	[Fact]
	public void WhenDecreasingPastExcess_ShouldReduceCurrent()
	{
		var health = new Health(100u, 50u, 20u);

		health.DecreaseCurrentHealthBy(25u);

		health.Current.Should().Be(45u);
		health.Excess.Should().Be(0u);
	}

	[Fact]
	public void WhenDepletingAndRestoring_ShouldMatchExpectedValues()
	{
		var health = new Health(75u, 30u);

		health.DepleteCurrentHealth();
		health.Current.Should().Be(0u);

		health.FullyRestoreCurrentHealth();
		health.Current.Should().Be(75u);
	}

	[Fact]
	public void WhenIncreasingPastMax_ShouldFillCurrentAndAddExcess()
	{
		var health = new Health(100u, 90u);

		health.IncreaseCurrentHealthBy(20u);

		health.Current.Should().Be(100u);
		health.Excess.Should().Be(10u);
	}

	[Fact]
	public void WhenIncreasingUnderMax_ShouldIncreaseCurrentOnly()
	{
		var health = new Health(100u, 40u);

		health.IncreaseCurrentHealthBy(10u);

		health.Current.Should().Be(50u);
		health.Excess.Should().Be(0u);
	}

	[Fact]
	public void WhenRestoringPastMax_ShouldNotCreateExcess()
	{
		var health = new Health(100u, 90u, 5u);

		health.RestoreCurrentHealthBy(20u);

		health.Current.Should().Be(100u);
		health.Excess.Should().Be(5u);
	}

	[Fact]
	public void WhenSettingCurrent_ShouldClampToMax()
	{
		var health = new Health(100u, 20u);

		health.SetCurrentHealthTo(150u);

		health.Current.Should().Be(100u);
	}

	[Fact]
	public void WhenSumExceedsUIntMax_ShouldNotLoseOverflow()
	{
		uint max    = uint.MaxValue - 1u;
		var  health = new Health(max, max);

		health.IncreaseCurrentHealthBy(10u);

		health.Current.Should().Be(max);
		health.Excess.Should().Be(10u);
	}
}
