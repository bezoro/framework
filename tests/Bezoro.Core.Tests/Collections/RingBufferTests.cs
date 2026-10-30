using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Bezoro.Core.Collections;
using Xunit;

namespace Bezoro.Core.Tests.Collections;

public sealed class RingBufferTests
{
	[Theory]
	[InlineData(0)]
	[InlineData(-1)]
	public void NonPositiveCapacity_IsRejected(int capacity) =>
		Assert.Throws<ArgumentOutOfRangeException>(() => new RingBuffer<int>(capacity));

	[Fact]
	public void FullBuffer_RejectsWithoutOverwritingOrInvalidatingEnumeration()
	{
		var buffer = new RingBuffer<int>(2);
		buffer.Enqueue(1);
		buffer.Enqueue(2);
		var enumerator = buffer.GetEnumerator();
		Assert.True(buffer.IsFull);
		Assert.False(buffer.TryEnqueue(3));
		Assert.Throws<InvalidOperationException>(() => buffer.Enqueue(3));
		Assert.True(enumerator.MoveNext());
		Assert.Equal(1, enumerator.Current);
		Assert.Equal(new[] { 1, 2 }, buffer.ToArray());
	}

	[Fact]
	public void Overwrite_ReportsExactlyTheOldestValue()
	{
		var buffer = new RingBuffer<int>(2);
		Assert.False(buffer.EnqueueOverwrite(10, out int absent));
		Assert.Equal(0, absent);
		Assert.False(buffer.EnqueueOverwrite(20, out _));
		Assert.True(buffer.EnqueueOverwrite(30, out int evicted));
		Assert.Equal(10, evicted);
		Assert.Equal(new[] { 20, 30 }, buffer.ToArray());
		Assert.True(buffer.EnqueueOverwrite(40, out evicted));
		Assert.Equal(20, evicted);
		Assert.Equal(new[] { 30, 40 }, buffer.ToArray());
	}

	[Fact]
	public void CapacityOne_HandlesOverwriteDequeueAndReuse()
	{
		var buffer = new RingBuffer<int>(1);
		buffer.Enqueue(1);
		Assert.True(buffer.EnqueueOverwrite(2, out int previous));
		Assert.Equal(1, previous);
		Assert.Equal(2, buffer.Dequeue());
		Assert.Empty(buffer);
		Assert.False(buffer.IsFull);
		buffer.Enqueue(3);
		Assert.Equal(3, buffer.Dequeue());
	}

	[Fact]
	public void EmptyOperations_HaveExplicitNonMutatingResults()
	{
		var buffer = new RingBuffer<string>(1);
		Assert.False(buffer.TryPeek(out string peeked));
		Assert.Null(peeked);
		Assert.False(buffer.TryDequeue(out string removed));
		Assert.Null(removed);
		Assert.Throws<InvalidOperationException>(() => buffer.Dequeue());
		buffer.CopyTo(Span<string>.Empty);
		buffer.Clear();
	}

	[Fact]
	public void WrappedBuffer_PreservesLogicalIndexPeekAndCopyOrder()
	{
		var buffer = new RingBuffer<int>(4);
		for (int i = 0; i < 4; i++) buffer.Enqueue(i);
		Assert.Equal(0, buffer.Dequeue());
		Assert.Equal(1, buffer.Dequeue());
		buffer.Enqueue(4);
		buffer.Enqueue(5);
		Assert.True(buffer.TryPeek(out int oldest));
		Assert.Equal(2, oldest);
		Assert.Equal(2, buffer[0]);
		Assert.Equal(5, buffer[3]);
		var destination = new[] { -1, -1, -1, -1, -1, -1 };
		buffer.CopyTo(destination.AsSpan(1, 4));
		Assert.Equal(new[] { -1, 2, 3, 4, 5, -1 }, destination);
		Assert.Equal(4, buffer.Count);
		Assert.Throws<ArgumentException>(() => buffer.CopyTo(new int[3]));
	}

	[Theory]
	[InlineData(-1)]
	[InlineData(1)]
	public void LogicalIndices_AreValidated(int index)
	{
		var buffer = new RingBuffer<int>(2);
		buffer.Enqueue(1);
		Assert.Throws<ArgumentOutOfRangeException>(() => buffer[index]);
	}

	[Fact]
	public void Clear_RemovesWrappedValuesAndAllowsReuse()
	{
		var buffer = new RingBuffer<int>(3);
		for (int i = 0; i < 5; i++) buffer.EnqueueOverwrite(i, out _);
		buffer.Clear();
		Assert.Empty(buffer);
		buffer.Clear();
		buffer.Enqueue(10);
		Assert.Equal(10, buffer.Dequeue());
		Assert.Empty(buffer);
	}

	[Theory]
	[InlineData(0)]
	[InlineData(1)]
	[InlineData(2)]
	[InlineData(3)]
	public void Enumeration_RejectsMutations(int mutation)
	{
		var buffer = new RingBuffer<int>(2);
		buffer.Enqueue(1);
		if (mutation == 3) buffer.Enqueue(2);
		var enumerator = buffer.GetEnumerator();
		Assert.True(enumerator.MoveNext());
		switch (mutation)
		{
			case 0: buffer.Enqueue(2); break;
			case 1: buffer.Dequeue(); break;
			case 2: buffer.Clear(); break;
			default: buffer.EnqueueOverwrite(3, out _); break;
		}
		Assert.Throws<InvalidOperationException>(() => enumerator.MoveNext());
		Assert.Throws<InvalidOperationException>(() => enumerator.Current);
		Assert.Throws<InvalidOperationException>(() => enumerator.Reset());
	}

	[Fact]
	public void Enumeration_HasDefinedBoundaryAndResetBehavior()
	{
		var buffer = new RingBuffer<int>(1);
		var enumerator = buffer.GetEnumerator();
		Assert.Throws<InvalidOperationException>(() => enumerator.Current);
		Assert.False(enumerator.MoveNext());
		Assert.False(enumerator.MoveNext());
		enumerator.Reset();
		Assert.False(enumerator.MoveNext());
		var uninitialized = default(RingBuffer<int>.Enumerator);
		Assert.Throws<InvalidOperationException>(() => uninitialized.MoveNext());
	}

	[Theory]
	[InlineData(1)]
	[InlineData(7)]
	[InlineData(32)]
	public void RandomizedOperations_MatchReferenceQueue(int capacity)
	{
		var buffer = new RingBuffer<int>(capacity);
		var expected = new Queue<int>();
		var random = new System.Random(314159 + capacity);
		for (int iteration = 0; iteration < 20_000; iteration++)
		{
			int operation = random.Next(6);
			int value = random.Next();
			switch (operation)
			{
				case 0:
				case 1:
					bool canAdd = expected.Count < capacity;
					Assert.Equal(canAdd, buffer.TryEnqueue(value));
					if (canAdd) expected.Enqueue(value);
					break;
				case 2:
					bool overwritten = expected.Count == capacity;
					int oldest = overwritten ? expected.Dequeue() : default;
					Assert.Equal(overwritten, buffer.EnqueueOverwrite(value, out int evicted));
					Assert.Equal(oldest, evicted);
					expected.Enqueue(value);
					break;
				case 3:
					bool canRemove = expected.Count > 0;
					Assert.Equal(canRemove, buffer.TryDequeue(out int removed));
					Assert.Equal(canRemove ? expected.Dequeue() : default, removed);
					break;
				case 4:
					Assert.Equal(expected.Count > 0, buffer.TryPeek(out int peeked));
					Assert.Equal(expected.Count > 0 ? expected.Peek() : default, peeked);
					break;
				default:
					if (random.Next(16) == 0) { buffer.Clear(); expected.Clear(); }
					break;
			}
			Assert.Equal(expected.Count, buffer.Count);
			Assert.Equal(expected.Count == capacity, buffer.IsFull);
			Assert.Equal(expected.ToArray(), buffer.ToArray());
			var copy = new int[expected.Count];
			buffer.CopyTo(copy);
			Assert.Equal(expected.ToArray(), copy);
		}
	}

	[Theory]
	[InlineData(0)]
	[InlineData(1)]
	[InlineData(2)]
	public void RemovedOverwrittenAndClearedReferences_AreReleased(int operation)
	{
		var buffer = new RingBuffer<object>(2);
		var weak = PopulateAndRelease(buffer, operation);
		GC.Collect();
		GC.WaitForPendingFinalizers();
		GC.Collect();
		Assert.False(weak.IsAlive);
		GC.KeepAlive(buffer);
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	private static WeakReference PopulateAndRelease(RingBuffer<object> buffer, int operation)
	{
		var value = new object();
		var weak = new WeakReference(value);
		buffer.Enqueue(value);
		if (operation == 0) buffer.TryDequeue(out _);
		else if (operation == 1)
		{
			buffer.Enqueue(new object());
			buffer.EnqueueOverwrite(new object(), out _);
		}
		else
		{
			buffer.Enqueue(new object());
			buffer.EnqueueOverwrite(new object(), out _);
			buffer.EnqueueOverwrite(value, out _);
			buffer.Clear();
		}
		return weak;
	}
}
