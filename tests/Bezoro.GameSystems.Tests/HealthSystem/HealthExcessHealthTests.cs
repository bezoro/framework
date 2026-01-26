using Bezoro.GameSystems.HealthSystem.Types;
using FluentAssertions;
using JetBrains.Annotations;
using Xunit;

namespace Bezoro.GameSystems.Tests.HealthSystem;

[TestSubject(typeof(Health))]
public class HealthExcessHealthTests
{
	[Fact]
	public void WhenClearingAndSetting_ShouldMatchExpectedValues()
	{
		var health = new Health(100u, 0u, 50u);

		health.ClearExcessHealth();
		health.Excess.Should().Be(0u);

		health.SetExcessHealthTo(15u);
		health.Excess.Should().Be(15u);
	}

	[Fact]
	public void WhenDecreasingBeyondExcess_ShouldClampToZero()
	{
		var health = new Health(100u, 50u, 10u);

		health.DecreaseExcessHealthBy(25u);

		health.Excess.Should().Be(0u);
	}

	[Fact]
	public void WhenIncreasingBeyondUIntMax_ShouldSaturate()
	{
		var health = new Health(100u, 0u, uint.MaxValue - 1u);

		health.IncreaseExcessHealthBy(10u);

		health.Excess.Should().Be(uint.MaxValue);
	}
}
