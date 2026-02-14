using BenchmarkDotNet.Attributes;
using Bezoro.ECS.Services;
using Bezoro.ECS.Types;

namespace Bezoro.ECS.Benchmarks;

[MemoryDiagnoser]
public class EcsQueryCachePressureBenchmarks
{
	private Entity[] _targets = null!;
	private WorldV1    _world   = null!;

	[Params(2_048)]
	public int TargetCount { get; set; }

	[Params(50_000)]
	public int SourceCount { get; set; }

	[Benchmark(Description = "Relationship-specific queries under high-cardinality targets")]
	public int RelationshipSpecificQueryPressure()
	{
		var count = 0;
		for (var i = 0; i < _targets.Length; i++)
		{
			var query = _world.Query().Related<Follows>(_targets[i]);
			foreach (var chunk in query)
				count += chunk.Count;
		}

		return count;
	}

	[GlobalCleanup]
	public void Cleanup()
	{
		_world.Dispose();
	}

	[GlobalSetup]
	public void Setup()
	{
		_world   = new();
		_targets = new Entity[TargetCount];
		for (var i = 0; i < TargetCount; i++)
			_targets[i] = _world.Spawn();

		for (var i = 0; i < SourceCount; i++)
		{
			var source = _world.Spawn(new Position { X = i, Y = i });
			_world.Add<Follows>(source, _targets[i % TargetCount]);
		}
	}

	private readonly struct Follows;

	private struct Position
	{
		public float X;
		public float Y;
	}
}
