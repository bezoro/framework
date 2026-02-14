using BenchmarkDotNet.Attributes;
using Bezoro.ECS.Abstractions;
using Bezoro.ECS.Services;
using Bezoro.ECS.Types;

namespace Bezoro.ECS.Benchmarks;

[MemoryDiagnoser]
public class EcsWorldHotPathBenchmarks
{
	private QueryHandle<PositionVelocityQuerySpec> _handle;
	private World _world = null!;

	[Params(100_000)]
	public int EntityCount { get; set; }

	[GlobalSetup]
	public void Setup()
	{
		_world = new(new()
		{
			EntityCapacity                = EntityCount + 16,
			ComponentTypeCapacity         = 64,
			CommandCapacity               = EntityCount * 3,
			CommandPayloadCapacityPerType = EntityCount * 2,
			QueryResultCapacity           = EntityCount
		});

		using var commands = _world.CreateCommandStream();
		for (var i = 0; i < EntityCount; i++)
		{
			var entity = commands.CreateEntity();
			commands.Set(entity, new Position { X = i, Y = i });
			commands.Set(entity, new Velocity { X = 1, Y = -1 });
		}

		_world.Playback(commands);
		_handle = _world.Compile<PositionVelocityQuerySpec>();
	}

	[GlobalCleanup]
	public void Cleanup()
	{
		_world.Dispose();
	}

	[Benchmark(Description = "World compiled query sequential ref/in ForEach")]
	public void QueryForEach()
	{
		using var cursor = _world.Execute(_handle);
		if (!cursor.MoveNext())
			return;

		cursor.Run<IntegrateJob, Position, Velocity>(new(0.016f));
	}

	[Benchmark(Description = "World direct compiled query ref/in ForEach")]
	public void QueryForEachDirect()
	{
		_world.Run<PositionVelocityQuerySpec, IntegrateJob, Position, Velocity>(_handle, new(0.016f));
	}

	private readonly struct PositionVelocityQuerySpec : ICompiledQuerySpec
	{
		public void Build(ref QueryBuilder builder)
		{
			builder.All<Position>();
			builder.All<Velocity>();
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

	private readonly struct IntegrateJob(float dt) : IForEach<Position, Velocity>
	{
		public void Execute(ref Position component1, in Velocity component2)
		{
			component1.X += component2.X * dt;
			component1.Y += component2.Y * dt;
		}
	}
}

