using BenchmarkDotNet.Attributes;
using Bezoro.ECS.Abstractions;
using Bezoro.ECS.Services;
using Bezoro.ECS.Types;

namespace Bezoro.ECS.Benchmarks;

[MemoryDiagnoser]
public class EcsV3VsWorldHotPathBenchmarks
{
	private ComponentAccessor<Position> _v2PositionAccessor;
	private ComponentAccessor<Position> _v3PositionAccessor;
	private Entity[]                    _v2Entities = null!;
	private Entity[]                    _v3Entities = null!;
	private QueryHandle<PositionVelocityQuerySpec> _v2Handle;
	private QueryHandle<PositionVelocityQuerySpec> _v3Handle;
	private World _v2World = null!;
	private WorldV3 _v3World = null!;

	[Params(100_000)]
	public int EntityCount { get; set; }

	[GlobalSetup]
	public void Setup()
	{
		_v2World = new(new()
		{
			EntityCapacity = EntityCount + 32,
			ComponentTypeCapacity = 64,
			CommandCapacity = EntityCount * 3,
			CommandPayloadCapacityPerType = EntityCount * 2,
			QueryResultCapacity = EntityCount + 32
		});
		_v3World = new(new()
		{
			EntityCapacity = EntityCount + 32,
			ComponentTypeCapacity = 64,
			CommandCapacity = EntityCount * 3,
			CommandPayloadCapacityPerType = EntityCount * 2,
			QueryResultCapacity = EntityCount + 32
		});

		using (var v2Commands = _v2World.CreateCommandStream())
		{
			for (var i = 0; i < EntityCount; i++)
			{
				var entity = v2Commands.CreateEntity();
				v2Commands.Set(entity, new Position { X = i, Y = i });
				v2Commands.Set(entity, new Velocity { X = 1, Y = -1 });
			}

			_v2World.Playback(v2Commands);
		}

		using (var v3Commands = _v3World.CreateCommandStream())
		{
			for (var i = 0; i < EntityCount; i++)
			{
				var entity = v3Commands.CreateEntity();
				v3Commands.Set(entity, new Position { X = i, Y = i });
				v3Commands.Set(entity, new Velocity { X = 1, Y = -1 });
			}

			_v3World.Playback(v3Commands);
		}

		_v2Handle = _v2World.Compile<PositionVelocityQuerySpec>();
		_v3Handle = _v3World.Compile<PositionVelocityQuerySpec>();
		_v2PositionAccessor = _v2World.GetAccessor<Position>();
		_v3PositionAccessor = _v3World.GetAccessor<Position>();

		_v2Entities = new Entity[EntityCount];
		_v3Entities = new Entity[EntityCount];
		using (var v2Cursor = _v2World.Execute(_v2World.Compile<PositionQuerySpec>()))
		{
			v2Cursor.MoveNext();
			v2Cursor.Current.CopyTo(_v2Entities);
		}

		using var v3Cursor = _v3World.Execute(_v3World.Compile<PositionQuerySpec>());
		v3Cursor.MoveNext();
		v3Cursor.Current.CopyTo(_v3Entities);
	}

	[GlobalCleanup]
	public void Cleanup()
	{
		_v2World.Dispose();
		_v3World.Dispose();
	}

	[Benchmark(Baseline = true, Description = "World sequential component read via accessor TryGet")]
	public float WorldSequentialReadAccessor()
	{
		var sum = 0f;
		for (var i = 0; i < _v2Entities.Length; i++)
		{
			if (_v2PositionAccessor.TryGet(_v2Entities[i], out var position))
				sum += position.X + position.Y;
		}

		return sum;
	}

	[Benchmark(Description = "V3 sequential component read via accessor TryGet")]
	public float V3SequentialReadAccessor()
	{
		var sum = 0f;
		for (var i = 0; i < _v3Entities.Length; i++)
		{
			if (_v3PositionAccessor.TryGet(_v3Entities[i], out var position))
				sum += position.X + position.Y;
		}

		return sum;
	}

	[Benchmark(Description = "World sequential component ref-write via accessor Get")]
	public float WorldSequentialWriteAccessor()
	{
		var sum = 0f;
		for (var i = 0; i < _v2Entities.Length; i++)
		{
			ref var position = ref _v2PositionAccessor.Get(_v2Entities[i]);
			position.X += 0.125f;
			position.Y -= 0.125f;
			sum += position.X;
		}

		return sum;
	}

	[Benchmark(Description = "V3 sequential component ref-write via accessor Get")]
	public float V3SequentialWriteAccessor()
	{
		var sum = 0f;
		for (var i = 0; i < _v3Entities.Length; i++)
		{
			ref var position = ref _v3PositionAccessor.Get(_v3Entities[i]);
			position.X += 0.125f;
			position.Y -= 0.125f;
			sum += position.X;
		}

		return sum;
	}

	[Benchmark(Description = "World direct compiled query struct-job run")]
	public int WorldDirectRun()
	{
		_v2World.Run<PositionVelocityQuerySpec, IntegrateJob, Position, Velocity>(_v2Handle, new(0.016f));
		return _v2World.EntityCount;
	}

	[Benchmark(Description = "V3 direct compiled query struct-job run")]
	public int V3DirectRun()
	{
		_v3World.Run<PositionVelocityQuerySpec, IntegrateJob, Position, Velocity>(_v3Handle, new(0.016f));
		return _v3World.EntityCount;
	}

	private readonly struct PositionQuerySpec : ICompiledQuerySpec
	{
		public void Build(ref QueryBuilder builder) => builder.All<Position>();
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
}

