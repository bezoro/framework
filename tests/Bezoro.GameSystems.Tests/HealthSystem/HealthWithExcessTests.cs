using Bezoro.GameSystems.HealthSystem.Types;
using FluentAssertions;
using JetBrains.Annotations;
using Xunit;

namespace Bezoro.GameSystems.Tests.HealthSystem;

[TestSubject(typeof(HealthWithExcess))]
public class HealthWithExcessTests
{
	[Fact]
	public void Constructor_WhenCurrentExceedsMax_ShouldMoveOverflowToExcess()
	{
		var health = new HealthWithExcess(100u, 120u, 5u, 50u);

		health.Max.Should().Be(100u);
		health.Current.Should().Be(100u);
		health.ExcessCurrent.Should().Be(25u);
	}

	[Fact]
	public void DecreaseCurrentHealthBy_WhenExcessAvailable_ShouldNotConsumeExcess()
	{
		var health = new HealthWithExcess(100u, 50u, 20u, 50u);

		health = health.DecreaseCurrentHealthBy(10u);

		health.Current.Should().Be(40u);
		health.ExcessCurrent.Should().Be(20u);
	}

	[Fact]
	public void DecreaseHealthBy_WhenBeyondTotal_ShouldSetCurrentAndExcessToZero()
	{
		var health = new HealthWithExcess(100u, 10u, 5u, 50u);

		health = health.DecreaseHealthBy(20u);

		health.Current.Should().Be(0u);
		health.ExcessCurrent.Should().Be(0u);
	}

	[Fact]
	public void DecreaseHealthBy_WhenExcessAvailable_ShouldConsumeExcessFirst()
	{
		var health = new HealthWithExcess(100u, 50u, 20u, 50u);

		health = health.DecreaseHealthBy(10u);

		health.Current.Should().Be(50u);
		health.ExcessCurrent.Should().Be(10u);
	}

	[Fact]
	public void IncreaseCurrentHealthBy_WhenPastMax_ShouldOverflowIntoExcess()
	{
		var health = new HealthWithExcess(100u, 90u, 0u, 50u);

		health = health.IncreaseCurrentHealthBy(20u);

		health.Current.Should().Be(100u);
		health.ExcessCurrent.Should().Be(10u);
	}

	[Fact]
	public void RestoreCurrentHealthBy_WhenPastMax_ShouldNotChangeExcess()
	{
		var health = new HealthWithExcess(100u, 90u, 5u, 50u);

		health = health.RestoreCurrentHealthBy(20u);

		health.Current.Should().Be(100u);
		health.ExcessCurrent.Should().Be(5u);
	}
}
