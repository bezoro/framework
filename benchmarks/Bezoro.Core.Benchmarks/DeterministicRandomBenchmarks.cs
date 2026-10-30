using System;
using System.Linq;
using BenchmarkDotNet.Attributes;
using Bezoro.Core.Randomness;

namespace Bezoro.Core.Benchmarks;

[MemoryDiagnoser]
public class DeterministicRandomBenchmarks
{
	private Pcg32State _state;
	private WeightedIndexTable _weights = null!;
	private int[] _shuffle = null!;

	[GlobalSetup]
	public void Setup()
	{
		_state = Pcg32State.Create(42, 54);
		_weights = new WeightedIndexTable(Enumerable.Range(0, 256).Select(index => (ulong)(index % 7)).ToArray());
		_shuffle = Enumerable.Range(0, 32).ToArray();
	}

	[Benchmark(Baseline = true)]
	public uint RawUInt32() => Pcg32.NextUInt32(ref _state);

	[Benchmark]
	public int BoundedInt32() => Pcg32.NextInt32(ref _state, -100, 100);

	[Benchmark]
	public ulong RejectionUInt64() => Pcg32.NextUInt64(ref _state, 0x8000000000000001);

	[Benchmark]
	public double UnitDouble() => Pcg32.NextDouble(ref _state);

	[Benchmark]
	public int WeightedPick256() => _weights.Pick(ref _state);

	[Benchmark]
	public int Shuffle32()
	{
		Pcg32.Shuffle(ref _state, _shuffle.AsSpan());
		return _shuffle[0];
	}
}
