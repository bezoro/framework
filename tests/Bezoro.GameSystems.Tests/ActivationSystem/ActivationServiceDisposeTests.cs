using System;
using Bezoro.GameSystems.ActivationSystem.Services;
using FluentAssertions;
using JetBrains.Annotations;
using Xunit;

namespace Bezoro.GameSystems.Tests.ActivationSystem;

[TestSubject(typeof(ActivationService))]
public class ActivationServiceDisposeTests
{
	[Fact]
	public void WhenDisposed_RegisterShouldThrow()
	{
		var service = new ActivationService();
		service.Dispose();

		var act = () => service.Register(() => { });

		act.Should().Throw<ObjectDisposedException>();
	}

	[Fact]
	public void WhenDisposed_StartShouldThrow()
	{
		var service = new ActivationService();
		service.Dispose();

		var act = () => service.Start(new());

		act.Should().Throw<ObjectDisposedException>();
	}

	[Fact]
	public void WhenDisposedTwice_ShouldNotThrow()
	{
		var service = new ActivationService();

		var act = () =>
		{
			service.Dispose();
			service.Dispose();
		};

		act.Should().NotThrow();
	}

	[Fact]
	public void WhenDisposedWhileRunning_ShouldStopLoop()
	{
		var service = new ActivationService();
		service.Start(new(iterationDelayMs: 10));

		service.Dispose();

		service.IsRunning.Should().BeFalse();
	}
}
