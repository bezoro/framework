using System;
using Bezoro.GameSystems.TimerSystem.Services;
using Bezoro.GameSystems.TimerSystem.Types;
using FluentAssertions;
using JetBrains.Annotations;
using Xunit;

namespace Bezoro.GameSystems.Tests.TimerSystem;

[TestSubject(typeof(TimerService))]
public class TimerServiceQueryTests
{
	[Fact]
	public void WhenCleanupCalled_ShouldNotRemoveActiveTimers()
	{
		using var service = new TimerService();
		var       h1      = service.Create(TimeSpan.FromSeconds(5));
		var       h2      = service.Create(TimeSpan.FromSeconds(5));
		service.Pause(h2);

		int removed = service.Cleanup();

		removed.Should().Be(0);
		service.TryGetInfo(h1, out _).Should().BeTrue();
		service.TryGetInfo(h2, out _).Should().BeTrue();
	}

	[Fact]
	public void WhenCleanupCalled_ShouldRemoveStoppedTimers()
	{
		using var service = new TimerService();
		var       h1      = service.Create(TimeSpan.FromSeconds(5));
		service.Create(TimeSpan.FromSeconds(5));

		service.Cancel(h1);

		int removed = service.Cleanup();

		removed.Should().Be(1);
		service.TryGetInfo(h1, out _).Should().BeFalse();
	}

	[Fact]
	public void WhenInvalidHandle_ShouldReturnFalse()
	{
		using var service = new TimerService();

		bool result = service.TryGetInfo(TimerHandle.None, out _);

		result.Should().BeFalse();
	}

	[Fact]
	public void WhenNonExistentHandle_ShouldReturnFalse()
	{
		using var service = new TimerService();

		bool result = service.TryGetInfo(new(999), out _);

		result.Should().BeFalse();
	}

	[Fact]
	public void WhenNoTimersToClean_ShouldReturnZero()
	{
		using var service = new TimerService();

		int removed = service.Cleanup();

		removed.Should().Be(0);
	}

	[Fact]
	public void WhenQueried_ShouldReturnCorrectDuration()
	{
		using var service = new TimerService();
		var       handle  = service.Create(TimeSpan.FromSeconds(5));

		service.TryGetInfo(handle, out var info);

		info.Duration.TotalSeconds.Should().BeApproximately(5.0, 0.1);
	}

	[Fact]
	public void WhenQueried_ShouldReturnCorrectHandle()
	{
		using var service = new TimerService();
		var       handle  = service.Create(TimeSpan.FromSeconds(5));

		service.TryGetInfo(handle, out var info);

		info.Handle.Should().Be(handle);
	}

	[Fact]
	public void WhenRunning_ProgressShouldBeBetweenZeroAndOne()
	{
		using var service = new TimerService();
		var       handle  = service.Create(TimeSpan.FromSeconds(10));

		service.TryGetInfo(handle, out var info);

		info.Progress.Should().BeInRange(0.0, 1.0);
	}

	[Fact]
	public void WhenRunning_RemainingShouldBeLessThanOrEqualToDuration()
	{
		using var service = new TimerService();
		var       handle  = service.Create(TimeSpan.FromSeconds(5));

		service.TryGetInfo(handle, out var info);

		info.Remaining.Should().BeLessThanOrEqualTo(info.Duration);
	}

	[Fact]
	public void WhenValidHandle_ShouldReturnTrue()
	{
		using var service = new TimerService();
		var       handle  = service.Create(TimeSpan.FromSeconds(5));

		bool result = service.TryGetInfo(handle, out _);

		result.Should().BeTrue();
	}
}
