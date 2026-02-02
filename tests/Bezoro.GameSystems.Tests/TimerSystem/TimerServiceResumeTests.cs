using System;
using Bezoro.GameSystems.TimerSystem.Services;
using Bezoro.GameSystems.TimerSystem.Types;
using FluentAssertions;
using JetBrains.Annotations;
using Xunit;

namespace Bezoro.GameSystems.Tests.TimerSystem;

[TestSubject(typeof(TimerService))]
public class TimerServiceResumeTests
{
	[Fact]
	public void WhenCancelledTimer_ShouldReturnFalse()
	{
		using var service = new TimerService();
		var       handle  = service.Create(TimeSpan.FromSeconds(5));
		service.Cancel(handle);

		bool result = service.Resume(handle);

		result.Should().BeFalse();
	}

	[Fact]
	public void WhenInvalidHandle_ShouldReturnFalse()
	{
		using var service = new TimerService();

		bool result = service.Resume(TimerHandle.None);

		result.Should().BeFalse();
	}

	[Fact]
	public void WhenPauseAndResume_ShouldPreserveProgress()
	{
		using var service = new TimerService();
		var       handle  = service.Create(TimeSpan.FromSeconds(10));

		service.TryGetInfo(handle, out var infoBefore).Should().BeTrue();
		service.Pause(handle);
		service.Resume(handle);
		service.TryGetInfo(handle, out var infoAfter).Should().BeTrue();

		// After pause/resume the timer should still have similar progress
		infoAfter.State.Should().Be(TimerState.Running);
		infoAfter.Duration.Should().Be(infoBefore.Duration);
	}

	[Fact]
	public void WhenPausedTimer_ShouldReturnTrue()
	{
		using var service = new TimerService();
		var       handle  = service.Create(TimeSpan.FromSeconds(5));
		service.Pause(handle);

		bool result = service.Resume(handle);

		result.Should().BeTrue();
	}

	[Fact]
	public void WhenPausedTimer_ShouldTransitionToRunning()
	{
		using var service = new TimerService();
		var       handle  = service.Create(TimeSpan.FromSeconds(5));
		service.Pause(handle);

		service.Resume(handle);

		service.TryGetInfo(handle, out var info).Should().BeTrue();
		info.State.Should().Be(TimerState.Running);
	}

	[Fact]
	public void WhenRunningTimer_ShouldReturnFalse()
	{
		using var service = new TimerService();
		var       handle  = service.Create(TimeSpan.FromSeconds(5));

		bool result = service.Resume(handle);

		result.Should().BeFalse();
	}
}
