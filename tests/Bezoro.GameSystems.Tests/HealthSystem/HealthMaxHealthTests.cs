using Bezoro.GameSystems.HealthSystem.Types;
using FluentAssertions;
using JetBrains.Annotations;
using Xunit;

namespace Bezoro.GameSystems.Tests.HealthSystem;

[TestSubject(typeof(Health))]
public class HealthMaxHealthTests
{
	[Fact]
	public void WhenClampingMax_ShouldClampCurrentAndPreserveExcess()
	{
		var health = new Health(100u, 90u, 20u);

		health.SetMaxHealthTo(50u, MaxHealthUpdateMode.ClampCurrent);

		health.Max.Should().Be(50u);
		health.Current.Should().Be(50u);
		health.Excess.Should().Be(20u);
	}

	[Fact]
	public void WhenDecreasingMaxPastZero_ShouldClampCurrentToZero()
	{
		var health = new Health(100u, 80u);

		health.DecreaseMaxHealthBy(200u);

		health.Max.Should().Be(0u);
		health.Current.Should().Be(0u);
	}

	[Fact]
	public void WhenIncreasingMaxBeyondUIntMax_ShouldSaturate()
	{
		uint max    = uint.MaxValue - 1u;
		var  health = new Health(max, 10u);

		health.IncreaseMaxHealthBy(10u);

		health.Max.Should().Be(uint.MaxValue);
		health.Current.Should().Be(10u);
	}

	[Fact]
	public void WhenPreservingPercentage_ShouldRoundAwayFromZero()
	{
		var health = new Health(2u, 1u);

		health.SetMaxHealthTo(1u, MaxHealthUpdateMode.PreservePercentage);

		health.Current.Should().Be(1u);
	}

	[Fact]
	public void WhenPreservingPercentage_ShouldScaleCurrent()
	{
		var health = new Health(100u, 25u);

		health.SetMaxHealthTo(200u, MaxHealthUpdateMode.PreservePercentage);

		health.Max.Should().Be(200u);
		health.Current.Should().Be(50u);
	}

	[Fact]
	public void WhenPreservingPercentageWithZeroMax_ShouldClampCurrent()
	{
		var health = new Health(0u, 0u);

		health.SetMaxHealthTo(10u, MaxHealthUpdateMode.PreservePercentage);

		health.Max.Should().Be(10u);
		health.Current.Should().Be(0u);
	}
}
