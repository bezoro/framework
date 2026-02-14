using BenchmarkDotNet.Attributes;
using Bezoro.ECS.Services;
using Bezoro.ECS.Types;

namespace Bezoro.ECS.Benchmarks;

[MemoryDiagnoser]
public class EcsV2CommandStreamBurstBenchmarks
{
	private CommandStream _commands = null!;
	private WorldV2       _world    = null!;

	[Params(50_000)]
	public int BurstSize { get; set; }

	[Benchmark(Description = "WorldV2 CommandStream large create burst (record+playback+reset)")]
	public int CommandStreamCreateBurst()
	{
		for (var i = 0; i < BurstSize; i++)
			_commands.CreateEntity(new Position { X = i, Y = i });

		_world.Playback(_commands);
		int count = _world.EntityCount;
		_world.Reset();
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
			CommandCapacity               = BurstSize + 32,
			CommandPayloadCapacityPerType = BurstSize + 32,
			QueryResultCapacity           = BurstSize + 32
		});
		_commands = _world.CreateCommandStream();
	}

	private struct Position
	{
		public float X;
		public float Y;
	}
}
