using System;
using System.Collections.Generic;
using Bezoro.Core.Extensions;
using Xunit;

namespace Bezoro.Core.Tests.Extensions;

public sealed class SortedSpanExtensionsTests
{
	[Theory]
	[InlineData(int.MinValue, 0, 0)]
	[InlineData(-10, 0, 1)]
	[InlineData(0, 1, 4)]
	[InlineData(1, 4, 4)]
	[InlineData(10, 4, 5)]
	[InlineData(int.MaxValue, 5, 5)]
	public void InsertionBounds_HandleDuplicatesAndMissingValues(int value, int lower, int upper)
	{
		int[] values = { -10, 0, 0, 0, 10 };
		ReadOnlySpan<int> span = values;
		Assert.Equal(lower, span.LowerBound(value));
		Assert.Equal(upper, span.UpperBound(value));
		Assert.Equal(lower, values.AsSpan().LowerBound(value));
		Assert.Equal(upper, values.AsSpan().UpperBound(value));
	}

	[Fact]
	public void SeededSortedSlices_MatchAnIndependentLinearScan()
	{
		var random = new Random(19841);
		for (int run = 0; run < 500; run++)
		{
			var values = new int[random.Next(256)];
			for (int index = 0; index < values.Length; index++) values[index] = random.Next(-32, 33);
			Array.Sort(values);
			int start = random.Next(values.Length + 1);
			int length = random.Next(values.Length - start + 1);
			ReadOnlySpan<int> slice = values.AsSpan(start, length);
			for (int value = -34; value <= 34; value++)
			{
				int lower = 0;
				while (lower < length && slice[lower] < value) lower++;
				int upper = lower;
				while (upper < length && slice[upper] <= value) upper++;
				Assert.Equal(lower, slice.LowerBound(value));
				Assert.Equal(upper, slice.UpperBound(value));
			}
		}
	}

	[Fact]
	public void ExplicitComparer_DefinesDescendingOrderAndNullHandling()
	{
		int[] descending = { 10, 5, 5, 0, -10 };
		var comparer = Comparer<int>.Create((left, right) => right.CompareTo(left));
		Assert.Equal(1, descending.AsSpan().LowerBound(5, comparer));
		Assert.Equal(3, descending.AsSpan().UpperBound(5, comparer));
		Assert.Equal(0, descending.AsSpan().LowerBound(20, comparer));
		Assert.Equal(5, descending.AsSpan().UpperBound(-20, comparer));
		string?[] names = { null, "alpha", "ALPHA", "beta" };
		Assert.Equal(1, names.AsSpan().LowerBound("Alpha", StringComparer.OrdinalIgnoreCase));
		Assert.Equal(3, names.AsSpan().UpperBound("Alpha", StringComparer.OrdinalIgnoreCase));
		Assert.Equal(0, names.AsSpan().LowerBound(null, StringComparer.OrdinalIgnoreCase));
		Assert.Equal(1, names.AsSpan().UpperBound(null, StringComparer.OrdinalIgnoreCase));
	}

	[Fact]
	public void EmptySpans_ReturnZeroWithoutCallingTheComparer()
	{
		var comparer = Comparer<int>.Create((_, _) => throw new InvalidOperationException());
		Assert.Equal(0, ReadOnlySpan<int>.Empty.LowerBound(5, comparer));
		Assert.Equal(0, ReadOnlySpan<int>.Empty.UpperBound(5, comparer));
		Assert.Equal(0, Span<int>.Empty.LowerBound(5));
		Assert.Equal(0, Span<int>.Empty.UpperBound(5));
	}

	[Fact]
	public void WarmedSearches_DoNotAllocateManagedMemory()
	{
		var values = new int[1024];
		for (int index = 0; index < values.Length; index++) values[index] = index / 4;
		int sink = 0;
		for (int index = 0; index < 100; index++) sink += values.AsSpan().LowerBound(index);
		long before = GC.GetAllocatedBytesForCurrentThread();
		for (int index = 0; index < 10_000; index++)
		{
			sink += values.AsSpan().LowerBound(index & 255);
			sink += values.AsSpan().UpperBound(index & 255);
		}
		long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
		Assert.Equal(0, allocated);
		Assert.True(sink > 0);
	}
}
