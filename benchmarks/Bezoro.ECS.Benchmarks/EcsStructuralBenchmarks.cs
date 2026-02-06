using BenchmarkDotNet.Attributes;
using Bezoro.ECS.Abstractions;
using Bezoro.ECS.Services;
using Bezoro.ECS.Types;

namespace Bezoro.ECS.Benchmarks;

[MemoryDiagnoser]
public class EcsStructuralBenchmarks
{
	[Params(100_000)]
	public int EntityCount { get; set; }

	[Benchmark(Description = "Add component to existing entities (structural move)")]
	public int AddComponentToEntities()
	{
		using var world    = new World();
		var       entities = new Entity[EntityCount];
		for (var i = 0; i < EntityCount; i++)
			entities[i] = world.Spawn(new Position { X = i, Y = i });

		for (var i = 0; i < entities.Length; i++)
			world.Add(entities[i], new Velocity { X = 1f, Y = 1f });

		return world.EntityCount;
	}

	[Benchmark(Description = "Create entities (Spawn with 2 components)")]
	public int CreateEntities()
	{
		using var world = new World();
		for (var i = 0; i < EntityCount; i++)
			world.Spawn(new Position { X = i, Y = i }, new Velocity { X = 1f, Y = 1f });

		return world.EntityCount;
	}

	[Benchmark(Description = "Destroy entities (Despawn)")]
	public int DestroyEntities()
	{
		using var world    = new World();
		var       entities = new Entity[EntityCount];
		for (var i = 0; i < EntityCount; i++)
			entities[i] = world.Spawn(new Position { X = i, Y = i });

		for (var i = 0; i < entities.Length; i++)
			world.Despawn(entities[i]);

		return world.EntityCount;
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
