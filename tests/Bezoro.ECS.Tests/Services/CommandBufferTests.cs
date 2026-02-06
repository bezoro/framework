using System;
using Bezoro.ECS.Abstractions;
using Bezoro.ECS.Services;
using Bezoro.ECS.Types;
using FluentAssertions;
using JetBrains.Annotations;
using Xunit;

namespace Bezoro.ECS.Tests.Services;

[TestSubject(typeof(CommandBuffer))]
public class CommandBufferTests
{
	[Fact]
	public void AddComponent_WhenComponentAlreadyExists_ShouldThrowOnPlayback()
	{
		// Arrange
		var world  = new World();
		var entity = world.Spawn();
		world.Add(entity, new Position { X = 1, Y = 2 });
		var commands = world.CreateCommandBuffer();
		commands.AddComponent(entity, new Position { X = 9, Y = 8 });

		// Act
		var act = () => commands.Playback();

		// Assert
		act.Should().Throw<InvalidOperationException>();

		var component = world.Get<Position>(entity);
		component.X.Should().Be(1);
		component.Y.Should().Be(2);
	}

	[Fact]
	public void CommandBuffer_ShouldDeferStructuralChangesUntilAfterUpdate()
	{
		// Arrange
		var world  = new World();
		var entity = world.Spawn();
		world.Add(entity, new Position { X = 1, Y = 2 });
		world.AddSystem(new AddVelocitySystem(entity));

		// Act
		world.Has<Velocity>(entity).Should().BeFalse();
		world.Update(0.016f);

		// Assert
		world.Has<Velocity>(entity).Should().BeTrue();
	}

	[Fact]
	public void CommandBuffer_WhenDisposed_ShouldRejectFurtherUsage()
	{
		// Arrange
		var world    = new World();
		var commands = world.CreateCommandBuffer();

		// Act
		commands.Dispose();
		var addAct      = () => commands.AddComponent(world.Spawn(), new Position { X = 1, Y = 1 });
		var playbackAct = () => commands.Playback();

		// Assert
		addAct.Should().Throw<ObjectDisposedException>();
		playbackAct.Should().Throw<ObjectDisposedException>();
	}

	[Fact]
	public void CreateEntity_WhenGivenInitialComponents_ShouldCreateEntityWithValuesOnPlayback()
	{
		// Arrange
		var world    = new World();
		var commands = world.CreateCommandBuffer();
		commands.CreateEntity(
			new Position { X = 10, Y = 20 },
			new Velocity { X = 3, Y  = -1 }
		);

		// Act
		commands.Playback();

		// Assert
		var matched = 0;
		foreach (var chunk in world.Query().All<Position>().All<Velocity>())
		{
			var positions  = chunk.Components<Position>();
			var velocities = chunk.Components<Velocity>();
			for (var i = 0; i < chunk.Count; i++)
			{
				positions[i].Should().Be(new Position { X  = 10, Y = 20 });
				velocities[i].Should().Be(new Velocity { X = 3, Y  = -1 });
				matched++;
			}
		}

		matched.Should().Be(1);
	}

	[Fact]
	public void Playback_WhenCalledDuringQueryIteration_ShouldThrow()
	{
		// Arrange
		var world  = new World();
		var entity = world.Spawn();
		world.Add(entity, new Position { X = 0, Y = 0 });

		var commands = world.CreateCommandBuffer();
		commands.CreateEntity();

		// Act
		var act = () =>
		{
			foreach (var _ in world.Query().All<Position>())
				commands.Playback();
		};

		// Assert
		act.Should().Throw<InvalidOperationException>()
		   .WithMessage("*Playback*");
	}

	[Fact]
	public void Playback_WhenCalledDuringUpdate_ShouldThrow()
	{
		// Arrange
		var world = new World();
		world.AddSystem(new PlaybackDuringUpdateSystem());

		// Act
		var act = () => world.Update(0.016f);

		// Assert
		act.Should().Throw<InvalidOperationException>()
		   .WithMessage("*Playback*");
	}

	[Fact]
	public void Playback_WhenFailing_ShouldKeepUnprocessedCommandsForRetry()
	{
		// Arrange
		var world    = new World();
		var existing = world.Spawn();
		world.Add(existing, new Position { X = 1, Y = 1 });

		var commands = world.CreateCommandBuffer();
		var temp     = commands.CreateEntity();
		commands.AddComponent(existing, new Position { X = 9, Y = 8 });
		commands.SetComponent(temp,     new Velocity { X = 3, Y = 4 });
		commands.SetComponent(existing, new Velocity { X = 7, Y = 0 });

		// Act
		var firstAttempt = () => commands.Playback();

		// Assert
		firstAttempt.Should().Throw<InvalidOperationException>();
		commands.HasCommands.Should().BeTrue();
		world.EntityCount.Should().Be(2);

		world.Remove<Position>(existing);
		commands.Playback();

		world.Get<Position>(existing).Should().Be(new Position { X = 9, Y = 8 });
		world.Get<Velocity>(existing).Should().Be(new Velocity { X = 7, Y = 0 });
		world.EntityCount.Should().Be(2);

		var velocityOnlyCount = 0;
		foreach (var chunk in world.Query().All<Velocity>().None<Position>())
			velocityOnlyCount += chunk.Count;

		velocityOnlyCount.Should().Be(1);
	}

	[Fact]
	public void RemoveComponent_WhenEntityIsNotAlive_ShouldThrowOnPlayback()
	{
		// Arrange
		var world  = new World();
		var entity = world.Spawn();
		world.Despawn(entity);

		var commands = world.CreateCommandBuffer();
		commands.RemoveComponent<Position>(entity);

		// Act
		var act = () => commands.Playback();

		// Assert
		act.Should().Throw<InvalidOperationException>();
	}

	[Fact]
	public void SetComponent_WhenComponentAlreadyExists_ShouldUpdateOnPlayback()
	{
		// Arrange
		var world  = new World();
		var entity = world.Spawn();
		world.Add(entity, new Position { X = 1, Y = 2 });
		var commands = world.CreateCommandBuffer();
		commands.SetComponent(entity, new Position { X = 9, Y = 8 });

		// Act
		commands.Playback();

		// Assert
		var component = world.Get<Position>(entity);
		component.X.Should().Be(9);
		component.Y.Should().Be(8);
	}

	private sealed class AddVelocitySystem : ISystem
	{
		private readonly Entity _entity;

		public AddVelocitySystem(Entity entity)
		{
			_entity = entity;
		}

		public ComponentAccess[] Accesses => [ComponentAccess.Read<Position>()];

		public SystemUpdateSettings UpdateSettings => SystemUpdateSettings.EveryFrame;

		public void Update(IWorld world, in SystemContext context)
		{
			if (world.Has<Velocity>(_entity)) return;

			context.Commands.AddComponent(_entity, new Velocity { X = 1, Y = 0 });
		}
	}

	private sealed class PlaybackDuringUpdateSystem : ISystem
	{
		public ComponentAccess[]    Accesses       => [];
		public SystemUpdateSettings UpdateSettings => SystemUpdateSettings.EveryFrame;

		public void Update(IWorld world, in SystemContext context) => context.Commands.Playback();
	}

	private struct Position : IComponent
	{
		public float X;
		public float Y;
	}

	private struct Velocity : IComponent
	{
		public float X;
		public float Y;
	}
}
