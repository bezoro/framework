using System;
using System.Runtime.CompilerServices;
using Bezoro.Core.Collections;
using Xunit;

namespace Bezoro.Core.Tests.Collections;

public sealed class BoundedCollectionsAllocationTests
{
	[Fact]
	public void WarmedCycles_AndConcreteEnumeration_AllocateNoManagedMemory()
	{
		const int iterations = 10_000;
		var map = new SlotMap<int>(8);
		var ring = new RingBuffer<int>(8);
		Measure(map, ring, iterations, out _);
		long smallest = long.MaxValue;
		for (int sample = 0; sample < 8; sample++)
		{
			long allocated = Measure(map, ring, iterations, out int sum);
			Assert.Equal(iterations * 3, sum);
			smallest = Math.Min(smallest, allocated);
		}
		Assert.Equal(0, smallest);
		Assert.Empty(map);
		Assert.Empty(ring);
	}

	[MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
	private static long Measure(SlotMap<int> map, RingBuffer<int> ring, int iterations, out int sum)
	{
		sum = 0;
		long before = GC.GetAllocatedBytesForCurrentThread();
		for (int i = 0; i < iterations; i++)
		{
			var handle = map.Add(1);
			map.TryGet(handle, out int value);
			sum += value;
			foreach (int item in map) sum += item;
			map.Remove(handle);
			ring.Enqueue(1);
			foreach (int item in ring) sum += item;
			ring.Dequeue();
		}
		return GC.GetAllocatedBytesForCurrentThread() - before;
	}
}
