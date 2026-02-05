using Bezoro.GameSystems.HealthSystem.Types;
using FluentAssertions;
using JetBrains.Annotations;
using Xunit;

namespace Bezoro.GameSystems.Tests.HealthSystem;

[TestSubject(typeof(Health))]
public class HealthStateTests
{
	[Fact]
	public void IsEmpty_WhenCurrentIsZero_ShouldBeTrue()
	{
		var health = new Health(100u, 0u);

		health.IsEmpty.Should().BeTrue();
	}

	[Fact]
	public void IsEmpty_WhenCurrentIsAboveZero_ShouldBeFalse()
	{
		var health = new Health(100u, 1u);

		health.IsEmpty.Should().BeFalse();
	}

	[Fact]
	public void IsFull_WhenCurrentEqualsMax_ShouldBeTrue()
	{
		var health = new Health(100u, 100u);

		health.IsFull.Should().BeTrue();
	}

	[Fact]
	public void IsFull_WhenCurrentBelowMax_ShouldBeFalse()
	{
		var health = new Health(100u, 99u);

		health.IsFull.Should().BeFalse();
	}
}
