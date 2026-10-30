using System;
using BenchmarkDotNet.Attributes;
using Bezoro.Core.Collections;
using Bezoro.Core.Extensions;

namespace Bezoro.Core.Benchmarks;

[MemoryDiagnoser]
public class BufferUtilitiesBenchmarks
{
	private BitSet _dense = null!;
	private BitSet _sparse = null!;
	private BitSet _target = null!;
	private int[] _sorted = null!;
	private int[] _ordered = null!;
	private int[] _swapBack = null!;

	[GlobalSetup]
	public void Setup()
	{
		_dense = new BitSet(4096);
		_dense.SetAll();
		_sparse = new BitSet(4096);
		_target = new BitSet(4096);
		for (int index = 0; index < _sparse.Length; index += 64) _sparse.Set(index);
		_sorted = new int[1024];
		_ordered = new int[1024];
		_swapBack = new int[1024];
		for (int index = 0; index < _sorted.Length; index++)
		{
			_sorted[index] = index / 4;
			_ordered[index] = index;
			_swapBack[index] = index;
		}
	}

	[Benchmark]
	public int Count4096Flags() => _dense.Count;

	[Benchmark]
	public int Enumerate64Of4096Flags()
	{
		int sum = 0;
		foreach (int index in _sparse) sum += index;
		return sum;
	}

	[Benchmark]
	public BitSet Xor4096Flags()
	{
		_target.XorWith(_sparse);
		return _target;
	}

	[Benchmark]
	public int LowerBound1024() => _sorted.AsSpan().LowerBound(127);

	[Benchmark]
	public int RemoveOrdered1024() => _ordered.AsSpan().RemoveAtOrdered(256).Length;

	[Benchmark]
	public int RemoveSwapBack1024() => _swapBack.AsSpan().RemoveAtSwapBack(256).Length;
}
