using BenchmarkDotNet.Attributes;
using Bezoro.ECS.Services;
using Bezoro.ECS.Types;

namespace Bezoro.ECS.Benchmarks;

[MemoryDiagnoser]
public class EcsCommandBufferBurstBenchmarks
{
	private CommandBuffer _commands = null!;
	private World         _world    = null!;

	[Params(50_000)]
	public int BurstSize { get; set; }

	[Benchmark(Description = "CommandBuffer large create burst (record+playback+clear)")]
	public int CommandBufferCreateBurst()
	{
		for (var i = 0; i < BurstSize; i++)
			_commands.CreateEntity(new Position { X = i, Y = i });

		_commands.Playback();
		int count = _world.EntityCount;
		_world.Clear();
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
		_world    = new();
		_commands = _world.CreateCommandBuffer();
	}

	private struct Position
	{
		public float X;
		public float Y;
	}
}
