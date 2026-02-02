using System;
using System.Linq;
using BenchmarkDotNet.Attributes;
using Bezoro.Core.Types;

namespace Bezoro.Core.Benchmarks;

[MemoryDiagnoser]
public class SwapbackArrayBenchmarks
{
	private static readonly int[] AddRangeBatch = Enumerable.Range(0, 1_000).ToArray();

	[Benchmark(Description = "Add items one-by-one (10,000)")]
	public void AddMany()
	{
		var arr = new SwapbackArray<int>();

		for (var i = 0; i < 10_000; i++)
			arr.Add(i);
	}

	[Benchmark(Description = "AddRange batches (100 × 1,000)")]
	public void AddRangeBatches()
	{
		var arr = new SwapbackArray<int>();

		for (var i = 0; i < 100; i++)
			arr.AddRange(AddRangeBatch.AsSpan());
	}

	[Benchmark(Description = "Balanced churn (remove/add, steady state)")]
	public void BalancedChurn()
	{
		var arr = new SwapbackArray<int>();

		for (var i = 0; i < 500; i++)
			arr.Add(i);

		for (var i = 0; i < 5_000; i++)
		{
			arr.TryRemoveAt(0);
			arr.Add(i + 500);
		}
	}

	[Benchmark(Description = "Churn with net growth (1,000 iterations)")]
	public void GrowthChurn()
	{
		var arr = new SwapbackArray<int>();

		for (var i = 0; i < 1_000; i++)
		{
			for (var j = 0; j < 100; j++)
				arr.Add(j);

			for (var j = 0; j < 50 && !arr.IsEmpty; j++)
				arr.TryRemoveAt(0);
		}
	}

	[Benchmark(Description = "Grow to 10,000 then shrink to 100")]
	public void GrowThenTrim()
	{
		var arr = new SwapbackArray<int>();

		for (var i = 0; i < 10_000; i++)
			arr.Add(i);

		while (arr.Count > 100)
			arr.TryRemoveAt(0);

		arr.TrimExcess();
	}

	[Benchmark(Description = "Sequential adds (100,000 items)")]
	public void ManyAdds()
	{
		var arr = new SwapbackArray<int>();

		for (var i = 0; i < 100_000; i++)
			arr.Add(i);
	}

	[Benchmark(Description = "Random removals (half of 10,000 items)")]
	public void RandomRemovals()
	{
		var arr = new SwapbackArray<int>();
		for (var i = 0; i < 10_000; i++)
			arr.Add(i);

		var random = new Random(42);
		for (var i = 0; i < 5_000; i++)
		{
			var index = (uint)random.Next((int)arr.Count);
			arr.TryRemoveAt(index);
		}
	}

	[Benchmark(Description = "TryRemoveAt cost (100,000 items, 500 removals)")]
	public void RemovalComplexityLarge()
	{
		var arr = new SwapbackArray<int>();
		for (var i = 0; i < 100_000; i++)
			arr.Add(i);

		var random = new Random(42);
		for (var i = 0; i < 500; i++)
			arr.TryRemoveAt((uint)random.Next((int)arr.Count));
	}

	[Benchmark(Description = "TryRemoveAt cost (1,000 items, 500 removals)")]
	public void RemovalComplexitySmall()
	{
		var arr = new SwapbackArray<int>();
		for (var i = 0; i < 1_000; i++)
			arr.Add(i);

		var random = new Random(42);
		for (var i = 0; i < 500; i++)
			arr.TryRemoveAt((uint)random.Next((int)arr.Count));
	}
}
