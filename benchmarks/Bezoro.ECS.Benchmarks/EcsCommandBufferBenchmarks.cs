using BenchmarkDotNet.Attributes;
using Bezoro.ECS.Services;
using Bezoro.ECS.Types;

namespace Bezoro.ECS.Benchmarks;

[MemoryDiagnoser]
public class EcsCommandBufferBenchmarks
{
	private Entity[] _entities = null!;
	private World    _world    = null!;

	[Params(10_000)]
	public int EntityCount { get; set; }

	[Benchmark(Description = "CommandBuffer set existing component (record + playback)")]
	public void RecordAndPlaybackSetComponent()
	{
		var commands = _world.CreateCommandBuffer();
		for (var i = 0; i < _entities.Length; i++)
			commands.SetComponent(_entities[i], new Position { X = i + 1, Y = i + 2 });

		commands.Playback();
		commands.Dispose();
	}

	[GlobalCleanup]
	public void Cleanup()
	{
		_world.Dispose();
	}

	[GlobalSetup]
	public void Setup()
	{
		_world    = new();
		_entities = new Entity[EntityCount];
		for (var i = 0; i < EntityCount; i++)
			_entities[i] = _world.Spawn(new Position { X = i, Y = i });
	}

	private struct Position
	{
		public float X;
		public float Y;
	}
}
