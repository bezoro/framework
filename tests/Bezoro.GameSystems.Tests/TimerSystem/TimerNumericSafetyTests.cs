using System;
using Bezoro.ECS.Services;
using Bezoro.ECS.Types;
using Bezoro.GameSystems.TimerSystem.Types;
using Xunit;
using TimerSystemType = Bezoro.GameSystems.TimerSystem.Services.TimerSystem;

namespace Bezoro.GameSystems.Tests.TimerSystem;

public class TimerNumericSafetyTests
{
	[Theory]
	[InlineData(float.NaN)]
	[InlineData(float.PositiveInfinity)]
	[InlineData(float.NegativeInfinity)]
	public void Constructor_RejectsNonfiniteTimes(float invalid)
	{
		Assert.Equal("durationSeconds", Assert.Throws<ArgumentOutOfRangeException>(() => new Timer(1, invalid)).ParamName);
		Assert.Equal("elapsedSeconds", Assert.Throws<ArgumentOutOfRangeException>(() => new Timer(1, 1, invalid)).ParamName);
	}

	[Theory]
	[InlineData(float.NaN)]
	[InlineData(float.PositiveInfinity)]
	[InlineData(float.NegativeInfinity)]
	public void NonfiniteTick_DoesNotCompleteTimerAndValidTicksRecover(float invalid)
	{
		using var world = new World();
		var system = new TimerSystemType();
		world.AddSystem(system);
		var finished = 0;
		system.Finished += _ => finished++;
		var entity = world.Spawn(new Timer(1, 1, 0, TimerState.Running));
		using var commands = world.CreateCommandStream();
		system.Update(new SystemContext(invalid, Stage.Tick, world, commands));
		Assert.Equal(0, finished);
		Assert.Equal(0f, world.Read<Timer>(entity).ElapsedSeconds);
		world.Tick(0.25f);
		Assert.Equal(0.25f, world.Read<Timer>(entity).ElapsedSeconds);
	}

	[Fact]
	public void MutatedNonfiniteDuration_DoesNotProduceAFalseFinishedEvent()
	{
		using var world = new World();
		var system = new TimerSystemType();
		world.AddSystem(system);
		var finished = 0;
		system.Finished += _ => finished++;
		var entity = world.Spawn(new Timer(1, 1, 0, TimerState.Running));
		world.Write<Timer>(entity).DurationSeconds = float.NaN;
		world.Tick(0.5f);
		Assert.Equal(0, finished);
		world.Write<Timer>(entity).DurationSeconds = 1;
		world.Tick(1);
		Assert.Equal(1, finished);
	}
}
