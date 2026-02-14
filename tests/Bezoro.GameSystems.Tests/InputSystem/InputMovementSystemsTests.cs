using Bezoro.ECS.Attributes;
using Bezoro.ECS.Services;
using Bezoro.ECS.Types;
using Bezoro.GameSystems.InputSystem.Services;
using Bezoro.GameSystems.InputSystem.Types;
using Bezoro.GameSystems.MovementSystem.Types;
using FluentAssertions;
using JetBrains.Annotations;
using Xunit;
using MovementSystemType = Bezoro.GameSystems.MovementSystem.Services.MovementSystem;

namespace Bezoro.GameSystems.Tests.InputSystem;

[TestSubject(typeof(IntentToVelocitySystem))]
public class InputMovementSystemsTests
{
	[Fact]
	public void FixedTick_WhenControlsAreDifferent_ShouldDriveEachEntityIndependently()
	{
		// Arrange
		var world = new WorldV1();
		var queue = new InputCommandQueue();
		world.SetResource(queue);
		world.AddSystem(new InputIngestionSystem(), Stage.Input);
		world.AddSystem(new IntentToVelocitySystem(), Stage.PreTick);
		world.AddSystem(new MovementSystemType(), Stage.Tick);

		var player = world.Spawn(
			new Position(),
			new Velocity(),
			new InputControl { ControlId = 1 },
			new MovementIntent()
		);
		world.Add(player, new MovementInputSettings { Speed = 3f, HoldDurationSeconds = 0.2f });

		var enemy = world.Spawn(
			new Position(),
			new Velocity(),
			new InputControl { ControlId = 2 },
			new MovementIntent()
		);
		world.Add(enemy, new MovementInputSettings { Speed = 5f, HoldDurationSeconds = 0.2f });

		queue.Enqueue(new InputCommand(controlId: 1, moveX: 1f, moveY: 0f, moveZ: 0f, sequence: 1));
		queue.Enqueue(new InputCommand(controlId: 2, moveX: 0f, moveY: -1f, moveZ: 0f, sequence: 1));

		// Act
		world.FixedTick(0.1f);

		// Assert
		var playerPosition = world.Get<Position>(player);
		playerPosition.X.Should().BeApproximately(0.3f, 0.0001f);
		playerPosition.Y.Should().BeApproximately(0f, 0.0001f);

		var enemyPosition = world.Get<Position>(enemy);
		enemyPosition.X.Should().BeApproximately(0f, 0.0001f);
		enemyPosition.Y.Should().BeApproximately(-0.5f, 0.0001f);
	}

	[Fact]
	public void FixedTick_WhenInputIsWithinHoldWindow_ShouldReuseLastInput()
	{
		// Arrange
		var world = new WorldV1();
		var queue = new InputCommandQueue();
		world.SetResource(queue);
		world.AddSystem(new InputIngestionSystem(), Stage.Input);
		world.AddSystem(new IntentToVelocitySystem(), Stage.PreTick);
		world.AddSystem(new MovementSystemType(), Stage.Tick);

		var entity = world.Spawn(
			new Position(),
			new Velocity(),
			new InputControl { ControlId = 7 },
			new MovementIntent()
		);
		world.Add(entity, new MovementInputSettings { Speed = 4f, HoldDurationSeconds = 0.2f });

		queue.Enqueue(new InputCommand(controlId: 7, moveX: 1f, moveY: 0f, moveZ: 0f, sequence: 1));

		// Act
		world.FixedTick(0.1f);
		var afterFirstTick = world.Get<Position>(entity);

		world.FixedTick(0.05f);
		var afterSecondTick = world.Get<Position>(entity);

		// Assert
		afterFirstTick.X.Should().BeApproximately(0.4f, 0.0001f);
		afterSecondTick.X.Should().BeApproximately(0.6f, 0.0001f);
	}

	[Fact]
	public void FixedTick_WhenInputExpires_ShouldZeroVelocity()
	{
		// Arrange
		var world = new WorldV1();
		var queue = new InputCommandQueue();
		world.SetResource(queue);
		world.AddSystem(new InputIngestionSystem(), Stage.Input);
		world.AddSystem(new IntentToVelocitySystem(), Stage.PreTick);
		world.AddSystem(new MovementSystemType(), Stage.Tick);

		var entity = world.Spawn(
			new Position(),
			new Velocity(),
			new InputControl { ControlId = 42 },
			new MovementIntent()
		);
		world.Add(entity, new MovementInputSettings { Speed = 2f, HoldDurationSeconds = 0.1f });

		queue.Enqueue(new InputCommand(controlId: 42, moveX: 1f, moveY: 0f, moveZ: 0f, sequence: 1));
		world.FixedTick(0.05f);

		// Act
		world.FixedTick(0.11f);

		// Assert
		var position = world.Get<Position>(entity);
		position.X.Should().BeApproximately(0.1f, 0.0001f);

		var velocity = world.Get<Velocity>(entity);
		velocity.X.Should().Be(0f);
		velocity.Y.Should().Be(0f);
		velocity.Z.Should().Be(0f);
	}

	[Fact]
	public void FixedTick_WhenNewInputArrivesAfterExpiry_ShouldResumeMovement()
	{
		// Arrange
		var world = new WorldV1();
		var queue = new InputCommandQueue();
		world.SetResource(queue);
		world.AddSystem(new InputIngestionSystem(), Stage.Input);
		world.AddSystem(new IntentToVelocitySystem(), Stage.PreTick);
		world.AddSystem(new MovementSystemType(), Stage.Tick);

		var entity = world.Spawn(
			new Position(),
			new Velocity(),
			new InputControl { ControlId = 9 },
			new MovementIntent()
		);
		world.Add(entity, new MovementInputSettings { Speed = 3f, HoldDurationSeconds = 0.1f });

		queue.Enqueue(new InputCommand(controlId: 9, moveX: 1f, moveY: 0f, moveZ: 0f, sequence: 1));
		world.FixedTick(0.05f);
		world.FixedTick(0.11f);

		// Act
		queue.Enqueue(new InputCommand(controlId: 9, moveX: -1f, moveY: 0f, moveZ: 0f, sequence: 2));
		world.FixedTick(0.05f);

		// Assert
		var position = world.Get<Position>(entity);
		position.X.Should().BeApproximately(0f, 0.0001f);

		var velocity = world.Get<Velocity>(entity);
		velocity.X.Should().Be(-3f);
	}

	[Fact]
	public void FixedTick_WhenOlderSequenceArrives_ShouldKeepNewestInput()
	{
		// Arrange
		var world = new WorldV1();
		var queue = new InputCommandQueue();
		world.SetResource(queue);
		world.AddSystem(new InputIngestionSystem(), Stage.Input);
		world.AddSystem(new IntentToVelocitySystem(), Stage.PreTick);
		world.AddSystem(new MovementSystemType(), Stage.Tick);

		var entity = world.Spawn(
			new Position(),
			new Velocity(),
			new InputControl { ControlId = 5 },
			new MovementIntent()
		);
		world.Add(entity, new MovementInputSettings { Speed = 1f, HoldDurationSeconds = 0.2f });

		queue.Enqueue(new InputCommand(controlId: 5, moveX: 1f, moveY: 0f, moveZ: 0f, sequence: 2));
		queue.Enqueue(new InputCommand(controlId: 5, moveX: -1f, moveY: 0f, moveZ: 0f, sequence: 1));

		// Act
		world.FixedTick(0.1f);

		// Assert
		var position = world.Get<Position>(entity);
		position.X.Should().BeApproximately(0.1f, 0.0001f);
	}

	[Fact]
	public void Metadata_WhenInspectingIntentToVelocitySystem_ShouldDeclareReadAndWriteAttributes()
	{
		// Arrange
		var systemType = typeof(IntentToVelocitySystem);

		// Act / Assert
		systemType.IsDefined(typeof(ReadsAttribute<InputControl>), true).Should().BeTrue();
		systemType.IsDefined(typeof(ReadsAttribute<MovementInputSettings>), true).Should().BeTrue();
		systemType.IsDefined(typeof(WritesAttribute<MovementIntent>), true).Should().BeTrue();
		systemType.IsDefined(typeof(WritesAttribute<Velocity>), true).Should().BeTrue();
	}
}
