using Bezoro.GameSystems.ActivationSystem.Services;
using Bezoro.GameSystems.ActivationSystem.Types;
using FluentAssertions;
using JetBrains.Annotations;
using Xunit;

namespace Bezoro.GameSystems.Tests.ActivationSystem;

[TestSubject(typeof(ActivationService))]
public class ActivationServiceStartTests
{
	[Fact]
	public void WhenNotStarted_IsRunningShouldBeFalse()
	{
		using var service = new ActivationService();

		service.IsRunning.Should().BeFalse();
	}

	[Fact]
	public void WhenStarted_IsRunningShouldBeTrue()
	{
		using var service = new ActivationService();

		service.Start(new ActivationConfig(iterationDelayMs: 10));

		service.IsRunning.Should().BeTrue();
	}

	[Fact]
	public void WhenStopped_IsRunningShouldBeFalse()
	{
		using var service = new ActivationService();
		service.Start(new ActivationConfig(iterationDelayMs: 10));

		service.Stop();

		service.IsRunning.Should().BeFalse();
	}

	[Fact]
	public void WhenStartedTwice_ShouldNotThrow()
	{
		using var service = new ActivationService();

		var act = () =>
		{
			service.Start(new ActivationConfig(iterationDelayMs: 10));
			service.Start(new ActivationConfig(iterationDelayMs: 10));
		};

		act.Should().NotThrow();
	}

	[Fact]
	public void WhenStoppedWithoutStarting_ShouldNotThrow()
	{
		using var service = new ActivationService();

		var act = () => service.Stop();

		act.Should().NotThrow();
	}

	[Fact]
	public void WhenNoItems_IsCompleteShouldBeTrue()
	{
		using var service = new ActivationService();

		service.IsComplete.Should().BeTrue();
	}

	[Fact]
	public void WhenItemsRegistered_IsCompleteShouldBeFalse()
	{
		using var service = new ActivationService();
		service.Register(() => { });

		service.IsComplete.Should().BeFalse();
	}
}
