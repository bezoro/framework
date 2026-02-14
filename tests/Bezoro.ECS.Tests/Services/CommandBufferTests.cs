using System;
using Bezoro.ECS.Abstractions;
using Bezoro.ECS.Attributes;
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
		var world  = new WorldV1();
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
		var world  = new WorldV1();
		var entity = world.Spawn();
		world.Add(entity, new Position { X = 1, Y = 2 });
		world.AddSystem(new AddVelocitySystem(entity));

		// Act
		world.Has<Velocity>(entity).Should().BeFalse();
		world.Tick(0.016f);

		// Assert
		world.Has<Velocity>(entity).Should().BeTrue();
	}

	[Fact]
	public void CommandBuffer_WhenDisposed_ShouldRejectFurtherUsage()
	{
		// Arrange
		var world    = new WorldV1();
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
		var world    = new WorldV1();
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
		var world  = new WorldV1();
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
		var world = new WorldV1();
		world.AddSystem(new PlaybackDuringUpdateSystem());

		// Act
		var act = () => world.Tick(0.016f);

		// Assert
		act.Should().Throw<InvalidOperationException>()
		   .WithMessage("*Playback*");
	}

	[Fact]
	public void Playback_WhenFailing_ShouldKeepUnprocessedCommandsForRetry()
	{
		// Arrange
		var world    = new WorldV1();
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
		var world  = new WorldV1();
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
		var world  = new WorldV1();
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

	[Fact]
	public void Playback_WhenLargeBatchCompletes_ShouldRetainCommandStorageForReuse()
	{
		var world    = new WorldV1();
		var commands = world.CreateCommandBuffer();

		for (var i = 0; i < 50_000; i++)
			commands.CreateEntity(new Position { X = i, Y = i });

		commands.Playback();

		commands.HasCommands.Should().BeFalse();
		commands.HasAllocatedStorage.Should().BeTrue();
	}

	[Fact]
	public void Record_WhenLargeBatchRunsRepeatedly_ShouldAllocateLessOnSecondRecordingPass()
	{
		var world    = new WorldV1();
		var commands = world.CreateCommandBuffer();

		RecordCreateBurst(commands, count: 256);
		commands.Playback();
		world.Clear();

		const int BURST_SIZE = 50_000;
		long firstPassAllocatedBytes  = MeasureRecordCreateBurstAllocation(commands, BURST_SIZE);
		commands.Playback();
		world.Clear();

		long secondPassAllocatedBytes = MeasureRecordCreateBurstAllocation(commands, BURST_SIZE);
		commands.Playback();
		world.Clear();

		secondPassAllocatedBytes.Should().BeLessThan(firstPassAllocatedBytes / 4);
	}

	[Fact]
	public void CreateEntity_WhenComponentContainsManagedReference_ShouldApplyDuringPlayback()
	{
		var world    = new WorldV1();
		var commands = world.CreateCommandBuffer();
		var payload  = new object();
		commands.CreateEntity(new ManagedPayload { Value = payload });

		commands.Playback();

		var found = false;
		foreach (var chunk in world.Query().All<ManagedPayload>())
		{
			var payloads = chunk.Components<ManagedPayload>();
			for (var i = 0; i < chunk.Count; i++)
			{
				payloads[i].Value.Should().BeSameAs(payload);
				found = true;
			}
		}

		found.Should().BeTrue();
	}

	[Reads<Position>]
	private sealed class AddVelocitySystem(Entity entity) : ISystem
	{
		public SystemUpdateSettings UpdateSettings => SystemUpdateSettings.EveryTick;

		public void Update(IWorld world, in SystemContext context)
		{
			if (world.Has<Velocity>(entity)) return;

			context.Commands.AddComponent(entity, new Velocity { X = 1, Y = 0 });
		}
	}

	private sealed class PlaybackDuringUpdateSystem : ISystem
	{
		public SystemUpdateSettings UpdateSettings => SystemUpdateSettings.EveryTick;

		public void Update(IWorld world, in SystemContext context) => context.Commands.Playback();
	}

	private struct Position	{
		public float X;
		public float Y;
	}

	private struct Velocity	{
		public float X;
		public float Y;
	}

	private struct ManagedPayload
	{
		public object? Value;
	}

	private static void RecordCreateBurst(CommandBuffer commands, int count)
	{
		for (var i = 0; i < count; i++)
			commands.CreateEntity(new Position { X = i, Y = i });
	}

	private static long MeasureRecordCreateBurstAllocation(CommandBuffer commands, int count)
	{
		long before = GC.GetAllocatedBytesForCurrentThread();
		RecordCreateBurst(commands, count);
		long after = GC.GetAllocatedBytesForCurrentThread();
		return after - before;
	}
}
