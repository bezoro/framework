using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Bezoro.Core.Collections;
using Xunit;

namespace Bezoro.Core.Tests.Collections;

public sealed class BitSetTests
{
	[Theory]
	[InlineData(0)]
	[InlineData(1)]
	[InlineData(63)]
	[InlineData(64)]
	[InlineData(65)]
	[InlineData(127)]
	[InlineData(128)]
	[InlineData(129)]
	[InlineData(511)]
	public void SeededOperations_MatchIndependentBooleanStorage(int length)
	{
		var random = new Random(7907 + length);
		var actual = new BitSet(length);
		var other = new BitSet(length);
		var expected = new bool[length];
		var expectedOther = new bool[length];
		for (int index = 0; index < length; index++)
		{
			expectedOther[index] = random.Next(2) != 0;
			other[index] = expectedOther[index];
		}

		for (int step = 0; step < 2000; step++)
		{
			int index = length == 0 ? 0 : random.Next(length);
			switch (random.Next(9))
			{
				case 0 when length > 0:
					Assert.Equal(!expected[index], actual.Set(index));
					expected[index] = true;
					break;
				case 1 when length > 0:
					Assert.Equal(expected[index], actual.Reset(index));
					expected[index] = false;
					break;
				case 2 when length > 0:
					expected[index] = !expected[index];
					Assert.Equal(expected[index], actual.Toggle(index));
					break;
				case 3:
					actual.UnionWith(other);
					for (int bit = 0; bit < length; bit++) expected[bit] |= expectedOther[bit];
					break;
				case 4:
					actual.IntersectWith(other);
					for (int bit = 0; bit < length; bit++) expected[bit] &= expectedOther[bit];
					break;
				case 5:
					actual.ExceptWith(other);
					for (int bit = 0; bit < length; bit++) expected[bit] &= !expectedOther[bit];
					break;
				case 6:
					actual.XorWith(other);
					for (int bit = 0; bit < length; bit++) expected[bit] ^= expectedOther[bit];
					break;
				case 7:
					actual.SetAll();
					Array.Fill(expected, true);
					break;
				case 8:
					actual.Clear();
					Array.Clear(expected, 0, length);
					break;
			}

			int count = 0;
			for (int bit = 0; bit < length; bit++)
			{
				Assert.Equal(expected[bit], actual.Contains(bit));
				if (expected[bit]) count++;
			}
			Assert.Equal(count, actual.Count);
			int start = random.Next(length + 1);
			int next = -1;
			for (int bit = start; bit < length; bit++)
				if (expected[bit]) { next = bit; break; }
			Assert.Equal(next, actual.FindNextSetBit(start));
			if ((step & 31) == 0)
			{
				var enumerated = new List<int>();
				foreach (int bit in actual) enumerated.Add(bit);
				Assert.Equal(count, enumerated.Count);
				int previous = -1;
				foreach (int bit in enumerated)
				{
					Assert.True(bit > previous);
					Assert.True(expected[bit]);
					previous = bit;
				}
			}
		}
	}

	[Theory]
	[InlineData(0)]
	[InlineData(1)]
	[InlineData(63)]
	[InlineData(64)]
	[InlineData(65)]
	[InlineData(128)]
	public void SetAllAndSelfOperations_RespectTheLogicalLength(int length)
	{
		var bits = new BitSet(length);
		bits.SetAll();
		bits.UnionWith(bits);
		bits.IntersectWith(bits);
		Assert.Equal(length, bits.Count);
		int enumerated = 0;
		foreach (int index in bits) Assert.Equal(enumerated++, index);
		Assert.Equal(length, enumerated);
		bits.XorWith(bits);
		Assert.Equal(0, bits.Count);
		bits.SetAll();
		bits.ExceptWith(bits);
		Assert.Equal(0, bits.Count);
		Assert.Equal(-1, bits.FindNextSetBit(length));
	}

	[Fact]
	public void InvalidIndicesAndLengths_AreRejectedBeforeMutation()
	{
		Assert.Throws<ArgumentOutOfRangeException>(() => new BitSet(-1));
		var bits = new BitSet(65);
		bits.Set(64);
		foreach (int index in new[] { -1, 65, int.MinValue, int.MaxValue })
		{
			Assert.Throws<ArgumentOutOfRangeException>(() => bits.Contains(index));
			Assert.Throws<ArgumentOutOfRangeException>(() => bits.Set(index));
			Assert.Throws<ArgumentOutOfRangeException>(() => bits.Reset(index));
			Assert.Throws<ArgumentOutOfRangeException>(() => bits.Toggle(index));
		}
		Assert.Throws<ArgumentOutOfRangeException>(() => bits.FindNextSetBit(-1));
		Assert.Throws<ArgumentOutOfRangeException>(() => bits.FindNextSetBit(66));
		Assert.True(bits[64]);
		Assert.Equal(1, bits.Count);
	}

	[Fact]
	public void InvalidSetOperations_AreRejectedWithoutChangingContentsOrEnumerator()
	{
		var bits = new BitSet(65);
		bits.Set(64);
		var cursor = bits.GetEnumerator();
		var other = new BitSet(64);
		Assert.Throws<ArgumentException>(() => bits.UnionWith(other));
		Assert.Throws<ArgumentException>(() => bits.IntersectWith(other));
		Assert.Throws<ArgumentException>(() => bits.ExceptWith(other));
		Assert.Throws<ArgumentException>(() => bits.XorWith(other));
		Assert.Throws<ArgumentNullException>(() => bits.UnionWith(null!));
		Assert.Throws<ArgumentNullException>(() => bits.IntersectWith(null!));
		Assert.Throws<ArgumentNullException>(() => bits.ExceptWith(null!));
		Assert.Throws<ArgumentNullException>(() => bits.XorWith(null!));
		Assert.True(cursor.MoveNext());
		Assert.Equal(64, cursor.Current);
		Assert.False(cursor.MoveNext());
		Assert.Equal(1, bits.Count);
	}

	[Fact]
	public void Enumerator_ValidatesPositionAndContentChanges()
	{
		var bits = new BitSet(65);
		bits.Set(64);
		var cursor = bits.GetEnumerator();
		Assert.Throws<InvalidOperationException>(() => cursor.Current);
		Assert.True(cursor.MoveNext());
		Assert.Equal(64, cursor.Current);
		Assert.False(cursor.MoveNext());
		Assert.False(cursor.MoveNext());
		Assert.Throws<InvalidOperationException>(() => cursor.Current);
		cursor = bits.GetEnumerator();
		Assert.False(bits.Set(64));
		Assert.True(cursor.MoveNext());
		bits.Toggle(0);
		Assert.Throws<InvalidOperationException>(() => cursor.MoveNext());
		Assert.Throws<InvalidOperationException>(() => cursor.Current);
		var emptyCursor = default(BitSet.Enumerator);
		Assert.False(emptyCursor.MoveNext());
		Assert.Throws<InvalidOperationException>(() => emptyCursor.Current);
	}

	[Fact]
	public void IndependentReaders_CanEnumerateAnUnchangedSet()
	{
		var bits = new BitSet(1024);
		for (int index = 0; index < bits.Length; index += 3) bits.Set(index);
		Parallel.For(0, 32, _ =>
		{
			int count = 0;
			foreach (int index in bits) { Assert.Equal(0, index % 3); count++; }
			Assert.Equal(342, count);
			Assert.Equal(342, bits.Count);
		});
	}

	[Fact]
	public void WarmedOperations_DoNotAllocateManagedMemory()
	{
		var left = new BitSet(1025);
		var right = new BitSet(1025);
		right.SetAll();
		int sink = 0;
		Exercise(128);
		long before = GC.GetAllocatedBytesForCurrentThread();
		Exercise(1000);
		long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
		Assert.Equal(0, allocated);
		Assert.True(sink > 0);

		void Exercise(int iterations)
		{
			for (int iteration = 0; iteration < iterations; iteration++)
			{
				left.Set(iteration % left.Length);
				left.Reset(0);
				left.Toggle(64);
				left.UnionWith(right);
				left.IntersectWith(right);
				sink += left.Count + left.FindNextSetBit(64);
				foreach (int index in left) sink += index;
				left.ExceptWith(right);
				left.XorWith(right);
				left.Clear();
			}
		}
	}
}
