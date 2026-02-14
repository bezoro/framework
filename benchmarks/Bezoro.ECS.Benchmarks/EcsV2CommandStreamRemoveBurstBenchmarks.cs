using BenchmarkDotNet.Attributes;
using Bezoro.ECS.Abstractions;
using Bezoro.ECS.Services;
using Bezoro.ECS.Types;

namespace Bezoro.ECS.Benchmarks;

[MemoryDiagnoser]
public class EcsV2CommandStreamRemoveBurstBenchmarks
{
	private const int OperationsPerInvoke = 8;

	private CommandStream _commands = null!;
	private Entity[]      _entities = null!;
	private WorldV2       _world    = null!;

	[Params(50_000)]
	public int BurstSize { get; set; }

	[Benchmark(
		Description = "WorldV2 CommandStream large remove burst (remove+restore cycle on existing component lane)",
		OperationsPerInvoke = OperationsPerInvoke
	)]
	public int CommandStreamRemoveBurst()
	{
		var count = 0;
		for (var op = 0; op < OperationsPerInvoke; op++)
		{
			for (var i = 0; i < BurstSize; i++)
				_commands.Remove<Velocity>(_entities[i]);

			_world.Playback(_commands);
			count += _world.EntityCount;

			for (var i = 0; i < BurstSize; i++)
				_commands.Set(_entities[i], new Velocity { X = 1, Y = -1 });

			_world.Playback(_commands);
		}

		return count;
	}

	[GlobalCleanup]
	public void Cleanup()
	{
		_commands.Dispose();
		_world.Dispose();
	}

	[GlobalSetup]
	public void Setup()
	{
		_world = new(new()
		{
			EntityCapacity                = BurstSize + 32,
			ComponentTypeCapacity         = 32,
			CommandCapacity               = (BurstSize * 2) + 32,
			CommandPayloadCapacityPerType = BurstSize + 32,
			QueryResultCapacity           = BurstSize + 32
		});
		_commands = _world.CreateCommandStream();

		for (var i = 0; i < BurstSize; i++)
		{
			var entity = _commands.CreateEntity(new Position { X = i, Y = i });
			_commands.Set(entity, new Velocity { X = 1, Y = -1 });
		}

		_world.Playback(_commands);

		var handle = _world.Compile<PositionQuerySpec>();
		_entities = new Entity[BurstSize];
		using (var cursor = _world.Execute(handle))
		{
			cursor.MoveNext();
			cursor.Current.CopyTo(_entities);
		}

		// Prime add/remove transition archetypes and copy maps before measurement.
		_commands.Remove<Velocity>(_entities[0]);
		_world.Playback(_commands);
		_commands.Set(_entities[0], new Velocity { X = 1, Y = -1 });
		_world.Playback(_commands);
	}

	private readonly struct PositionQuerySpec : ICompiledQuerySpec
	{
		public void Build(ref QueryBuilder builder) => builder.All<Position>();
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
