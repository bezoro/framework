using Bezoro.GameSystems.HealthSystem.Types;
using FluentAssertions;
using JetBrains.Annotations;
using Xunit;

namespace Bezoro.GameSystems.Tests.HealthSystem;

[TestSubject(typeof(Health))]
public class HealthConstructorTests
{
	[Fact]
	public void WhenCurrentExceedsMax_ShouldClampAndAddOverflowToExcess()
	{
		var health = new Health(100u, 120u, 5u);

		health.Max.Should().Be(100u);
		health.Current.Should().Be(100u);
		health.Excess.Should().Be(25u);
	}

	[Fact]
	public void WhenMaxIsZero_ShouldClampCurrentAndCarryOverflowToExcess()
	{
		var health = new Health(0u, 25u, 10u);

		health.Max.Should().Be(0u);
		health.Current.Should().Be(0u);
		health.Excess.Should().Be(35u);
	}
}
