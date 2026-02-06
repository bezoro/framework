using Bezoro.GameSystems.HealthSystem.Types;
using FluentAssertions;
using JetBrains.Annotations;
using Xunit;

namespace Bezoro.GameSystems.Tests.HealthSystem;

[TestSubject(typeof(Health))]
public class HealthCurrentHealthTests
{
	[Fact]
	public void WhenDecreasing_ShouldReduceCurrent()
	{
		var health = new Health(100u, 50u);

		health = health.DecreaseCurrentHealthBy(10u);

		health.Current.Should().Be(40u);
	}

	[Fact]
	public void WhenDecreasingBeyondTotal_ShouldSetCurrentToZero()
	{
		var health = new Health(100u, 10u);

		health = health.DecreaseCurrentHealthBy(20u);

		health.Current.Should().Be(0u);
	}

	[Fact]
	public void WhenDecreasingToExactZero_ShouldClampToZero()
	{
		var health = new Health(100u, 25u);

		health = health.DecreaseCurrentHealthBy(25u);

		health.Current.Should().Be(0u);
	}

	[Fact]
	public void WhenDepletingAndRestoring_ShouldMatchExpectedValues()
	{
		var health = new Health(75u, 30u);

		health = health.DepleteCurrentHealth();
		health.Current.Should().Be(0u);

		health = health.FullyRestoreCurrentHealth();
		health.Current.Should().Be(75u);
	}

	[Fact]
	public void WhenRestoreSumExceedsUIntMax_ShouldClampToMax()
	{
		uint max    = uint.MaxValue - 1u;
		var  health = new Health(max, max);

		health = health.RestoreCurrentHealthBy(10u);

		health.Current.Should().Be(max);
	}

	[Fact]
	public void WhenRestoringPastMax_ShouldClampToMax()
	{
		var health = new Health(100u, 90u);

		health = health.RestoreCurrentHealthBy(20u);

		health.Current.Should().Be(100u);
	}

	[Fact]
	public void WhenRestoringUnderMax_ShouldIncreaseCurrentOnly()
	{
		var health = new Health(100u, 40u);

		health = health.RestoreCurrentHealthBy(10u);

		health.Current.Should().Be(50u);
	}

	[Fact]
	public void WhenSettingCurrent_ShouldClampToMax()
	{
		var health = new Health(100u, 20u);

		health = health.SetCurrentHealthTo(150u);

		health.Current.Should().Be(100u);
	}
}
