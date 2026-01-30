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
public class TimerServiceTickTests
{
	[Fact]
	public async Task WhenTimerExpires_ShouldTransitionToCompleted()
	{
		using var service = new TimerService();
		var       handle  = service.Create(TimeSpan.FromMilliseconds(50));

		service.Start(new TimerConfig(tickRateMs: 10));

		await Task.Delay(200);

		service.TryGetInfo(handle, out var info).Should().BeTrue();
		info.State.Should().Be(TimerState.Completed);
	}

	[Fact]
	public async Task WhenTimerExpires_ShouldInvokeCallback()
	{
		using var service  = new TimerService();
		int       callFlag = 0;

		service.Create(TimeSpan.FromMilliseconds(50), _ => Interlocked.Exchange(ref callFlag, 1));
		service.Start(new TimerConfig(tickRateMs: 10));

		await Task.Delay(200);

		Volatile.Read(ref callFlag).Should().Be(1);
	}

	[Fact]
	public async Task WhenTimerExpires_ShouldRaiseTimerCompletedEvent()
	{
		using var service    = new TimerService();
		int       eventFlag  = 0;

		service.TimerCompleted += _ => Interlocked.Exchange(ref eventFlag, 1);

		service.Create(TimeSpan.FromMilliseconds(50));
		service.Start(new TimerConfig(tickRateMs: 10));

		await Task.Delay(200);

		Volatile.Read(ref eventFlag).Should().Be(1);
	}

	[Fact]
	public async Task WhenTimerIsPaused_ShouldNotComplete()
	{
		using var service = new TimerService();
		var       handle  = service.Create(TimeSpan.FromMilliseconds(50));
		service.Pause(handle);

		service.Start(new TimerConfig(tickRateMs: 10));
		await Task.Delay(200);

		service.TryGetInfo(handle, out var info).Should().BeTrue();
		info.State.Should().Be(TimerState.Paused);
	}

	[Fact]
	public async Task WhenMultipleTimers_ShouldCompleteIndependently()
	{
		using var service = new TimerService();
		var       fast    = service.Create(TimeSpan.FromMilliseconds(50));
		var       slow    = service.Create(TimeSpan.FromMilliseconds(500));

		service.Start(new TimerConfig(tickRateMs: 10));
		await Task.Delay(200);

		service.TryGetInfo(fast, out var fastInfo).Should().BeTrue();
		service.TryGetInfo(slow, out var slowInfo).Should().BeTrue();

		fastInfo.State.Should().Be(TimerState.Completed);
		slowInfo.State.Should().Be(TimerState.Running);
	}

	[Fact]
	public async Task WhenCallbackThrows_ShouldNotCrashLoop()
	{
		using var service   = new TimerService();
		int       completed = 0;

		// First timer throws
		service.Create(TimeSpan.FromMilliseconds(30), _ => throw new InvalidOperationException("test"));

		// Second timer should still complete
		service.Create(TimeSpan.FromMilliseconds(60), _ => Interlocked.Exchange(ref completed, 1));

		service.Start(new TimerConfig(tickRateMs: 10));
		await Task.Delay(300);

		Volatile.Read(ref completed).Should().Be(1);
		service.IsRunning.Should().BeTrue();
	}

	[Fact]
	public async Task WhenTimerCompleted_ProgressShouldBeOne()
	{
		using var service = new TimerService();
		var       handle  = service.Create(TimeSpan.FromMilliseconds(50));

		service.Start(new TimerConfig(tickRateMs: 10));
		await Task.Delay(200);

		service.TryGetInfo(handle, out var info).Should().BeTrue();
		info.Progress.Should().Be(1.0);
	}
}
