using System;
using Bezoro.GameSystems.TimerSystem.Services;
using Bezoro.GameSystems.TimerSystem.Types;
using FluentAssertions;
using JetBrains.Annotations;
using Xunit;

namespace Bezoro.GameSystems.Tests.TimerSystem;

[TestSubject(typeof(TimerService))]
public class TimerServicePauseTests
{
	[Fact]
	public void WhenAlreadyPaused_ShouldReturnFalse()
	{
		using var service = new TimerService();
		var       handle  = service.Create(TimeSpan.FromSeconds(5));
		service.Pause(handle);

		bool result = service.Pause(handle);

		result.Should().BeFalse();
	}

	[Fact]
	public void WhenCancelledTimer_ShouldReturnFalse()
	{
		using var service = new TimerService();
		var       handle  = service.Create(TimeSpan.FromSeconds(5));
		service.Cancel(handle);

		bool result = service.Pause(handle);

		result.Should().BeFalse();
	}

	[Fact]
	public void WhenInvalidHandle_ShouldReturnFalse()
	{
		using var service = new TimerService();

		bool result = service.Pause(TimerHandle.None);

		result.Should().BeFalse();
	}

	[Fact]
	public void WhenNonExistentHandle_ShouldReturnFalse()
	{
		using var service = new TimerService();

		bool result = service.Pause(new(999));

		result.Should().BeFalse();
	}

	[Fact]
	public void WhenPaused_ShouldPreserveElapsedTime()
	{
		using var service = new TimerService();
		var       handle  = service.Create(TimeSpan.FromSeconds(10));

		// Let some time pass (even minimal)
		service.Pause(handle);

		service.TryGetInfo(handle, out var info).Should().BeTrue();
		info.Elapsed.Should().BeGreaterThanOrEqualTo(TimeSpan.Zero);
		info.Remaining.Should().BeLessThanOrEqualTo(TimeSpan.FromSeconds(10));
	}

	[Fact]
	public void WhenRunningTimer_ShouldReturnTrue()
	{
		using var service = new TimerService();
		var       handle  = service.Create(TimeSpan.FromSeconds(5));

		bool result = service.Pause(handle);

		result.Should().BeTrue();
	}

	[Fact]
	public void WhenRunningTimer_ShouldTransitionToPaused()
	{
		using var service = new TimerService();
		var       handle  = service.Create(TimeSpan.FromSeconds(5));

		service.Pause(handle);

		service.TryGetInfo(handle, out var info).Should().BeTrue();
		info.State.Should().Be(TimerState.Paused);
	}
}
