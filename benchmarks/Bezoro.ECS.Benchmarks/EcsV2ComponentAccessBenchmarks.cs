using BenchmarkDotNet.Attributes;
using Bezoro.ECS.Abstractions;
using Bezoro.ECS.Services;
using Bezoro.ECS.Types;

namespace Bezoro.ECS.Benchmarks;

[MemoryDiagnoser]
public class EcsV2ComponentAccessBenchmarks
{
	private ComponentAccessor<Position>                _positionAccessor;
	private QueryHandle<PositionQuerySpec>             _positionQueryHandle = default;
	private Entity[]                                   _entities = null!;
	private QueryHandle<PositionVelocityQuerySpec> _queryHandle = default;
	private WorldV2                                    _world = null!;

	[Params(100_000)]
	public int EntityCount { get; set; }

	[Benchmark(Description = "WorldV2 sequential component read via TryGet<T>")]
	public float SequentialTryGetRead()
	{
		var sum = 0f;
		for (var i = 0; i < _entities.Length; i++)
		{
			if (_world.TryGet<Position>(_entities[i], out var position))
				sum += position.X + position.Y;
		}

		return sum;
	}

	[Benchmark(Description = "WorldV2 accessor sequential component read via TryGet")]
	public float SequentialAccessorTryGetRead()
	{
		var sum = 0f;
		for (var i = 0; i < _entities.Length; i++)
		{
			if (_positionAccessor.TryGet(_entities[i], out var position))
				sum += position.X + position.Y;
		}

		return sum;
	}

	[Benchmark(Description = "WorldV2 sequential component ref-write via Get<T>")]
	public float SequentialGetWrite()
	{
		var sum = 0f;
		for (var i = 0; i < _entities.Length; i++)
		{
			ref var position = ref _world.Get<Position>(_entities[i]);
			position.X += 0.125f;
			position.Y -= 0.125f;
			sum += position.X;
		}

		return sum;
	}

	[Benchmark(Description = "WorldV2 accessor sequential component ref-write via Get")]
	public float SequentialAccessorGetWrite()
	{
		var sum = 0f;
		for (var i = 0; i < _entities.Length; i++)
		{
			ref var position = ref _positionAccessor.Get(_entities[i]);
			position.X += 0.125f;
			position.Y -= 0.125f;
			sum += position.X;
		}

		return sum;
	}

	[Benchmark(Description = "WorldV2 query cursor struct-job run")]
	public int QueryCursorRun()
	{
		using var cursor = _world.Execute(_queryHandle);
		if (!cursor.MoveNext())
			return 0;

		cursor.Run<IntegrateJob, Position, Velocity>(new(0.016f));
		return _world.EntityCount;
	}

	[Benchmark(Description = "WorldV2 query cursor sequential Get<T> write")]
	public float QueryCursorSequentialGetWrite()
	{
		using var cursor = _world.Execute(_positionQueryHandle);
		if (!cursor.MoveNext())
			return 0f;

		var sum = 0f;
		for (var i = 0; i < EntityCount; i++)
		{
			ref var position = ref cursor.Get<Position>(i);
			position.X += 0.125f;
			position.Y -= 0.125f;
			sum += position.X;
		}

		return sum;
	}

	[Benchmark(Description = "WorldV2 direct compiled query struct-job run")]
	public int QueryDirectRun()
	{
		_world.Run<PositionVelocityQuerySpec, IntegrateJob, Position, Velocity>(_queryHandle, new(0.016f));
		return _world.EntityCount;
	}

	[GlobalSetup]
	public void Setup()
	{
		_world = new(new()
		{
			EntityCapacity                = EntityCount + 32,
			ComponentTypeCapacity         = 64,
			CommandCapacity               = EntityCount * 3,
			CommandPayloadCapacityPerType = EntityCount * 2,
			QueryResultCapacity           = EntityCount + 32
		});

		using var commands = _world.CreateCommandStream();
		for (var i = 0; i < EntityCount; i++)
		{
			var entity = commands.CreateEntity();
			commands.Set(entity, new Position { X = i, Y = i });
			commands.Set(entity, new Velocity { X = 1, Y = -1 });
		}

		_world.Playback(commands);
		_queryHandle = _world.Compile<PositionVelocityQuerySpec>();
		_positionAccessor = _world.GetAccessor<Position>();

		_positionQueryHandle = _world.Compile<PositionQuerySpec>();
		_entities = new Entity[EntityCount];
		using var cursor = _world.Execute(_positionQueryHandle);
		cursor.MoveNext();
		cursor.Current.CopyTo(_entities);
	}

	[GlobalCleanup]
	public void Cleanup() => _world.Dispose();

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
