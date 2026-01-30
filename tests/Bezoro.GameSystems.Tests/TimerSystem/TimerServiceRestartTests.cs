using System;
using System.Threading;
using System.Threading.Tasks;
using Bezoro.GameSystems.TimerSystem.Services;
using Bezoro.GameSystems.TimerSystem.Types;
using FluentAssertions;
using JetBrains.Annotations;
using Xunit;

namespace Bezoro.GameSystems.Tests.TimerSystem;

[TestSubject(typeof(TimerService))]
public class TimerServiceRestartTests
{
	[Fact]
	public void WhenRunningTimer_ShouldReturnTrue()
	{
		using var service = new TimerService();
		var       handle  = service.Create(TimeSpan.FromSeconds(5));

		var result = service.Restart(handle);

		result.Should().BeTrue();
	}

	[Fact]
	public void WhenRunningTimer_ShouldResetProgress()
	{
		using var service = new TimerService();
		var       handle  = service.Create(TimeSpan.FromSeconds(5));

		service.Restart(handle);

		service.TryGetInfo(handle, out var info).Should().BeTrue();
		info.State.Should().Be(TimerState.Running);
		info.Progress.Should().BeLessThan(0.01);
	}

	[Fact]
	public void WhenPausedTimer_ShouldReturnTrue()
	{
		using var service = new TimerService();
		var       handle  = service.Create(TimeSpan.FromSeconds(5));
		service.Pause(handle);

		var result = service.Restart(handle);

		result.Should().BeTrue();
	}

	[Fact]
	public void WhenPausedTimer_ShouldTransitionToRunning()
	{
		using var service = new TimerService();
		var       handle  = service.Create(TimeSpan.FromSeconds(5));
		service.Pause(handle);

		service.Restart(handle);

		service.TryGetInfo(handle, out var info).Should().BeTrue();
		info.State.Should().Be(TimerState.Running);
		info.Progress.Should().BeLessThan(0.01);
	}

	[Fact]
	public void WhenStoppedTimer_ShouldReturnTrue()
	{
		using var service = new TimerService();
		var       handle  = service.Create(TimeSpan.FromSeconds(5));
		service.Cancel(handle);

		var result = service.Restart(handle);

		result.Should().BeTrue();
	}

	[Fact]
	public void WhenStoppedTimer_ShouldTransitionToRunning()
	{
		using var service = new TimerService();
		var       handle  = service.Create(TimeSpan.FromSeconds(5));
		service.Cancel(handle);

		service.Restart(handle);

		service.TryGetInfo(handle, out var info).Should().BeTrue();
		info.State.Should().Be(TimerState.Running);
	}

	[Fact]
	public async Task WhenCompletedTimer_ShouldReturnTrue()
	{
		using var service = new TimerService();
		var       handle  = service.Create(TimeSpan.FromMilliseconds(50), mode: TimerMode.Persistent);

		service.Start(new TimerConfig(tickRateMs: 10));
		await Task.Delay(200);

		service.TryGetInfo(handle, out var info).Should().BeTrue();
		info.State.Should().Be(TimerState.Completed);

		var result = service.Restart(handle);

		result.Should().BeTrue();
	}

	[Fact]
	public async Task WhenCompletedTimer_ShouldTransitionToRunningAndResetProgress()
	{
		using var service = new TimerService();
		var       handle  = service.Create(TimeSpan.FromMilliseconds(50), mode: TimerMode.Persistent);

		service.Start(new TimerConfig(tickRateMs: 10));
		await Task.Delay(200);

		service.Restart(handle);

		service.TryGetInfo(handle, out var info).Should().BeTrue();
		info.State.Should().Be(TimerState.Running);
		info.Progress.Should().BeLessThan(0.5);
	}

	[Fact]
	public void WhenInvalidHandle_ShouldReturnFalse()
	{
		using var service = new TimerService();

		var result = service.Restart(TimerHandle.None);

		result.Should().BeFalse();
	}

	[Fact]
	public void WhenNonExistentHandle_ShouldReturnFalse()
	{
		using var service = new TimerService();

		var result = service.Restart(new TimerHandle(999));

		result.Should().BeFalse();
	}

	[Fact]
	public void WhenRestarted_ShouldCountAsActive()
	{
		using var service = new TimerService();
		var       handle  = service.Create(TimeSpan.FromSeconds(5));
		service.Cancel(handle);
		service.ActiveCount.Should().Be(0);

		service.Restart(handle);

		service.ActiveCount.Should().Be(1);
	}

	[Fact]
	public async Task WhenRestartedAfterCompletion_ShouldFireCallbackAgain()
	{
		using var service = new TimerService();
		int       count   = 0;

		var handle = service.Create(TimeSpan.FromMilliseconds(50), _ => Interlocked.Increment(ref count), TimerMode.Persistent);
		service.Start(new TimerConfig(tickRateMs: 10));

		await Task.Delay(200);
		Volatile.Read(ref count).Should().Be(1);

		service.Restart(handle);
		await Task.Delay(200);

		Volatile.Read(ref count).Should().Be(2);
	}
}
