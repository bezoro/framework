using System;
using System.Threading;
using Bezoro.ECS.Abstractions;
using Bezoro.ECS.Attributes;
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
	public void UpdateAll_WhenSystemDoesNotRecordCommands_ShouldKeepCommandStorageUnallocated()
	{
		var world  = new World();
		var system = new CommandBufferAllocationProbeSystem();
		world.AddSystem(system);

		world.Tick(1f / 60f);

		system.RecordedCommands.Should().Be(0);
	}

	[Fact]
	public void UpdateAll_WhenReusingCommandReferenceFromPreviousTick_ShouldThrowObjectDisposedException()
	{
		var world  = new World();
		var system = new StaleCommandBufferReferenceSystem();
		world.AddSystem(system);

		world.Tick(1f / 60f);
		world.Tick(1f / 60f);

		system.ReuseException.Should().BeOfType<ObjectDisposedException>();
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

	[Fact]
	public void UpdateAll_WhenSystemsHaveNoMetadata_ShouldRunInParallel()
	{
		// Systems with no declared access metadata default to non-exclusive and may run concurrently.
		var world = new World(new WorldOptions { MaxDegreeOfParallelism = 4 });
		var probe = new ConcurrencyProbe();
		world.AddSystem(new UndeclaredAccessProbeSystem(probe));
		world.AddSystem(new UndeclaredAccessProbeSystem(probe));

		world.Tick(1f / 60f);

		probe.MaxConcurrent.Should().BeGreaterThan(1);
	}

	[Fact]
	public void UpdateAll_WhenSystemsDeclareReadMetadata_ShouldAllowParallelExecution()
	{
		var world = new World(new WorldOptions { MaxDegreeOfParallelism = 4 });
		var probe = new ConcurrencyProbe();
		world.AddSystem(new DeclaredReadProbeSystem(probe));
		world.AddSystem(new DeclaredReadProbeSystem(probe));

		world.Tick(1f / 60f);

		probe.MaxConcurrent.Should().BeGreaterThan(1);
	}

	[Fact]
	public void UpdateAll_WhenExclusiveSystemIsRegistered_ShouldRunAloneEvenAlongsideUndeclaredSystems()
	{
		// [Exclusive] systems must still run alone; fix 2a only affects undeclared systems.
		var world = new World(new WorldOptions { MaxDegreeOfParallelism = 4 });
		var probe = new ConcurrencyProbe();

		world.AddSystem(new UndeclaredAccessProbeSystem(probe));
		world.AddSystem(new ExclusiveProbeSystem(probe));

		world.Tick(1f / 60f);

		// The exclusive system forces a batch break, so no two systems run at the same time.
		probe.MaxConcurrent.Should().Be(1);
	}

	[Fact]
	public void UpdateAll_WhenUndeclaredAndDeclaredReadSystemsShareStage_ShouldBatchTogether()
	{
		// After fix 2a: undeclared systems default to isExclusive=false.
		// An undeclared system and a [Reads<Counter>] system have no conflicts, so they run in the same batch.
		var world = new World(new WorldOptions { MaxDegreeOfParallelism = 4 });
		var probe = new ConcurrencyProbe();

		world.AddSystem(new UndeclaredAccessProbeSystem(probe));
		world.AddSystem(new DeclaredReadProbeSystem(probe));

		world.Tick(1f / 60f);

		// Both systems have no write conflicts and neither is exclusive, so they should run concurrently.
		probe.MaxConcurrent.Should().BeGreaterThan(1);
	}

	[Fact]
	public void UpdateAll_WhenSystemReentersTick_ShouldThrowInvalidOperationException()
	{
		var world = new World();
		var system = new ReentrantTickSystem();
		world.AddSystem(system);

		world.Tick(1f / 60f);

		system.ReentrantException.Should().BeOfType<InvalidOperationException>();
		system.UpdateCount.Should().Be(1);
	}

	[Fact]
	public void UpdateAll_WhenReentrantTickAttemptFails_ShouldAllowSubsequentTicks()
	{
		var world = new World();
		var system = new ReentrantTickSystem();
		world.AddSystem(system);

		world.Tick(1f / 60f);
		world.Tick(1f / 60f);

		system.ReentrantException.Should().BeOfType<InvalidOperationException>();
		system.UpdateCount.Should().Be(2);
	}

	private sealed class CommandCaptureSystem : ISystem
	{
		public CommandStream? Captured { get; private set; }

		public void Update(in SystemContext context)
		{
			Captured = context.Commands;
			context.Commands.CreateEntity();
		}
	}

	private sealed class StaleCommandBufferReferenceSystem : ISystem
	{
		private CommandStream? _previous;

		public Exception? ReuseException { get; private set; }

		public void Update(in SystemContext context)
		{
			if (_previous is not null && ReuseException is null)
			{
				try
				{
					_previous.CreateEntity();
				}
				catch (Exception ex)
				{
					ReuseException = ex;
				}
			}

			_previous = context.Commands;
		}
	}

	private sealed class CommandBufferAllocationProbeSystem : ISystem
	{
		public int RecordedCommands { get; private set; }

		public void Update(in SystemContext context)
		{
			RecordedCommands = context.Commands.GetDiagnostics().RecordedCommands;
		}
	}

	private readonly struct Counter	{
		public int Value { get; init; }
	}

	private sealed class FixedStepSystem : ISystem
	{
		public SystemUpdateSettings UpdateSettings => SystemUpdateSettings.FixedInterval(0.5f);
		public float                LastDeltaTime  { get; private set; }
		public int                  UpdateCount    { get; private set; }

		public void Update(in SystemContext context)
		{
			UpdateCount++;
			LastDeltaTime = context.DeltaTime;
		}
	}

	private sealed class NoOpSystem : ISystem
	{
		public void Update(in SystemContext context) { }
	}

	private sealed class ReentrantTickSystem : ISystem
	{
		private bool _attempted;

		public Exception? ReentrantException { get; private set; }
		public int UpdateCount { get; private set; }

		public void Update(in SystemContext context)
		{
			UpdateCount++;
			if (_attempted) return;

			_attempted = true;
			try
			{
				context.World.Tick(0f);
			}
			catch (Exception ex)
			{
				ReentrantException = ex;
			}
		}
	}

	private sealed class PhaseCounterSystem(SystemLoopPhase loopPhase, SystemUpdateSettings updateSettings) : ISystem
	{
		public SystemLoopPhase      LoopPhase      { get; } = loopPhase;
		public SystemUpdateSettings UpdateSettings { get; } = updateSettings;
		public float                LastDeltaTime  { get; private set; }
		public int                  UpdateCount    { get; private set; }

		public void Update(in SystemContext context)
		{
			UpdateCount++;
			LastDeltaTime = context.DeltaTime;
		}
	}

	[Reads<Counter>]
	private sealed class ReadCounterSystem : ISystem
	{
		private QueryHandle<CounterQuerySpec> _query;

		public SystemUpdateSettings UpdateSettings => SystemUpdateSettings.EveryTick;
		public int                  LastObserved   { get; private set; } = -1;

		public void OnCreate(World world)
		{
			if (world is null) throw new ArgumentNullException(nameof(world));
			_query = world.Compile<CounterQuerySpec>();
		}

		public void Update(in SystemContext context)
		{
			using var cursor = context.World.Execute(_query);
			if (!cursor.MoveNext() || cursor.Current.Length == 0)
				return;

			LastObserved = cursor.Get<Counter>(0).Value;
		}
	}

	private sealed class ThrowingSystem : ISystem
	{
		public void Update(in SystemContext context) =>
			throw new InvalidOperationException("system-fail");
	}

	private sealed class UndeclaredAccessProbeSystem(ConcurrencyProbe probe) : ISystem
	{
		public void Update(in SystemContext context) =>
			probe.Enter();
	}

	[Reads<Counter>]
	private sealed class DeclaredReadProbeSystem(ConcurrencyProbe probe) : ISystem
	{
		public void Update(in SystemContext context) =>
			probe.Enter();
	}

	[Exclusive]
	private sealed class ExclusiveProbeSystem(ConcurrencyProbe probe) : ISystem
	{
		public void Update(in SystemContext context) =>
			probe.Enter();
	}

	[Writes<Counter>]
	private sealed class WriteCounterSystem(int value) : ISystem
	{
		private QueryHandle<CounterQuerySpec> _query;

		public SystemUpdateSettings UpdateSettings => SystemUpdateSettings.EveryTick;

		public void OnCreate(World world)
		{
			if (world is null) throw new ArgumentNullException(nameof(world));
			_query = world.Compile<CounterQuerySpec>();
		}

		public void Update(in SystemContext context)
		{
			using var cursor = context.World.Execute(_query);
			if (!cursor.MoveNext())
				return;

			for (var i = 0; i < cursor.Current.Length; i++)
				cursor.Get<Counter>(i) = new() { Value = value };
		}
	}

	private readonly struct CounterQuerySpec : ICompiledQuerySpec
	{
		public void Build(ref QueryBuilder builder) => builder.All<Counter>();
	}

	private sealed class ConcurrencyProbe
	{
		private readonly ManualResetEventSlim _release = new(false);
		private          int                  _maxConcurrent;
		private          int                  _running;

		public int MaxConcurrent => Volatile.Read(ref _maxConcurrent);

		public void Enter()
		{
			int running = Interlocked.Increment(ref _running);
			UpdateMax(running);

			if (running == 1)
				_release.Wait(TimeSpan.FromMilliseconds(200));
			else
				_release.Set();

			Thread.Sleep(10);
			Interlocked.Decrement(ref _running);
		}

		private void UpdateMax(int candidate)
		{
			while (true)
			{
				int current = Volatile.Read(ref _maxConcurrent);
				if (candidate <= current)
					return;

				if (Interlocked.CompareExchange(ref _maxConcurrent, candidate, current) == current)
					return;
			}
		}
	}
}
