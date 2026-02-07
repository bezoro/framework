using System;
using Bezoro.ECS.Abstractions;
using Bezoro.ECS.Options;
using Bezoro.ECS.Services;
using Bezoro.ECS.Types;
using FluentAssertions;
using JetBrains.Annotations;
using Xunit;

namespace Bezoro.ECS.Tests.Services;

[TestSubject(typeof(SystemManager))]
public class SystemManagerTests
{
	[Fact]
	public void UpdateAll_Should_Respect_Update_Frequency()
	{
		// Arrange
		var world  = new World();
		var system = new FixedStepSystem();
		world.AddSystem(system);

		// Act
		world.Tick(0.2f);
		world.Tick(0.29f);
		world.Tick(0.31f);

		// Assert
		system.UpdateCount.Should().Be(1);
		system.LastDeltaTime.Should().BeApproximately(0.5f, 0.0001f);
	}

	[Fact]
	public void UpdateAll_ShouldRespectWriteReadDependenciesAcrossBatches()
	{
		// Arrange
		var world  = new World(new WorldOptions { MaxDegreeOfParallelism = 1 });
		var entity = world.Spawn();
		world.Add(entity, new Counter { Value = 1 });

		var preRead  = new ReadCounterSystem();
		var write    = new WriteCounterSystem(2);
		var postRead = new ReadCounterSystem();

		world.AddSystem(preRead);
		world.AddSystem(write);
		world.AddSystem(postRead);

		// Act
		world.Tick(1f / 60f);

		// Assert
		preRead.LastObserved.Should().Be(1);
		postRead.LastObserved.Should().Be(2);
	}

	[Fact]
	public void UpdateAll_When_DeltaTime_Is_Large_Should_Cap_Catch_Up_Ticks()
	{
		// Arrange
		var world  = new World();
		var system = new FixedStepSystem();
		world.AddSystem(system);

		// Act
		world.Tick(10f);
		world.Tick(0f);
		world.Tick(0f);
		world.Tick(0f);

		// Assert
		system.UpdateCount.Should().Be(3);
		system.LastDeltaTime.Should().BeApproximately(0.5f, 0.0001f);
	}

	[Fact]
	public void UpdateAll_WhenFixedStepSystemsUseDifferentPhases_ShouldAccumulateIndependently()
	{
		var world        = new World();
		var updateSystem = new PhaseCounterSystem(SystemLoopPhase.Tick,      SystemUpdateSettings.FixedInterval(0.5f));
		var fixedSystem  = new PhaseCounterSystem(SystemLoopPhase.FixedTick, SystemUpdateSettings.FixedInterval(0.5f));

		world.AddSystem(updateSystem);
		world.AddSystem(fixedSystem);

		world.Tick(0.3f);
		world.FixedTick(0.3f);
		world.Tick(0.3f);
		world.FixedTick(0.3f);

		updateSystem.UpdateCount.Should().Be(1);
		updateSystem.LastDeltaTime.Should().BeApproximately(0.5f, 0.0001f);
		fixedSystem.UpdateCount.Should().Be(1);
		fixedSystem.LastDeltaTime.Should().BeApproximately(0.5f, 0.0001f);
	}

	[Fact]
	public void UpdateAll_WhenParallelSystemThrows_ShouldRethrowOriginalException()
	{
		var world = new World(new WorldOptions { MaxDegreeOfParallelism = 4 });
		world.AddSystem(new NoOpSystem());
		world.AddSystem(new ThrowingSystem());

		var act = () => world.Tick(1f / 60f);

		act.Should().Throw<InvalidOperationException>()
		   .WithMessage("system-fail");
	}

	[Fact]
	public void UpdateAll_WhenPlanIsDirty_ShouldBuildPlanOnceAndReuseAcrossFrames()
	{
		// Arrange
		var world = new World();
		world.AddSystem(new ReadCounterSystem());
		world.AddSystem(new WriteCounterSystem(3));

		// Assert precondition
		world.SchedulerPlanBuildCount.Should().Be(0);

		// Act
		world.Tick(1f / 60f);
		world.Tick(1f / 60f);
		world.Tick(1f / 60f);

		// Assert
		world.SchedulerPlanBuildCount.Should().Be(1);
	}

	[Fact]
	public void UpdateAll_WhenSystemCapturesCommands_ShouldDisposeBufferAfterFlush()
	{
		var world  = new World();
		var system = new CommandCaptureSystem();
		world.AddSystem(system);

		world.Tick(1f / 60f);

		world.EntityCount.Should().Be(1);
		system.Captured.Should().NotBeNull();
		var act = () => system.Captured!.CreateEntity();
		act.Should().Throw<ObjectDisposedException>();
	}

	[Fact]
	public void UpdateAll_WhenSystemLoopPhaseIsFixedUpdate_ShouldNotRunDuringUpdate()
	{
		var world  = new World();
		var system = new PhaseCounterSystem(SystemLoopPhase.FixedTick, SystemUpdateSettings.EveryTick);
		world.AddSystem(system);

		world.Tick(1f / 60f);

		system.UpdateCount.Should().Be(0);
	}

	[Fact]
	public void UpdateAll_WhenSystemLoopPhaseIsLateUpdate_ShouldRunOnlyDuringLateUpdate()
	{
		var world  = new World();
		var system = new PhaseCounterSystem(SystemLoopPhase.LateTick, SystemUpdateSettings.EveryTick);
		world.AddSystem(system);

		world.Tick(1f / 60f);
		world.FixedTick(1f / 50f);
		world.LateTick(1f / 60f);

		system.UpdateCount.Should().Be(1);
		system.LastDeltaTime.Should().BeApproximately(1f / 60f, 0.0001f);
	}

	[Fact]
	public void UpdateAll_WhenSystemsChangeAfterFirstUpdate_ShouldRebuildPlanOnNextUpdate()
	{
		// Arrange
		var world = new World();
		world.AddSystem(new ReadCounterSystem());
		world.Tick(1f / 60f);
		world.SchedulerPlanBuildCount.Should().Be(1);

		// Act
		world.AddSystem(new WriteCounterSystem(5));
		world.SchedulerPlanBuildCount.Should().Be(1);
		world.Tick(1f / 60f);

		// Assert
		world.SchedulerPlanBuildCount.Should().Be(2);
	}

	private sealed class CommandCaptureSystem : ISystem
	{
		public CommandBuffer? Captured { get; private set; }

		public void Update(IWorld world, in SystemContext context)
		{
			Captured = context.Commands;
			context.Commands.CreateEntity();
		}
	}

	private readonly struct Counter : IComponent
	{
		public int Value { get; init; }
	}

	private sealed class FixedStepSystem : ISystem
	{
		public ComponentAccess[] Accesses => [];

		public SystemUpdateSettings UpdateSettings => SystemUpdateSettings.FixedInterval(0.5f);
		public float                LastDeltaTime  { get; private set; }
		public int                  UpdateCount    { get; private set; }

		public void Update(IWorld world, in SystemContext context)
		{
			UpdateCount++;
			LastDeltaTime = context.DeltaTime;
		}
	}

	private sealed class NoOpSystem : ISystem
	{
		public void Update(IWorld world, in SystemContext context) { }
	}

	private sealed class PhaseCounterSystem(SystemLoopPhase loopPhase, SystemUpdateSettings updateSettings) : ISystem
	{
		public SystemLoopPhase      LoopPhase      { get; } = loopPhase;
		public SystemUpdateSettings UpdateSettings { get; } = updateSettings;
		public float                LastDeltaTime  { get; private set; }
		public int                  UpdateCount    { get; private set; }

		public void Update(IWorld world, in SystemContext context)
		{
			UpdateCount++;
			LastDeltaTime = context.DeltaTime;
		}
	}

	private sealed class ReadCounterSystem : ISystem
	{
		public ComponentAccess[] Accesses => [ComponentAccess.Read<Counter>()];

		public SystemUpdateSettings UpdateSettings => SystemUpdateSettings.EveryTick;
		public int                  LastObserved   { get; private set; } = -1;

		public void Update(IWorld world, in SystemContext context)
		{
			foreach (var chunk in world.Query().All<Counter>())
			{
				var counters = chunk.Components<Counter>();
				if (chunk.Count > 0)
					LastObserved = counters[0].Value;
			}
		}
	}

	private sealed class ThrowingSystem : ISystem
	{
		public void Update(IWorld world, in SystemContext context) =>
			throw new InvalidOperationException("system-fail");
	}

	private sealed class WriteCounterSystem(int value) : ISystem
	{
		public ComponentAccess[] Accesses => [ComponentAccess.Write<Counter>()];

		public SystemUpdateSettings UpdateSettings => SystemUpdateSettings.EveryTick;

		public void Update(IWorld world, in SystemContext context)
		{
			foreach (var chunk in world.Query().All<Counter>())
			{
				var counters = chunk.Components<Counter>();
				for (var i = 0; i < chunk.Count; i++)
					counters[i] = new() { Value = value };
			}
		}
	}
}
