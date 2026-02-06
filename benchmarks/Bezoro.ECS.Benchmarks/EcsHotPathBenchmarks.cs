using System;
using BenchmarkDotNet.Attributes;
using Bezoro.ECS.Abstractions;
using Bezoro.ECS.Services;
using Bezoro.ECS.Types;

namespace Bezoro.ECS.Benchmarks;

[MemoryDiagnoser]
public class EcsHotPathBenchmarks
{
	private World _world = null!;
	private Entity[] _entities = null!;

	[Params(100_000)]
	public int EntityCount { get; set; }

	[GlobalSetup]
	public void Setup()
	{
		_world = new World();
		_entities = new Entity[EntityCount];
		for (var i = 0; i < EntityCount; i++)
		{
			_entities[i] = _world.Spawn(
				new Position { X = i, Y = i * 2f },
				new Velocity { X = 1f, Y = -1f });
		}
	}

	[GlobalCleanup]
	public void Cleanup()
	{
		_world.Dispose();
	}

	[Benchmark(Description = "Iterate entities (Position+Velocity, single-threaded)")]
	public void IterateSingleThreaded()
	{
		_world.Query<Position, Velocity>()
			.ForEach<MovementJob, Position, Velocity>(new MovementJob { DeltaTime = 0.016f });
	}

	[Benchmark(Description = "Iterate entities (Position+Velocity, parallel)")]
	public void IterateParallel()
	{
		_world.Query<Position, Velocity>()
			.ForEachParallel(chunk =>
			{
				var positions = chunk.Components<Position>();
				var velocities = chunk.ReadOnlyComponents<Velocity>();
				for (var i = 0; i < chunk.Count; i++)
				{
					positions[i].X += velocities[i].X * 0.016f;
					positions[i].Y += velocities[i].Y * 0.016f;
				}
			}, Environment.ProcessorCount);
	}

	[Benchmark(Description = "Entity lookup by id (Get<Position>)")]
	public float EntityLookupById()
	{
		var sum = 0f;
		for (var i = 0; i < _entities.Length; i++)
		{
			ref var position = ref _world.Get<Position>(_entities[i]);
			sum += position.X;
		}

		return sum;
	}

	[Benchmark(Description = "Query cache hit (create+enumerate repeated query)")]
	public int QueryCacheHit()
	{
		var count = 0;
		var query = _world.Query<Position, Velocity>();
		foreach (var chunk in query)
			count += chunk.Count;

		return count;
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

	private struct MovementJob : IForEach<Position, Velocity>
	{
		public float DeltaTime;

		public void Execute(ref Position component1, in Velocity component2)
		{
			component1.X += component2.X * DeltaTime;
			component1.Y += component2.Y * DeltaTime;
		}
	}
}
