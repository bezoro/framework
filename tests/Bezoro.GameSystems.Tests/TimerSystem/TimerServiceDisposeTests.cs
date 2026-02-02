using System;
using Bezoro.GameSystems.TimerSystem.Services;
using FluentAssertions;
using JetBrains.Annotations;
using Xunit;

namespace Bezoro.GameSystems.Tests.TimerSystem;

[TestSubject(typeof(TimerService))]
public class TimerServiceDisposeTests
{
	[Fact]
	public void WhenDisposed_CreateShouldThrow()
	{
		var service = new TimerService();
		service.Dispose();

		var act = () => service.Create(TimeSpan.FromSeconds(1));

		act.Should().Throw<ObjectDisposedException>();
	}

	[Fact]
	public void WhenDisposed_StartShouldThrow()
	{
		var service = new TimerService();
		service.Dispose();

		var act = () => service.Start(new());

		act.Should().Throw<ObjectDisposedException>();
	}

	[Fact]
	public void WhenDisposedTwice_ShouldNotThrow()
	{
		var service = new TimerService();

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
		var service = new TimerService();
		service.Start(new(10));

		service.Dispose();

		service.IsRunning.Should().BeFalse();
	}
}
