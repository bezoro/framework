using BenchmarkDotNet.Attributes;
using Bezoro.Core.Types;
using Bezoro.Core.Types.Pool;

namespace Bezoro.Core.Benchmarks;

[MemoryDiagnoser]
public class ObjectPoolBenchmarks
{
	private readonly ObjectPool<PooledItem> _pool = new(() => new(), new() { InitialCapacity = 1, MaxCapacity = 1 });

	[Benchmark(Baseline = true, Description = "Rent and return")]
	public void RentAndReturn()
	{
		var item = _pool.Rent();
		_pool.Return(item);
	}

	[Benchmark(Description = "Rent handle and dispose")]
	public void RentHandleAndDispose()
	{
		var handle = _pool.RentHandle();
		handle.Dispose();
	}
}
