using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Bezoro.ECS.Abstractions;
using Bezoro.ECS.Services;
using Bezoro.ECS.Types;
using Xunit;

namespace Bezoro.ECS.Tests.Services;

public sealed class WorldGroupTests
{
	[Fact]
	public void Construction_CopiesMembershipAndRejectsDuplicatesAndNulls()
	{
		using var first = CreateWorld();
		using var second = CreateWorld();
		var input = new[] { first, second };
		var group = new WorldGroup(input, 2);
		input[0] = second;
		Assert.Same(first, group.Worlds[0]);
		Assert.Same(second, group.Worlds[1]);
		Assert.Throws<ArgumentException>(() => new WorldGroup(new[] { first, first }, 2));
		Assert.Throws<ArgumentException>(() => new WorldGroup(new[] { first, null! }, 2));
		Assert.Throws<ArgumentNullException>(() => new WorldGroup(null!, 2));
		Assert.Throws<ArgumentOutOfRangeException>(() => new WorldGroup(new[] { first }, 0));
	}

	[Fact]
	public void ParallelAndSequentialGroups_ProduceIdenticalIndependentComponentsResourcesAndCommands()
	{
		var sequential = new World[4];
		var parallel = new World[4];
		try
		{
			for (int index = 0; index < sequential.Length; index++)
			{
				sequential[index] = CreateSimulation(index);
				parallel[index] = CreateSimulation(index);
			}
			var sequentialGroup = new WorldGroup(sequential);
			var parallelGroup = new WorldGroup(parallel, 4);
			for (int tick = 0; tick < 12; tick++)
			{
				sequentialGroup.Tick(0.125f);
				parallelGroup.Tick(0.125f);
			}

			for (int index = 0; index < sequential.Length; index++)
			{
				Assert.Equal(sequential[index].EntityCount, parallel[index].EntityCount);
				Assert.Equal(sequential[index].ReadResource<Counter>().Value, parallel[index].ReadResource<Counter>().Value);
				for (int id = 0; id < sequential[index].EntityCount; id++)
					Assert.Equal(sequential[index].Read<Position>(new Entity(id, 0)).Value,
						parallel[index].Read<Position>(new Entity(id, 0)).Value);
			}
		}
		finally
		{
			foreach (var world in sequential) world?.Dispose();
			foreach (var world in parallel) world?.Dispose();
		}
	}

	[Fact]
	public async Task RunningGroup_RejectsConcurrentCallsAndWaitsForWorkers()
	{
		using var entered = new CountdownEvent(2);
		using var release = new ManualResetEventSlim();
		using var first = CreateWorld();
		using var second = CreateWorld();
		first.AddSystem(new CallbackSystem(() => Block(entered, release)));
		second.AddSystem(new CallbackSystem(() => Block(entered, release)));
		var group = new WorldGroup(new[] { first, second }, 2);
		Task running = Task.Run(() => group.Tick(0.1f));
		try
		{
			Assert.True(entered.Wait(TimeSpan.FromSeconds(10)));
			Assert.Throws<InvalidOperationException>(() => group.FixedTick(0.1f));
			Assert.False(running.IsCompleted);
		}
		finally { release.Set(); }
		await running.WaitAsync(TimeSpan.FromSeconds(10));
	}

	[Fact]
	public async Task Cancellation_JoinsStartedWorldsAndSkipsUndispatchedWorlds()
	{
		using var entered = new CountdownEvent(2);
		using var release = new ManualResetEventSlim();
		using var cancellation = new CancellationTokenSource();
		using var first = CreateWorld();
		using var second = CreateWorld();
		using var third = CreateWorld();
		int completed = 0;
		int thirdSteps = 0;
		Action block = () => { Block(entered, release); Interlocked.Increment(ref completed); };
		first.AddSystem(new CallbackSystem(block));
		second.AddSystem(new CallbackSystem(block));
		third.AddSystem(new CallbackSystem(() => thirdSteps++));
		var group = new WorldGroup(new[] { first, second, third }, 2);
		Task running = Task.Run(() => group.Tick(0.1f, cancellation.Token));
		try
		{
			Assert.True(entered.Wait(TimeSpan.FromSeconds(10)));
			cancellation.Cancel();
			Assert.False(running.IsCompleted);
		}
		finally { release.Set(); }
		await Assert.ThrowsAnyAsync<OperationCanceledException>(() => running.WaitAsync(TimeSpan.FromSeconds(10)));
		Assert.Equal(2, completed);
		Assert.Equal(0, thirdSteps);
	}

	[Fact]
	public async Task WorkerFailure_JoinsOtherWorkersAndReleasesTheGroupForReuse()
	{
		using var started = new CountdownEvent(2);
		using var failNow = new ManualResetEventSlim();
		using var finishSecond = new ManualResetEventSlim();
		using var failureThrown = new ManualResetEventSlim();
		using var first = CreateWorld();
		using var second = CreateWorld();
		bool fail = true;
		bool secondCompleted = false;
		var expected = new InvalidOperationException("Independent world failed.");
		first.AddSystem(new CallbackSystem(() =>
		{
			if (!fail) return;
			started.Signal();
			if (!failNow.Wait(TimeSpan.FromSeconds(10))) throw new TimeoutException();
			failureThrown.Set();
			throw expected;
		}));
		second.AddSystem(new CallbackSystem(() =>
		{
			if (fail) Block(started, finishSecond);
			secondCompleted = true;
		}));
		var group = new WorldGroup(new[] { first, second }, 2);
		Task running = Task.Run(() => group.Tick(0.1f));
		try
		{
			Assert.True(started.Wait(TimeSpan.FromSeconds(10)));
			failNow.Set();
			Assert.True(failureThrown.Wait(TimeSpan.FromSeconds(10)));
			Assert.False(running.IsCompleted);
		}
		finally { failNow.Set(); finishSecond.Set(); }
		Assert.Same(expected, await Assert.ThrowsAsync<InvalidOperationException>(() => running.WaitAsync(TimeSpan.FromSeconds(10))));
		Assert.True(secondCompleted);
		fail = false;
		group.Tick(0.1f);
	}

	[Fact]
	public void DisposedWorldAndPreCanceledToken_AreRejectedBeforeAnyMutation()
	{
		using var first = CreateWorld();
		var second = CreateWorld();
		int updates = 0;
		first.AddSystem(new CallbackSystem(() => updates++));
		var group = new WorldGroup(new[] { first, second }, 2);
		using var cancellation = new CancellationTokenSource();
		cancellation.Cancel();
		Assert.ThrowsAny<OperationCanceledException>(() => group.Tick(0.1f, cancellation.Token));
		Assert.Equal(0, updates);
		second.Dispose();
		Assert.Throws<ObjectDisposedException>(() => group.Tick(0.1f));
		Assert.Equal(0, updates);
	}

	[Theory]
	[InlineData(float.NaN)]
	[InlineData(float.PositiveInfinity)]
	[InlineData(-1f)]
	public void InvalidElapsedTime_DoesNotRunWorlds(float deltaTime)
	{
		using var world = CreateWorld();
		int updates = 0;
		world.AddSystem(new CallbackSystem(() => updates++));
		Assert.Throws<ArgumentOutOfRangeException>(() => new WorldGroup(new[] { world }, 2).Tick(deltaTime));
		Assert.Equal(0, updates);
	}

	[Fact]
	public void LoopPhases_AreForwardedToTheCorrespondingSystems()
	{
		using var world = CreateWorld();
		var phases = new List<SystemLoopPhase>();
		world.AddSystem(new CallbackSystem(() => phases.Add(SystemLoopPhase.Tick)));
		world.AddSystem(new CallbackSystem(() => phases.Add(SystemLoopPhase.FixedTick), SystemLoopPhase.FixedTick));
		world.AddSystem(new CallbackSystem(() => phases.Add(SystemLoopPhase.LateTick), SystemLoopPhase.LateTick));
		var group = new WorldGroup(new[] { world });
		group.Tick(0.1f);
		group.FixedTick(0.1f);
		group.LateTick(0.1f);
		Assert.Equal(new[] { SystemLoopPhase.Tick, SystemLoopPhase.FixedTick, SystemLoopPhase.LateTick }, phases);
	}

	private static void Block(CountdownEvent entered, ManualResetEventSlim release)
	{
		entered.Signal();
		if (!release.Wait(TimeSpan.FromSeconds(10))) throw new TimeoutException("Test worker was not released.");
	}

	private static World CreateWorld() => new(new WorldConfig
	{
		EntityCapacity = 32, ComponentTypeCapacity = 8, QueryResultCapacity = 32,
		CommandCapacity = 32, CommandPayloadCapacityPerType = 32, ChunkCapacity = 8,
		MaxDegreeOfParallelism = 1
	});

	private static World CreateSimulation(int seed)
	{
		var world = CreateWorld();
		world.SetResource(new Counter());
		world.Spawn(new Position { Value = seed });
		world.AddSystem(new SimulationSystem());
		return world;
	}

	private sealed class CallbackSystem(Action callback, SystemLoopPhase phase = SystemLoopPhase.Tick) : ISystem
	{
		public SystemLoopPhase LoopPhase => phase;
		public void Update(in SystemContext context) => callback();
	}
	private struct Position { public float Value; }
	private struct Counter { public int Value; }
	private readonly struct PositionQuery : ICompiledQuerySpec
	{
		public void Build(ref QueryBuilder builder) => builder.All<Position>();
	}
	private sealed class SimulationSystem : ISystem
	{
		public void Update(in SystemContext context)
		{
			float delta = context.DeltaTime;
			context.World.Query<PositionQuery>().ForEach((Entity _, ref Position position) => position.Value += delta);
			int step = ++context.World.WriteResource<Counter>().Value;
			context.CommandStream.CreateEntity(new Position { Value = step });
		}
	}
}
