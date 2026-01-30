using System;
using Bezoro.GameSystems.TimerSystem.Services;
using Bezoro.GameSystems.TimerSystem.Types;
using FluentAssertions;
using JetBrains.Annotations;
using Xunit;

namespace Bezoro.GameSystems.Tests.TimerSystem;

[TestSubject(typeof(TimerService))]
public class TimerServiceCancelTests
{
	[Fact]
	public void WhenRunningTimer_ShouldReturnTrue()
	{
		using var service = new TimerService();
		var       handle  = service.Create(TimeSpan.FromSeconds(5));

		var result = service.Cancel(handle);

		result.Should().BeTrue();
	}

	[Fact]
	public void WhenRunningTimer_ShouldTransitionToStopped()
	{
		using var service = new TimerService();
		var       handle  = service.Create(TimeSpan.FromSeconds(5));

		service.Cancel(handle);

		service.TryGetInfo(handle, out var info).Should().BeTrue();
		info.State.Should().Be(TimerState.Stopped);
	}

	[Fact]
	public void WhenPausedTimer_ShouldReturnTrue()
	{
		using var service = new TimerService();
		var       handle  = service.Create(TimeSpan.FromSeconds(5));
		service.Pause(handle);

		var result = service.Cancel(handle);

		result.Should().BeTrue();
	}

	[Fact]
	public void WhenPausedTimer_ShouldTransitionToStopped()
	{
		using var service = new TimerService();
		var       handle  = service.Create(TimeSpan.FromSeconds(5));
		service.Pause(handle);

		service.Cancel(handle);

		service.TryGetInfo(handle, out var info).Should().BeTrue();
		info.State.Should().Be(TimerState.Stopped);
	}

	[Fact]
	public void WhenAlreadyStopped_ShouldReturnFalse()
	{
		using var service = new TimerService();
		var       handle  = service.Create(TimeSpan.FromSeconds(5));
		service.Cancel(handle);

		var result = service.Cancel(handle);

		result.Should().BeFalse();
	}

	[Fact]
	public void WhenInvalidHandle_ShouldReturnFalse()
	{
		using var service = new TimerService();

		var result = service.Cancel(TimerHandle.None);

		result.Should().BeFalse();
	}

	[Fact]
	public void WhenCancelled_ShouldDecrementActiveCount()
	{
		using var service = new TimerService();
		service.Create(TimeSpan.FromSeconds(1));
		var handle = service.Create(TimeSpan.FromSeconds(2));

		service.Cancel(handle);

		service.ActiveCount.Should().Be(1);
	}
}
