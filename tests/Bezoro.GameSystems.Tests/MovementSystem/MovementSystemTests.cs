using Bezoro.ECS.Attributes;
using Bezoro.ECS.Services;
using Bezoro.ECS.Types;
using Bezoro.GameSystems.MovementSystem.Types;
using FluentAssertions;
using JetBrains.Annotations;
using Xunit;
using MovementSystemType = Bezoro.GameSystems.MovementSystem.Services.MovementSystem;

namespace Bezoro.GameSystems.Tests.MovementSystem;

[TestSubject(typeof(MovementSystemType))]
public class MovementSystemTests
{
	[Fact]
	public void Metadata_WhenInspectingMovementSystem_ShouldDeclareReadAndWriteAttributes()
	{
		// Arrange
		var systemType = typeof(MovementSystemType);

		// Act / Assert
		systemType.IsDefined(typeof(WritesAttribute<Position>), true).Should().BeTrue();
		systemType.IsDefined(typeof(ReadsAttribute<Velocity>),  true).Should().BeTrue();
	}

	[Fact]
	public void Update_WhenEntitiesHavePositionAndVelocity_ShouldAdvancePositions()
	{
		// Arrange
		var world = new World();
		world.AddSystem(new MovementSystemType());

		var e1 = world.Spawn(
			new Position { X = 1f, Y   = 2f, Z  = 3f },
			new Velocity { X = 0.5f, Y = -1f, Z = 2f }
		);

		var e2 = world.Spawn(
			new Position { X = -4f, Y = 0.5f, Z = 1f },
			new Velocity { X = 2f, Y  = 1f, Z   = -0.5f }
		);

		// Act
		world.Update(2f);

		// Assert
		var p1 = world.Get<Position>(e1);
		p1.X.Should().Be(1f + 0.5f * 2f);
		p1.Y.Should().Be(2f + -1f * 2f);
		p1.Z.Should().Be(3f + 2f * 2f);

		var p2 = world.Get<Position>(e2);
		p2.X.Should().Be(-4f + 2f * 2f);
		p2.Y.Should().Be(0.5f + 1f * 2f);
		p2.Z.Should().Be(1f + -0.5f * 2f);
	}

	[Fact]
	public void Update_WhenFixedInterval_ShouldAccumulateAndApplyStep()
	{
		// Arrange
		var world  = new World();
		var system = new MovementSystemType(SystemUpdateSettings.Fixed(0.5f));
		world.AddSystem(system);

		var entity = world.Spawn(
			new Position { X = 0f, Y = 0f, Z = 0f },
			new Velocity { X = 2f, Y = 0f, Z = 0f }
		);

		// Act
		world.Update(0.25f);
		var afterFirst = world.Get<Position>(entity);

		world.Update(0.25f);
		var afterSecond = world.Get<Position>(entity);

		// Assert
		afterFirst.Should().Be(new Position { X  = 0f, Y = 0f, Z = 0f });
		afterSecond.Should().Be(new Position { X = 1f, Y = 0f, Z = 0f });
	}
}
