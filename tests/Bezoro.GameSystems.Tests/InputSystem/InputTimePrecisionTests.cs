using System;
using Bezoro.ECS.Services;
using Bezoro.ECS.Types;
using Bezoro.GameSystems.InputSystem.Extensions;
using Bezoro.GameSystems.InputSystem.Services;
using Bezoro.GameSystems.InputSystem.Types;
using Bezoro.GameSystems.MovementSystem.Types;
using Xunit;

namespace Bezoro.GameSystems.Tests.InputSystem;

public class InputTimePrecisionTests
{
	[Fact]
	public void InputExpiresAfterLongUptimeWithSmallFrameDurations()
	{
		using var world = new World();
		var queue = world.GetOrCreateInputCommandQueue();
		world.AddMovementInputPipeline();
		var entity = world.Spawn(new Velocity(), new InputControl { ControlId = 1 }, new MovementIntent(),
			new MovementInputSettings { Speed = 1, HoldDurationSeconds = 0.03125f });
		world.FixedTick(1048576);
		queue.Enqueue(1, 1, 0, 0, 1);
		world.FixedTick(0.015625f);
		Assert.Equal(1f, world.Read<Velocity>(entity).X);
		world.FixedTick(0.03125f);
		Assert.Equal(1f, world.Read<Velocity>(entity).X);
		world.FixedTick(0.015625f);
		Assert.Equal(0f, world.Read<Velocity>(entity).X);
	}

	[Theory]
	[InlineData(float.NaN)]
	[InlineData(float.PositiveInfinity)]
	[InlineData(float.NegativeInfinity)]
	public void NonfiniteTick_DoesNotCorruptInputClock(float invalid)
	{
		using var world = new World();
		var queue = world.GetOrCreateInputCommandQueue();
		world.AddMovementInputPipeline();
		using var commands = world.CreateCommandStream();
		new InputIngestionSystem().Update(new SystemContext(invalid, Stage.Input, world, commands));
		Assert.Equal(0f, queue.SimulationTimeSeconds);
		world.FixedTick(0.25f);
		Assert.Equal(0.25f, queue.SimulationTimeSeconds);
	}
}
