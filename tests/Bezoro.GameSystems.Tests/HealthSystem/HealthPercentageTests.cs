using Bezoro.GameSystems.HealthSystem.Types;
using FluentAssertions;
using JetBrains.Annotations;
using Xunit;

namespace Bezoro.GameSystems.Tests.HealthSystem;

[TestSubject(typeof(Health))]
public class HealthPercentageTests
{
	[Fact]
	public void ShouldRoundToNearestPercent()
	{
		var health = new Health(3u, 2u);

		health.Percentage.Value.Should().Be(67);
	}

	[Fact]
	public void WhenMaxIsZero_ShouldBeZero()
	{
		var health = new Health(0u, 0u);

		health.Percentage.Value.Should().Be(0);
	}
}
