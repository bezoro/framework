using Bezoro.GameSystems.HealthSystem.Types;
using FluentAssertions;
using JetBrains.Annotations;
using Xunit;

namespace Bezoro.GameSystems.Tests.HealthSystem;

[TestSubject(typeof(Health))]
public class HealthConstructorTests
{
	[Fact]
	public void WhenCurrentExceedsMax_ShouldClampToMax()
	{
		var health = new Health(100u, 120u);

		health.Max.Should().Be(100u);
		health.Current.Should().Be(100u);
	}

	[Fact]
	public void WhenMaxIsZero_ShouldClampCurrentToZero()
	{
		var health = new Health(0u, 25u);

		health.Max.Should().Be(0u);
		health.Current.Should().Be(0u);
	}
}
