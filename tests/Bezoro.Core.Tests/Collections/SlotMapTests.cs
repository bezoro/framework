using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Bezoro.Core.Collections;
using Xunit;

namespace Bezoro.Core.Tests.Collections;

public sealed class SlotMapTests
{
	[Fact]
	public void ZeroCapacity_IsEmptyAndRejectsAdds()
	{
		var map = new SlotMap<int>(0);
		Assert.Equal(0, map.Capacity);
		Assert.Equal(0, map.AvailableCapacity);
		Assert.Empty(map);
		Assert.False(map.TryAdd(1, out var handle));
		Assert.True(handle.IsDefault);
		Assert.Throws<InvalidOperationException>(() => map.Add(1));
		map.Clear();
	}

	[Fact]
	public void NegativeCapacity_IsRejected() => Assert.Throws<ArgumentOutOfRangeException>(() => new SlotMap<int>(-1));

	[Fact]
	public void AddReadAndReplace_PreserveIdentity()
	{
		var map = new SlotMap<int>(2);
		var first = map.Add(10);
		var second = map.Add(20);
		Assert.NotEqual(first, second);
		Assert.False(first.IsDefault);
		Assert.True(map.Contains(first));
		Assert.True(map.TryGet(first, out int value));
		Assert.Equal(10, value);
		map[first] = 30;
		Assert.Equal(30, map[first]);
		Assert.Equal(first, map.GetHandleAt(0));
		Assert.Equal(new[] { 30, 20 }, map.Values.ToArray());
		Assert.Equal(2, map.Count);
		Assert.Equal(0, map.AvailableCapacity);
	}

	[Fact]
	public void CapacityExhaustion_DoesNotMutateExistingValues()
	{
		var map = new SlotMap<int>(1);
		var handle = map.Add(7);
		Assert.False(map.TryAdd(9, out var rejected));
		Assert.True(rejected.IsDefault);
		Assert.Throws<InvalidOperationException>(() => map.Add(9));
		Assert.Equal(7, map[handle]);
		Assert.Single(map);
	}

	[Fact]
	public void RemovalCompactsDenseValuesWithoutChangingMovedHandle()
	{
		var map = new SlotMap<string>(4);
		var a = map.Add("a");
		var b = map.Add("b");
		var c = map.Add("c");
		var d = map.Add("d");
		Assert.True(map.TryRemove(b, out string removed));
		Assert.Equal("b", removed);
		Assert.Equal(new[] { "a", "d", "c" }, map.Values.ToArray());
		Assert.Equal(d, map.GetHandleAt(1));
		Assert.Equal("d", map[d]);
		Assert.Equal("a", map[a]);
		Assert.Equal("c", map[c]);
		Assert.False(map.Contains(b));
		Assert.False(map.TryRemove(b, out removed));
		Assert.Null(removed);
	}

	[Fact]
	public void ReusedSlot_DoesNotResurrectOldHandle()
	{
		var map = new SlotMap<int>(1);
		var old = map.Add(1);
		Assert.True(map.Remove(old));
		var replacement = map.Add(2);
		Assert.NotEqual(old, replacement);
		Assert.False(map.Contains(old));
		Assert.False(map.TryGet(old, out int absent));
		Assert.Equal(0, absent);
		Assert.Equal(2, map[replacement]);
	}

	[Fact]
	public void DefaultStaleAndForeignHandles_AreRejected()
	{
		var map = new SlotMap<int>(1);
		var other = new SlotMap<int>(1);
		var otherType = new SlotMap<string>(1);
		var local = map.Add(1);
		var foreign = other.Add(2);
		var foreignType = otherType.Add("x");
		Assert.False(local.Equals(default(SlotHandle)));
		foreach (var invalid in new[] { default(SlotHandle), foreign, foreignType })
		{
			Assert.False(map.Contains(invalid));
			Assert.False(map.TryGet(invalid, out _));
			Assert.False(map.Remove(invalid));
			Assert.Throws<ArgumentException>(() => map[invalid]);
			Assert.Throws<ArgumentException>(() => map[invalid] = 4);
		}
		Assert.Equal(1, map[local]);
	}

	[Fact]
	public void Clear_InvalidatesAllHandlesAndRestoresReusableCapacity()
	{
		var map = new SlotMap<int>(3);
		var handles = new[] { map.Add(1), map.Add(2), map.Add(3) };
		map.Clear();
		map.Clear();
		Assert.Equal(3, map.AvailableCapacity);
		Assert.Empty(map);
		for (int i = 0; i < 3; i++) map.Add(i + 10);
		Assert.All(handles, handle => Assert.False(map.Contains(handle)));
		Assert.Equal(3, map.Count);
	}

	[Fact]
	public void ExhaustedGeneration_RetiresSlotInsteadOfWrapping()
	{
		var map = new SlotMap<int>(1, uint.MaxValue - 1);
		var initial = map.Add(1);
		Assert.True(map.Remove(initial));
		var final = map.Add(2);
		Assert.NotEqual(initial, final);
		Assert.Equal(uint.MaxValue, final.Generation);
		Assert.True(map.Remove(final));
		Assert.Empty(map);
		Assert.Equal(1, map.Capacity);
		Assert.Equal(0, map.AvailableCapacity);
		Assert.False(map.TryAdd(3, out _));
		map.Clear();
		Assert.False(map.Contains(initial));
		Assert.False(map.Contains(final));
		Assert.False(map.TryAdd(3, out _));
	}

	[Fact]
	public void Clear_AlsoRetiresExhaustedGenerations()
	{
		var map = new SlotMap<int>(2, uint.MaxValue);
		var first = map.Add(1);
		var second = map.Add(2);
		map.Clear();
		Assert.Equal(0, map.AvailableCapacity);
		Assert.False(map.TryAdd(3, out _));
		Assert.False(map.Contains(first));
		Assert.False(map.Contains(second));
	}

	[Theory]
	[InlineData(-1)]
	[InlineData(1)]
	public void DenseIndices_AreValidated(int index)
	{
		var map = new SlotMap<int>(2);
		map.Add(3);
		Assert.Throws<ArgumentOutOfRangeException>(() => map.GetHandleAt(index));
	}

	[Fact]
	public void Handles_SupportValueEqualityAndHashCollections()
	{
		var map = new SlotMap<int>(1);
		var handle = map.Add(5);
		var copy = map.GetHandleAt(0);
		Assert.True(handle == copy);
		Assert.False(handle != copy);
		Assert.True(handle.Equals((object)copy));
		Assert.False(handle.Equals("other"));
		Assert.Equal(handle.GetHashCode(), copy.GetHashCode());
		Assert.Single(new HashSet<SlotHandle> { handle, copy });
	}

	[Fact]
	public void ConcurrentConstruction_IssuesDistinctOwnerIdentities()
	{
		var maps = new SlotMap<int>[128];
		var handles = new SlotHandle[maps.Length];
		Parallel.For(0, maps.Length, index =>
		{
			maps[index] = new SlotMap<int>(1);
			handles[index] = maps[index].Add(index);
		});
		Assert.Equal(maps.Length, handles.Distinct().Count());
		for (int i = 0; i < maps.Length; i++)
			Assert.False(maps[i].Contains(handles[(i + 1) % maps.Length]));
	}

	[Theory]
	[InlineData(0)]
	[InlineData(1)]
	[InlineData(2)]
	[InlineData(3)]
	public void Enumeration_RejectsMutations(int mutation)
	{
		var map = new SlotMap<int>(2);
		var first = map.Add(1);
		var enumerator = map.GetEnumerator();
		Assert.True(enumerator.MoveNext());
		switch (mutation)
		{
			case 0: map.Add(2); break;
			case 1: map.Remove(first); break;
			case 2: map[first] = 9; break;
			default: map.Clear(); break;
		}
		Assert.Throws<InvalidOperationException>(() => enumerator.MoveNext());
		Assert.Throws<InvalidOperationException>(() => enumerator.Current);
		Assert.Throws<InvalidOperationException>(() => enumerator.Reset());
	}

	[Fact]
	public void Enumeration_HasDefinedBoundaryAndResetBehavior()
	{
		var map = new SlotMap<int>(1);
		var handle = map.Add(5);
		var enumerator = map.GetEnumerator();
		Assert.Throws<InvalidOperationException>(() => enumerator.Current);
		Assert.True(enumerator.MoveNext());
		Assert.Equal(5, enumerator.Current);
		Assert.False(map.TryAdd(6, out _));
		Assert.False(map.Remove(default));
		Assert.Equal(5, enumerator.Current);
		Assert.False(enumerator.MoveNext());
		Assert.False(enumerator.MoveNext());
		Assert.Throws<InvalidOperationException>(() => enumerator.Current);
		enumerator.Reset();
		Assert.True(enumerator.MoveNext());
		Assert.Equal(5, enumerator.Current);
		Assert.True(map.Contains(handle));
		var uninitialized = default(SlotMap<int>.Enumerator);
		Assert.Throws<InvalidOperationException>(() => uninitialized.MoveNext());
	}

	[Theory]
	[InlineData(1729)]
	[InlineData(8675309)]
	public void RandomizedOperations_MatchReferenceDictionary(int seed)
	{
		const int capacity = 32;
		var map = new SlotMap<int>(capacity);
		var expected = new Dictionary<SlotHandle, int>();
		var random = new System.Random(seed);
		for (int iteration = 0; iteration < 10_000; iteration++)
		{
			int operation = random.Next(6);
			if (operation < 2)
			{
				int value = random.Next();
				bool added = map.TryAdd(value, out var handle);
				Assert.Equal(expected.Count < capacity, added);
				if (added) expected.Add(handle, value);
			}
			else if (operation < 4 && expected.Count > 0)
			{
				var handle = expected.Keys.ElementAt(random.Next(expected.Count));
				if (operation == 2)
				{
					Assert.True(map.TryRemove(handle, out int removed));
					Assert.Equal(expected[handle], removed);
					expected.Remove(handle);
					Assert.False(map.Contains(handle));
				}
				else map[handle] = expected[handle] = random.Next();
			}
			else if (operation == 4 && random.Next(8) == 0)
			{
				var previousHandles = expected.Keys.ToArray();
				map.Clear();
				expected.Clear();
				Assert.All(previousHandles, handle => Assert.False(map.Contains(handle)));
			}
			Assert.Equal(expected.Count, map.Count);
			Assert.Equal(capacity - expected.Count, map.AvailableCapacity);
			for (int index = 0; index < map.Count; index++)
			{
				var handle = map.GetHandleAt(index);
				Assert.Equal(expected[handle], map.Values[index]);
				Assert.Equal(expected[handle], map[handle]);
			}
		}
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public void RemovedAndClearedValues_AreNotRetained(bool clear)
	{
		var map = new SlotMap<object>(2);
		var weak = PopulateAndRelease(map, clear);
		GC.Collect();
		GC.WaitForPendingFinalizers();
		GC.Collect();
		Assert.False(weak.IsAlive);
		GC.KeepAlive(map);
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	private static WeakReference PopulateAndRelease(SlotMap<object> map, bool clear)
	{
		var value = new object();
		var weak = new WeakReference(value);
		var handle = map.Add(value);
		if (clear) map.Clear();
		else map.Remove(handle);
		return weak;
	}
}
