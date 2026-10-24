using Bezoro.ECS.Services;
using Bezoro.ECS.Types;
using Bezoro.GameSystems.InputSystem.Extensions;
using Bezoro.GameSystems.InputSystem.Types;
using Bezoro.GameSystems.MovementSystem.Types;
using Xunit;
using MovementSystemType = Bezoro.GameSystems.MovementSystem.Services.MovementSystem;

namespace Bezoro.GameSystems.Tests.InputSystem;

public class InputNumericSafetyAdditionalTests
{
	[Fact]
	public void InitialZeroSequenceMovesAndEqualSequenceCannotReplaceIt()
	{
		var world = CreateWorld(out var queue);
		var entity = Spawn(world, 2f);
		queue.Enqueue(new InputCommand(7, 1f, -2f, 0.5f, 0));
		world.FixedTick(0.125f);
		Assert.Equal(new Position { X = 0.25f, Y = -0.5f, Z = 0.125f }, world.Read<Position>(entity));

		queue.Enqueue(new InputCommand(7, -1f, 2f, -0.5f, 0));
		world.FixedTick(0.125f);
		Assert.Equal(new Position { X = 0.5f, Y = -1f, Z = 0.25f }, world.Read<Position>(entity));
		Assert.Equal(new Velocity { X = 2f, Y = -4f, Z = 1f }, world.Read<Velocity>(entity));
		Assert.Equal(new MovementIntent { X = 1f, Y = -2f, Z = 0.5f }, world.Read<MovementIntent>(entity));
	}

	[Fact]
	public void NewerNeutralCommandStopsMovementAndOlderOrEqualCommandsCannotRestartIt()
	{
		var world = CreateWorld(out var queue);
		var entity = Spawn(world, 2f);
		queue.Enqueue(new InputCommand(7, 1f, -2f, 0.5f, 7));
		world.FixedTick(0.125f);
		var stoppedAt = world.Read<Position>(entity);

		queue.Enqueue(new InputCommand(7, 0f, 0f, 0f, 8));
		world.FixedTick(0.125f);
		Assert.Equal(stoppedAt, world.Read<Position>(entity));
		Assert.Equal(default(Velocity), world.Read<Velocity>(entity));
		Assert.Equal(default(MovementIntent), world.Read<MovementIntent>(entity));

		queue.Enqueue(new InputCommand(7, -1f, 2f, -0.5f, 7));
		queue.Enqueue(new InputCommand(7, -1f, 2f, -0.5f, 8));
		world.FixedTick(0.125f);
		Assert.Equal(stoppedAt, world.Read<Position>(entity));
		Assert.Equal(default(Velocity), world.Read<Velocity>(entity));
		Assert.Equal(default(MovementIntent), world.Read<MovementIntent>(entity));
	}

	[Theory]
	[InlineData(0f)]
	[InlineData(0.5f)]
	public void FiniteExtremeCommandsKeepTheirIntentAndRepresentableVelocity(float speed)
	{
		var world = CreateWorld(out var queue);
		var entity = Spawn(world, speed);
		queue.Enqueue(new InputCommand(7, float.MaxValue, float.MinValue, -0f, 1));
		world.FixedTick(0.125f);

		var expectedVelocity = new Velocity { X = float.MaxValue * speed, Y = float.MinValue * speed, Z = 0f };
		Assert.Equal(new MovementIntent { X = float.MaxValue, Y = float.MinValue, Z = 0f }, world.Read<MovementIntent>(entity));
		Assert.Equal(expectedVelocity, world.Read<Velocity>(entity));
		Assert.Equal(new Position { X = expectedVelocity.X * 0.125f, Y = expectedVelocity.Y * 0.125f, Z = 0f }, world.Read<Position>(entity));
	}

	private static World CreateWorld(out InputCommandQueue queue)
	{
		var world = new World();
		queue = world.GetOrCreateInputCommandQueue();
		world.AddMovementInputPipeline();
		world.AddSystem(new MovementSystemType());
		return world;
	}

	private static Entity Spawn(World world, float speed)
	{
		var entity = world.Spawn(new Position(), new Velocity(), new InputControl { ControlId = 7 }, new MovementIntent());
		world.Add(entity, new MovementInputSettings { Speed = speed, HoldDurationSeconds = 0.5f });
		return entity;
	}
}
