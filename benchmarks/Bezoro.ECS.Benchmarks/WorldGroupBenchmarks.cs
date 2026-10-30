using System;
using BenchmarkDotNet.Attributes;
using Bezoro.ECS.Abstractions;
using Bezoro.ECS.Services;
using Bezoro.ECS.Types;

namespace Bezoro.ECS.Benchmarks;

[MemoryDiagnoser]
public class WorldGroupBenchmarks
{
	private World[] _worlds = null!;
	private WorldGroup _sequential = null!;
	private WorldGroup _parallel = null!;

	[Params(1, 4096, 32768)]
	public int EntitiesPerWorld { get; set; }

	[GlobalSetup]
	public void Setup()
	{
		_worlds = new World[4];
		for (int index = 0; index < _worlds.Length; index++)
		{
			var world = new World(new WorldConfig
			{
				EntityCapacity = EntitiesPerWorld, ComponentTypeCapacity = 4,
				CommandCapacity = 8, CommandPayloadCapacityPerType = 8,
				QueryResultCapacity = EntitiesPerWorld, MaxDegreeOfParallelism = 1
			});
			_worlds[index] = world;
			for (int entity = 0; entity < EntitiesPerWorld; entity++)
				world.Spawn(new Position { Value = entity * 0.01f });
			world.AddSystem(new IntegrationSystem());
		}
		_sequential = new WorldGroup(_worlds, 1);
		_parallel = new WorldGroup(_worlds, Math.Min(4, Environment.ProcessorCount));
		_sequential.Tick(0.016f);
		_parallel.Tick(0.016f);
	}

	[Benchmark(Baseline = true)]
	public void SequentialWorlds() => _sequential.Tick(0.016f);

	[Benchmark]
	public void ParallelWorlds() => _parallel.Tick(0.016f);

	[GlobalCleanup]
	public void Cleanup()
	{
		foreach (var world in _worlds) world.Dispose();
	}

	private struct Position { public float Value; }
	private readonly struct Positions : ICompiledQuerySpec
	{
		public void Build(ref QueryBuilder builder) => builder.All<Position>();
	}
	private sealed class IntegrationSystem : ISystem
	{
		public void Update(in SystemContext context) =>
			context.World.Query<Positions>().Run<Integrate, Position>(new(context.DeltaTime));
	}
	private readonly struct Integrate(float deltaTime) : IForEach<Position>
	{
		public void Execute(ref Position component1)
		{
			float value = component1.Value;
			for (int iteration = 0; iteration < 32; iteration++)
				value = value * 0.9999f + deltaTime;
			component1.Value = value;
		}
	}
}
