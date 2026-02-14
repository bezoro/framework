using Bezoro.ECS.Services;
using Bezoro.ECS.Types;
using Bezoro.GameSystems.TimerSystem.Extensions;
using Bezoro.GameSystems.TimerSystem.Types;
using FluentAssertions;
using JetBrains.Annotations;
using Xunit;
using TimerSystemType = Bezoro.GameSystems.TimerSystem.Services.TimerSystem;

namespace Bezoro.GameSystems.Tests.TimerSystem;

[TestSubject(typeof(TimerSystemType))]
public class TimerSystemTests
{
	[Fact]
	public void Tick_WhenOneShotTimerCompletes_ShouldRaiseFinishedAndDespawn()
	{
		// Arrange
		var world  = new WorldV1();
		var system = new TimerSystemType();
		world.AddSystem(system);

		var finishedCount = 0;
		system.Finished += _ => finishedCount++;

		var timerEntity = world.Spawn(
			new Timer(timerId: 1, durationSeconds: 1f, elapsedSeconds: 0.5f, state: TimerState.Running, mode: TimerMode.OneShot)
		);

		// Act
		world.Tick(0.6f);

		// Assert
		world.IsAlive(timerEntity).Should().BeFalse();
		finishedCount.Should().Be(1);
	}

	[Fact]
	public void Tick_WhenRunningTimersExist_ShouldIncrementElapsedByDeltaTime()
	{
		// Arrange
		var world = new WorldV1();
		world.AddSystem(new TimerSystemType());

		var e1 = world.Spawn(
			new Timer(timerId: 1, durationSeconds: 10f, elapsedSeconds: 1f, state: TimerState.Running)
		);
		var e2 = world.Spawn(
			new Timer(timerId: 2, durationSeconds: 5f, elapsedSeconds: 0.25f, state: TimerState.Running)
		);

		// Act
		world.Tick(0.5f);

		// Assert
		var t1 = world.Get<Timer>(e1);
		var t2 = world.Get<Timer>(e2);

		t1.ElapsedSeconds.Should().BeApproximately(1.5f, 0.0001f);
		t2.ElapsedSeconds.Should().BeApproximately(0.75f, 0.0001f);
	}

	[Fact]
	public void Tick_WhenTimerCompletes_ShouldSetCompletedAndPublishFinishedEvent()
	{
		// Arrange
		var world  = new WorldV1();
		var system = new TimerSystemType();
		world.AddSystem(system);

		var finishedCount = 0;
		system.Finished += _ => finishedCount++;

		var timerEntity = world.Spawn(
			new Timer(timerId: 7, durationSeconds: 1f, elapsedSeconds: 0.75f, state: TimerState.Running)
		);

		// Act
		world.Tick(0.5f);

		// Assert
		var timer = world.Get<Timer>(timerEntity);
		timer.State.Should().Be(TimerState.Completed);
		timer.ElapsedSeconds.Should().BeApproximately(1f, 0.0001f);

		finishedCount.Should().Be(1);

		var events = world.GetResource<TimerEventsResource>();
		events.Count.Should().Be(1);
		events.TryDequeue(out var evt).Should().BeTrue();
		evt.Lifecycle.Should().Be(TimerLifecycle.Finished);
		evt.TimerEntity.Should().Be(timerEntity);
		evt.TimerId.Should().Be(7);
	}

	[Fact]
	public void Tick_WhenPausedTimerIsResumed_ShouldRaiseResumedAndContinueProgress()
	{
		// Arrange
		var world  = new WorldV1();
		var system = new TimerSystemType();
		world.AddSystem(system);

		var resumedCount = 0;
		system.Resumed += _ => resumedCount++;

		var timerEntity = world.Spawn(
			new Timer(timerId: 13, durationSeconds: 10f, elapsedSeconds: 2f, state: TimerState.Paused)
		);

		// Act
		world.ResumeTimer(timerEntity);
		world.Tick(1f);

		// Assert
		var timer = world.Get<Timer>(timerEntity);
		timer.State.Should().Be(TimerState.Running);
		timer.ElapsedSeconds.Should().BeApproximately(3f, 0.0001f);
		resumedCount.Should().Be(1);
	}

	[Fact]
	public void Tick_WhenRestartRequested_ShouldRaiseRestartedWithoutStarted()
	{
		// Arrange
		var world  = new WorldV1();
		var system = new TimerSystemType();
		world.AddSystem(system);

		var startedCount   = 0;
		var restartedCount = 0;
		system.Started += _ => startedCount++;
		system.Restarted += _ => restartedCount++;

		var timerEntity = world.Spawn(
			new Timer(timerId: 21, durationSeconds: 10f, elapsedSeconds: 9f, state: TimerState.Running)
		);

		// Act
		world.RestartTimer(timerEntity);
		world.Tick(0.25f);

		// Assert
		var timer = world.Get<Timer>(timerEntity);
		timer.State.Should().Be(TimerState.Running);
		timer.ElapsedSeconds.Should().BeApproximately(0.25f, 0.0001f);
		restartedCount.Should().Be(1);
		startedCount.Should().Be(0);
	}

	[Fact]
	public void Tick_WhenStartPauseStopAreRequested_ShouldEmitMatchingLifecycleCallbacks()
	{
		// Arrange
		var world  = new WorldV1();
		var system = new TimerSystemType();
		world.AddSystem(system);

		var startedCount = 0;
		var pausedCount  = 0;
		var stoppedCount = 0;

		system.Started += _ => startedCount++;
		system.Paused += _ => pausedCount++;
		system.Stopped += _ => stoppedCount++;

		var timerEntity = world.Spawn(
			new Timer(timerId: 30, durationSeconds: 10f, elapsedSeconds: 0f, state: TimerState.Stopped)
		);

		// Act
		world.StartTimer(timerEntity);
		world.Tick(0.5f);

		world.PauseTimer(timerEntity);
		world.Tick(0.5f);

		world.StopTimer(timerEntity);
		world.Tick(0.5f);

		// Assert
		var timer = world.Get<Timer>(timerEntity);
		timer.State.Should().Be(TimerState.Stopped);
		startedCount.Should().Be(1);
		pausedCount.Should().Be(1);
		stoppedCount.Should().Be(1);
	}
}
