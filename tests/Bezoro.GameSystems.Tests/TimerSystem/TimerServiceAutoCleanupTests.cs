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
public class TimerServiceAutoCleanupTests
{
	[Fact]
	public async Task WhenOneShotCompletes_ShouldAutoRemove()
	{
		using var service = new TimerService();
		var       handle  = service.Create(TimeSpan.FromMilliseconds(50), mode: TimerMode.OneShot);

		service.Start(new TimerConfig(tickRateMs: 10));
		await Task.Delay(300);

		service.TryGetInfo(handle, out _).Should().BeFalse();
	}

	[Fact]
	public async Task WhenPersistentCompletes_ShouldNotAutoRemove()
	{
		using var service = new TimerService();
		var       handle  = service.Create(TimeSpan.FromMilliseconds(50), mode: TimerMode.Persistent);

		service.Start(new TimerConfig(tickRateMs: 10));
		await Task.Delay(300);

		service.TryGetInfo(handle, out var info).Should().BeTrue();
		info.State.Should().Be(TimerState.Completed);
	}

	[Fact]
	public async Task WhenPersistentCompleted_ShouldBeRestartable()
	{
		using var service   = new TimerService();
		int       callCount = 0;

		var handle = service.Create(
			TimeSpan.FromMilliseconds(50),
			_ => Interlocked.Increment(ref callCount),
			TimerMode.Persistent);

		service.Start(new TimerConfig(tickRateMs: 10));
		await Task.Delay(200);

		Volatile.Read(ref callCount).Should().Be(1);

		service.Restart(handle).Should().BeTrue();
		await Task.Delay(200);

		Volatile.Read(ref callCount).Should().Be(2);
	}

	[Fact]
	public void WhenDefaultMode_ShouldBeOneShot()
	{
		using var service = new TimerService();
		var       handle  = service.Create(TimeSpan.FromSeconds(5));

		service.TryGetInfo(handle, out var info);

		info.Mode.Should().Be(TimerMode.OneShot);
	}

	[Fact]
	public async Task WhenManualCleanup_ShouldRemovePersistentCompleted()
	{
		using var service = new TimerService();
		var       handle  = service.Create(TimeSpan.FromMilliseconds(50), mode: TimerMode.Persistent);

		service.Start(new TimerConfig(tickRateMs: 10));
		await Task.Delay(200);

		service.TryGetInfo(handle, out var info).Should().BeTrue();
		info.State.Should().Be(TimerState.Completed);

		var removed = service.Cleanup();

		removed.Should().Be(1);
		service.TryGetInfo(handle, out _).Should().BeFalse();
	}
}
