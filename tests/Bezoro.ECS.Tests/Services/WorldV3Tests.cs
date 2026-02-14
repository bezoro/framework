using System;
using Bezoro.ECS.Abstractions;
using Bezoro.ECS.Attributes;
using Bezoro.ECS.Services;
using Bezoro.ECS.Types;
using FluentAssertions;
using JetBrains.Annotations;
using Xunit;

namespace Bezoro.ECS.Tests.Services;

[TestSubject(typeof(WorldV3))]
public class WorldV3Tests
{
	[Fact]
	public void Tick_WhenSystemsAreIndependent_ShouldExecuteInSingleWave()
	{
		using var world = new WorldV3(new()
		{
			EntityCapacity = 16,
			ComponentTypeCapacity = 16,
			CommandCapacity = 64,
			CommandPayloadCapacityPerType = 64,
			QueryResultCapacity = 16,
			ParallelWorkerCount = 4
		});

		var entity = world.CreateEntity();
		world.Set(entity, new Position { X = 1, Y = 2 });
		world.Set(entity, new Health { Value = 3 });

		world.AddSystem(new SetPositionSystem(entity, 10));
		world.AddSystem(new SetHealthSystem(entity, 77));

		world.Tick(0.016f);

		world.Get<Position>(entity).X.Should().Be(10);
		world.Get<Health>(entity).Value.Should().Be(77);
		var diagnostics = world.GetSchedulerDiagnostics();
		diagnostics.RegisteredSystems.Should().Be(2);
		diagnostics.WaveCount.Should().Be(1);
		diagnostics.MaxWaveWidth.Should().Be(2);
	}

	[Fact]
	public void Tick_WhenSystemsConflictOnComponent_ShouldSplitIntoMultipleWaves()
	{
		using var world = new WorldV3(new()
		{
			EntityCapacity = 16,
			ComponentTypeCapacity = 16,
			CommandCapacity = 64,
			CommandPayloadCapacityPerType = 64,
			QueryResultCapacity = 16,
			ParallelWorkerCount = 4
		});

		var entity = world.CreateEntity();
		world.Set(entity, new Position { X = 1, Y = 2 });
		world.Set(entity, new Health { Value = 0 });

		world.AddSystem(new SetPositionSystem(entity, 5));
		world.AddSystem(new ReadPositionWriteHealthSystem(entity));

		world.Tick(0.016f);

		world.Get<Health>(entity).Value.Should().Be(5);
		var diagnostics = world.GetSchedulerDiagnostics();
		diagnostics.RegisteredSystems.Should().Be(2);
		diagnostics.WaveCount.Should().Be(2);
		diagnostics.MaxWaveWidth.Should().Be(1);
	}

	[Fact]
	public void Tick_WhenSystemsConflictOnWrite_ShouldPreserveRegistrationOrderDeterministically()
	{
		using var world = new WorldV3(new()
		{
			EntityCapacity = 16,
			ComponentTypeCapacity = 16,
			CommandCapacity = 64,
			CommandPayloadCapacityPerType = 64,
			QueryResultCapacity = 16,
			ParallelWorkerCount = 4
		});

		var entity = world.CreateEntity();
		world.Set(entity, new Position { X = 1, Y = 2 });

		world.AddSystem(new SetPositionSystem(entity, 11));
		world.AddSystem(new SetPositionSystem(entity, 29));

		world.Tick(0.016f);

		world.Get<Position>(entity).X.Should().Be(29);
		var diagnostics = world.GetSchedulerDiagnostics();
		diagnostics.WaveCount.Should().Be(2);
	}

	[Fact]
	public void Run_WhenExecutingHotPathAfterWarmup_ShouldNotAllocate()
	{
		using var world = new WorldV3(new()
		{
			EntityCapacity = 1024,
			ComponentTypeCapacity = 16,
			CommandCapacity = 2048,
			CommandPayloadCapacityPerType = 2048,
			QueryResultCapacity = 1024
		});

		for (var i = 0; i < 256; i++)
		{
			var entity = world.CreateEntity();
			world.Set(entity, new Position { X = i, Y = i });
			world.Set(entity, new Velocity { X = 1, Y = -1 });
		}

		var handle = world.Compile<PositionVelocityQuerySpec>();
		world.Run<PositionVelocityQuerySpec, IntegrateJob, Position, Velocity>(handle, new(0.016f));

		GC.Collect();
		GC.WaitForPendingFinalizers();
		GC.Collect();

		long before = GC.GetAllocatedBytesForCurrentThread();
		for (var i = 0; i < 200; i++)
			world.Run<PositionVelocityQuerySpec, IntegrateJob, Position, Velocity>(handle, new(0.016f));

		long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
		allocated.Should().Be(0);
	}

	[Fact]
	public void Run_WhenQueryResultCapacityOverflowsWithFailFast_ShouldThrow()
	{
		using var world = new WorldV3(new()
		{
			EntityCapacity = 64,
			ComponentTypeCapacity = 16,
			CommandCapacity = 256,
			CommandPayloadCapacityPerType = 256,
			QueryResultCapacity = 8,
			OverflowPolicy = WorldOverflowPolicy.FailFast
		});

		for (var i = 0; i < 16; i++)
		{
			var entity = world.CreateEntity();
			world.Set(entity, new Position { X = i, Y = i });
			world.Set(entity, new Velocity { X = 1, Y = -1 });
		}

		var handle = world.Compile<PositionVelocityQuerySpec>();
		var action = () => world.Run<PositionVelocityQuerySpec, IntegrateJob, Position, Velocity>(handle, new(1f));

		action.Should().Throw<InvalidOperationException>()
			.WithMessage("*Query result capacity*");
	}

	[Fact]
	public void Run_WhenQueryResultCapacityOverflowsWithDropNewest_ShouldProcessUpToCapacity()
	{
		using var world = new WorldV3(new()
		{
			EntityCapacity = 64,
			ComponentTypeCapacity = 16,
			CommandCapacity = 256,
			CommandPayloadCapacityPerType = 256,
			QueryResultCapacity = 8,
			OverflowPolicy = WorldOverflowPolicy.DropNewest
		});

		var entities = new Entity[16];
		for (var i = 0; i < entities.Length; i++)
		{
			entities[i] = world.CreateEntity();
			world.Set(entities[i], new Position { X = i, Y = i });
			world.Set(entities[i], new Velocity { X = 1, Y = -1 });
		}

		var handle = world.Compile<PositionVelocityQuerySpec>();
		world.Run<PositionVelocityQuerySpec, IntegrateJob, Position, Velocity>(handle, new(1f));

		var updatedCount = 0;
		for (var i = 0; i < entities.Length; i++)
		{
			ref var position = ref world.Get<Position>(entities[i]);
			if (position.X == i + 1)
				updatedCount++;
		}

		updatedCount.Should().Be(8);
	}

	private readonly struct PositionVelocityQuerySpec : ICompiledQuerySpec
	{
		public void Build(ref QueryBuilder builder)
		{
			builder.All<Position>();
			builder.All<Velocity>();
		}
	}

	private readonly struct IntegrateJob(float dt) : IForEach<Position, Velocity>
	{
		public void Execute(ref Position component1, in Velocity component2)
		{
			component1.X += component2.X * dt;
			component1.Y += component2.Y * dt;
		}
	}

	[WritesAttribute<Position>]
	private sealed class SetPositionSystem(Entity entity, int value) : ISystemV3
	{
		public void Update(in SystemContextV3 context) =>
			context.Commands.Set(entity, new Position { X = value, Y = value });
	}

	[WritesAttribute<Health>]
	private sealed class SetHealthSystem(Entity entity, int value) : ISystemV3
	{
		public void Update(in SystemContextV3 context) =>
			context.Commands.Set(entity, new Health { Value = value });
	}

	[ReadsAttribute<Position>]
	[WritesAttribute<Health>]
	private sealed class ReadPositionWriteHealthSystem(Entity entity) : ISystemV3
	{
		public void Update(in SystemContextV3 context)
		{
			context.World.TryGet(entity, out Position position).Should().BeTrue();
			context.Commands.Set(entity, new Health { Value = (int)position.X });
		}
	}

	private struct Position
	{
		public float X;
		public float Y;
	}

	private struct Velocity
	{
		public float X;
		public float Y;
	}

	private struct Health
	{
		public int Value;
	}
}
