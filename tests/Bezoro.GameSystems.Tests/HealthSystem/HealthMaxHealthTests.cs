using Bezoro.Core.Types;
using Bezoro.GameSystems.HealthSystem.Types;
using FluentAssertions;
using JetBrains.Annotations;
using Xunit;

namespace Bezoro.GameSystems.Tests.HealthSystem;

[TestSubject(typeof(Health))]
public class HealthMaxHealthTests
{
	[Fact]
	public void WhenClampingMax_ShouldClampCurrent()
	{
		var health = new Health(100u, 90u);

		health = health.SetMaxHealthTo(50u, MaxValueUpdateMode.ClampCurrent);

		health.Max.Should().Be(50u);
		health.Current.Should().Be(50u);
	}

	[Fact]
	public void WhenDecreasingMaxPastZero_ShouldClampCurrentToZero()
	{
		var health = new Health(100u, 80u);

		health = health.DecreaseMaxHealthBy(200u, MaxValueUpdateMode.ClampCurrent);

		health.Max.Should().Be(0u);
		health.Current.Should().Be(0u);
	}

	[Fact]
	public void WhenIncreasingMaxBeyondUIntMax_ShouldSaturate()
	{
		uint max    = uint.MaxValue - 1u;
		var  health = new Health(max, 10u);

		health = health.IncreaseMaxHealthBy(10u, MaxValueUpdateMode.ClampCurrent);

		health.Max.Should().Be(uint.MaxValue);
		health.Current.Should().Be(10u);
	}

	[Fact]
	public void WhenIncreasingMaxWithPreservePercentage_ShouldScaleCurrent()
	{
		var health = new Health(100u, 25u);

		health = health.IncreaseMaxHealthBy(100u, MaxValueUpdateMode.PreservePercentage);

		health.Max.Should().Be(200u);
		health.Current.Should().Be(50u);
	}

	[Fact]
	public void WhenPreservingPercentage_ShouldRoundAwayFromZero()
	{
		var health = new Health(2u, 1u);

		health = health.SetMaxHealthTo(1u, MaxValueUpdateMode.PreservePercentage);

		health.Current.Should().Be(1u);
	}

	[Fact]
	public void WhenPreservingPercentage_ShouldScaleCurrent()
	{
		var health = new Health(100u, 25u);

		health = health.SetMaxHealthTo(200u, MaxValueUpdateMode.PreservePercentage);

		health.Max.Should().Be(200u);
		health.Current.Should().Be(50u);
	}

	[Fact]
	public void WhenPreservingPercentageWithZeroMax_ShouldClampCurrent()
	{
		var health = new Health(0u, 0u);

		health = health.SetMaxHealthTo(10u, MaxValueUpdateMode.PreservePercentage);

		health.Max.Should().Be(10u);
		health.Current.Should().Be(0u);
	}
}
