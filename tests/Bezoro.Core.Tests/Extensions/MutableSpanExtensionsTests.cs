using System;
using System.Collections.Generic;
using Bezoro.Core.Extensions;
using Xunit;

namespace Bezoro.Core.Tests.Extensions;

public sealed class MutableSpanExtensionsTests
{
	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public void SeededRemovals_MatchIndependentLists(bool swapBack)
	{
		var random = new Random(9311);
		for (int run = 0; run < 100; run++)
		{
			var values = new int[random.Next(1, 128)];
			for (int index = 0; index < values.Length; index++) values[index] = index + 1;
			var expected = new List<int>(values);
			Span<int> live = values;
			while (live.Length > 0)
			{
				int index = random.Next(live.Length);
				if (swapBack)
				{
					expected[index] = expected[expected.Count - 1];
					expected.RemoveAt(expected.Count - 1);
					live = live.RemoveAtSwapBack(index);
				}
				else
				{
					expected.RemoveAt(index);
					live = live.RemoveAtOrdered(index);
				}
				Assert.Equal(expected.Count, live.Length);
				for (int current = 0; current < live.Length; current++) Assert.Equal(expected[current], live[current]);
				Assert.Equal(0, values[live.Length]);
			}
		}
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public void ReferenceRemoval_ClearsTailAndPreservesAdjacentSliceStorage(bool swapBack)
	{
		object first = new();
		object removed = new();
		object last = new();
		object sentinel = new();
		object?[] values = { sentinel, first, removed, last, sentinel };
		Span<object?> slice = values.AsSpan(1, 3);
		Span<object?> live = swapBack ? slice.RemoveAtSwapBack(1) : slice.RemoveAtOrdered(1);
		Assert.Equal(2, live.Length);
		Assert.Same(first, live[0]);
		Assert.Same(last, live[1]);
		Assert.Null(values[3]);
		Assert.Same(sentinel, values[0]);
		Assert.Same(sentinel, values[4]);
		live[0] = last;
		Assert.Same(last, values[1]);
	}

	[Theory]
	[InlineData(-1)]
	[InlineData(3)]
	[InlineData(int.MinValue)]
	[InlineData(int.MaxValue)]
	public void InvalidIndices_AreRejectedBeforeMutation(int index)
	{
		int[] values = { 1, 2, 3 };
		Assert.Throws<ArgumentOutOfRangeException>(() => values.AsSpan().RemoveAtOrdered(index));
		Assert.Equal(new[] { 1, 2, 3 }, values);
		Assert.Throws<ArgumentOutOfRangeException>(() => values.AsSpan().RemoveAtSwapBack(index));
		Assert.Equal(new[] { 1, 2, 3 }, values);
	}

	[Fact]
	public void EmptySpan_RejectsRemoval()
	{
		Assert.Throws<ArgumentOutOfRangeException>(() => Span<int>.Empty.RemoveAtOrdered(0));
		Assert.Throws<ArgumentOutOfRangeException>(() => Span<int>.Empty.RemoveAtSwapBack(0));
	}

	[Fact]
	public void WarmedRemovals_DoNotAllocateManagedMemory()
	{
		var values = new int[1024];
		for (int index = 0; index < 128; index++)
		{
			values.AsSpan().RemoveAtOrdered(8);
			values.AsSpan().RemoveAtSwapBack(8);
		}
		long before = GC.GetAllocatedBytesForCurrentThread();
		for (int iteration = 0; iteration < 1000; iteration++)
		{
			Span<int> live = values;
			live = live.RemoveAtOrdered(8);
			live = live.RemoveAtSwapBack(8);
		}
		Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
	}
}
