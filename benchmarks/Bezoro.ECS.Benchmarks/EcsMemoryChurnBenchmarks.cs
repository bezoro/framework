using BenchmarkDotNet.Attributes;
using Bezoro.ECS.Services;
using Bezoro.ECS.Types;

namespace Bezoro.ECS.Benchmarks;

[MemoryDiagnoser]
public class EcsMemoryChurnBenchmarks
{
	private Entity[]  _entities = null!;
	private Payload[] _payloads = null!;
	private WorldV1     _world    = null!;

	[Params(50_000)]
	public int EntityCount { get; set; }

	[Benchmark(Description = "Managed component spawn+despawn churn")]
	public int ManagedComponentSpawnAndDespawn()
	{
		for (var i = 0; i < EntityCount; i++)
			_entities[i] = _world.Spawn(new ManagedPayload { Value = _payloads[i] });

		for (var i = 0; i < EntityCount; i++)
			_world.Despawn(_entities[i]);

		return _world.EntityCount;
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
		_payloads = new Payload[EntityCount];
		for (var i = 0; i < EntityCount; i++)
			_payloads[i] = new(i);
	}

	private sealed record Payload(int Id);

	private struct ManagedPayload
	{
		public Payload? Value;
	}
}
