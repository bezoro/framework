using BenchmarkDotNet.Attributes;
using Bezoro.ECS.Abstractions;
using Bezoro.ECS.Services;
using Bezoro.ECS.Types;

namespace Bezoro.ECS.Benchmarks;

[MemoryDiagnoser]
public class EcsWorldCommandStreamSetBurstBenchmarks
{
	private CommandStream _commands = null!;
	private Entity[]      _entities = null!;
	private World       _world    = null!;

	[Params(50_000)]
	public int BurstSize { get; set; }

	[Benchmark(Description = "World CommandStream large set burst (record+playback on existing component)")]
	public int CommandStreamSetBurst()
	{
		for (var i = 0; i < BurstSize; i++)
			_commands.Set(_entities[i], new Position { X = i + 1, Y = -i });

		_world.Playback(_commands);
		return _world.EntityCount;
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
			var entity = _commands.CreateEntity();
			_commands.Set(entity, new Position { X = i, Y = i });
		}

		_world.Playback(_commands);

		var handle = _world.Compile<PositionQuerySpec>();
		_entities = new Entity[BurstSize];
		using var cursor = _world.Execute(handle);
		cursor.MoveNext();
		cursor.Current.CopyTo(_entities);
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
}

