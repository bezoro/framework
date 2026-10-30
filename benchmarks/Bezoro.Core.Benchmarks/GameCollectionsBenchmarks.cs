using BenchmarkDotNet.Attributes;
using Bezoro.Core.Collections;

namespace Bezoro.Core.Benchmarks;

/// <summary>Measures preallocated game collection operations separately from storage construction.</summary>
[MemoryDiagnoser]
public class GameCollectionsBenchmarks
{
	private SlotMap<int> _slots = null!;
	private SlotMap<int> _denseSlots = null!;
	private RingBuffer<int> _queue = null!;
	private RingBuffer<int> _fullQueue = null!;

	[GlobalSetup]
	public void Setup()
	{
		_slots = new SlotMap<int>(1024);
		_denseSlots = new SlotMap<int>(1024);
		_queue = new RingBuffer<int>(1024);
		_fullQueue = new RingBuffer<int>(1024);
		for (int i = 0; i < 1024; i++)
		{
			_denseSlots.Add(i);
			_fullQueue.Enqueue(i);
		}
	}

	[Benchmark]
	public int SlotMapAddReadRemove()
	{
		var handle = _slots.Add(42);
		_slots.TryGet(handle, out int value);
		_slots.Remove(handle);
		return value;
	}

	[Benchmark]
	public int SlotMapDenseScan()
	{
		int sum = 0;
		foreach (int value in _denseSlots.Values) sum += value;
		return sum;
	}

	[Benchmark]
	public int RingBufferEnqueueDequeue()
	{
		_queue.Enqueue(42);
		return _queue.Dequeue();
	}

	[Benchmark]
	public int RingBufferOverwrite()
	{
		_fullQueue.EnqueueOverwrite(42, out int evicted);
		return evicted;
	}
}
