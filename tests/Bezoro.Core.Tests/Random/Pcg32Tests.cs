using System;
using System.Linq;
using Bezoro.Core.Randomness;
using Xunit;

namespace Bezoro.Core.Tests.Randomness;

public sealed class Pcg32Tests
{
	[Fact]
	public void ReferenceSeed_ProducesThePublishedPcg32Sequence()
	{
		// The six hexadecimal outputs from pcg32-demo, initstate=42, initseq=54:
		// https://www.pcg-random.org/using-pcg-c-basic.html
		uint[] expected = [0xa15c02b7, 0x7b47f409, 0xba1d3330, 0x83d2f293, 0xbfa4784b, 0xcbed606e];
		var state = Pcg32State.Create(42, 54);
		Assert.Equal(0x185706b82c2e03f8UL, state.Value);
		foreach (uint value in expected) Assert.Equal(value, Pcg32.NextUInt32(ref state));
	}

	[Fact]
	public void PureStep_LeavesItsInputUnchangedAndMatchesTheRefConvenienceMethod()
	{
		var original = Pcg32State.Create(42, 54);
		var copy = original;
		var next = original.Next(out uint pureValue);
		Assert.Equal(pureValue, Pcg32.NextUInt32(ref copy));
		Assert.Equal(next, copy);
		Assert.Equal(Pcg32State.Create(42, 54), original);
	}

	[Fact]
	public void RawSnapshot_RestoresTheExactContinuation()
	{
		var state = Pcg32State.Create(ulong.MaxValue, Pcg32State.MaxStream);
		for (int i = 0; i < 123; i++) Pcg32.NextUInt32(ref state);
		var restored = new Pcg32State(state.Value, state.Stream);
		for (int i = 0; i < 1_000; i++)
		{
			Assert.Equal(Pcg32.NextUInt64(ref state), Pcg32.NextUInt64(ref restored));
			Assert.Equal(Pcg32.NextInt32(ref state, int.MinValue, int.MaxValue),
				Pcg32.NextInt32(ref restored, int.MinValue, int.MaxValue));
			Assert.Equal(Pcg32.NextDouble(ref state), Pcg32.NextDouble(ref restored));
		}
		Assert.Equal(state, restored);
	}

	[Fact]
	public void DefaultState_IsValidAndDoesNotStayAtZero()
	{
		Pcg32State state = default;
		Assert.Equal(new Pcg32State(0, 0), state);
		Assert.Equal(0u, Pcg32.NextUInt32(ref state));
		Assert.Equal(1UL, state.Value);
		Assert.Equal(0u, Pcg32.NextUInt32(ref state));
		Assert.NotEqual(0u, Pcg32.NextUInt32(ref state));
		Assert.NotEqual(default(Pcg32State), Pcg32State.Create(0));
	}

	[Fact]
	public void StreamSelector_RejectsBitsThatWouldAliasAnotherStream()
	{
		Assert.Throws<ArgumentOutOfRangeException>(() => new Pcg32State(0, Pcg32State.MaxStream + 1));
		Assert.Throws<ArgumentOutOfRangeException>(() => Pcg32State.Create(0, ulong.MaxValue));
		Assert.Equal(Pcg32State.MaxStream, Pcg32State.Create(0, Pcg32State.MaxStream).Stream);
	}

	[Fact]
	public void DifferentStreams_FromTheSameSeedProduceDifferentSequences()
	{
		var first = Pcg32State.Create(42, 0);
		var second = Pcg32State.Create(42, Pcg32State.MaxStream);
		var firstValues = new uint[16];
		var secondValues = new uint[16];
		for (int i = 0; i < firstValues.Length; i++)
		{
			firstValues[i] = Pcg32.NextUInt32(ref first);
			secondValues[i] = Pcg32.NextUInt32(ref second);
		}
		Assert.False(firstValues.SequenceEqual(secondValues));
	}

	[Fact]
	public void UInt64_ConcatenatesHighDrawThenLowDraw()
	{
		var state = Pcg32State.Create(42, 54);
		Assert.Equal(0xa15c02b77b47f409UL, Pcg32.NextUInt64(ref state));
		Assert.Equal(0xba1d333083d2f293UL, Pcg32.NextUInt64(ref state));
	}

	[Fact]
	public void SignedFullWidth_PreservesEveryRawBit()
	{
		var state = Pcg32State.Create(42, 54);
		var raw = state;
		for (int i = 0; i < 100; i++)
		{
			Assert.Equal(unchecked((int)Pcg32.NextUInt32(ref raw)), Pcg32.NextInt32(ref state));
			Assert.Equal(unchecked((long)Pcg32.NextUInt64(ref raw)), Pcg32.NextInt64(ref state));
		}
	}

	[Fact]
	public void BoundedUInt32_RejectsTheLowBiasedPartOfTheRawRange()
	{
		// Independent reference PCG rejection sampler, bound=2^31+1. Its second raw
		// draw is below the threshold and must be discarded, rather than reduced modulo.
		uint[] expected = [559678134, 974992175, 64156306, 1067743306, 1273847917, 1069982636, 19922796, 1713320025];
		var state = Pcg32State.Create(42, 54);
		foreach (uint value in expected)
			Assert.Equal(value, Pcg32.NextUInt32(ref state, 0x80000001));
		Assert.Equal(0x91723b7b84518c9dUL, state.Value);
	}

	[Theory]
	[InlineData(1u)]
	[InlineData(2u)]
	[InlineData(17u)]
	[InlineData(0x80000001u)]
	[InlineData(uint.MaxValue)]
	public void BoundedUInt32_AlwaysStaysBelowTheExclusiveBound(uint bound)
	{
		var state = Pcg32State.Create(98, 127);
		for (int i = 0; i < 10_000; i++) Assert.True(Pcg32.NextUInt32(ref state, bound) < bound);
	}

	[Theory]
	[InlineData(1UL)]
	[InlineData(2UL)]
	[InlineData(17UL)]
	[InlineData(0x8000000000000001UL)]
	[InlineData(ulong.MaxValue)]
	public void BoundedUInt64_AlwaysStaysBelowTheExclusiveBound(ulong bound)
	{
		var state = Pcg32State.Create(98, 127);
		for (int i = 0; i < 10_000; i++) Assert.True(Pcg32.NextUInt64(ref state, bound) < bound);
	}

	[Fact]
	public void BoundedUInt64_RejectsZeroWhenTheOverflowThresholdIsOne()
	{
		Pcg32State actual = default;
		Pcg32State expected = default;
		Assert.Equal(0UL, Pcg32.NextUInt64(ref expected));
		ulong next = Pcg32.NextUInt64(ref expected);
		Assert.NotEqual(0UL, next);
		Assert.Equal(next, Pcg32.NextUInt64(ref actual, ulong.MaxValue));
		Assert.Equal(expected, actual);
	}

	[Theory]
	[InlineData(int.MinValue, int.MaxValue)]
	[InlineData(int.MinValue, int.MinValue + 1)]
	[InlineData(int.MaxValue - 1, int.MaxValue)]
	[InlineData(-20, 21)]
	public void SignedRanges_HandleExtremeEndpointsWithoutOverflow(int minimum, int maximum)
	{
		var state = Pcg32State.Create(ulong.MaxValue, Pcg32State.MaxStream);
		for (int i = 0; i < 10_000; i++)
		{
			int value = Pcg32.NextInt32(ref state, minimum, maximum);
			Assert.InRange(value, minimum, maximum - 1);
		}
	}

	[Theory]
	[InlineData(long.MinValue, long.MaxValue)]
	[InlineData(long.MinValue, long.MinValue + 1)]
	[InlineData(long.MaxValue - 1, long.MaxValue)]
	[InlineData(-20L, 21L)]
	public void SignedLongRanges_HandleExtremeEndpointsWithoutOverflow(long minimum, long maximum)
	{
		var state = Pcg32State.Create(ulong.MaxValue, Pcg32State.MaxStream);
		for (int i = 0; i < 10_000; i++)
		{
			long value = Pcg32.NextInt64(ref state, minimum, maximum);
			Assert.InRange(value, minimum, maximum - 1);
		}
	}

	[Fact]
	public void InvalidRanges_ThrowWithoutConsumingAnyState()
	{
		var state = Pcg32State.Create(42, 54);
		var snapshot = state;
		Assert.Throws<ArgumentOutOfRangeException>(() => Pcg32.NextUInt32(ref state, 0));
		Assert.Throws<ArgumentOutOfRangeException>(() => Pcg32.NextUInt64(ref state, 0));
		Assert.Throws<ArgumentOutOfRangeException>(() => Pcg32.NextInt32(ref state, 2, 2));
		Assert.Throws<ArgumentOutOfRangeException>(() => Pcg32.NextInt32(ref state, 2, -2));
		Assert.Throws<ArgumentOutOfRangeException>(() => Pcg32.NextInt64(ref state, 2, 2));
		Assert.Throws<ArgumentOutOfRangeException>(() => Pcg32.NextInt64(ref state, 2, -2));
		Assert.Equal(snapshot, state);
	}

	[Fact]
	public void FloatingPointConversions_HaveVersionedGoldenBitPatterns()
	{
		var singleState = Pcg32State.Create(42, 54);
		var doubleState = singleState;
		Assert.Equal(0x3f215c02, BitConverter.SingleToInt32Bits(Pcg32.NextSingle(ref singleState)));
		Assert.Equal(0x3fe42b8056ef68feL, BitConverter.DoubleToInt64Bits(Pcg32.NextDouble(ref doubleState)));
		var oneDraw = Pcg32State.Create(42, 54);
		Pcg32.NextUInt32(ref oneDraw);
		Assert.Equal(oneDraw, singleState);
		Pcg32.NextUInt32(ref oneDraw);
		Assert.Equal(oneDraw, doubleState);
	}

	[Fact]
	public void FloatingPointConversions_NeverReachOne()
	{
		var state = Pcg32State.Create(78);
		for (int i = 0; i < 100_000; i++)
		{
			float single = Pcg32.NextSingle(ref state);
			double value = Pcg32.NextDouble(ref state);
			Assert.True(single >= 0 && single < 1);
			Assert.True(value >= 0 && value < 1);
		}
	}

	[Fact]
	public void Shuffle_MatchesTheVersionedDescendingFisherYatesOrder()
	{
		var state = Pcg32State.Create(42, 54);
		int[] values = Enumerable.Range(0, 10).ToArray();
		Pcg32.Shuffle(ref state, values.AsSpan());
		Assert.Equal(new[] { 8, 2, 6, 4, 5, 1, 7, 0, 9, 3 }, values);
		Assert.Equal(0x91723b7b84518c9dUL, state.Value);
	}

	[Fact]
	public void Shuffle_OnlyChangesTheRequestedSliceAndPreservesAllItems()
	{
		var state = Pcg32State.Create(42, 54);
		string[] values = ["start", "a", "b", "c", "d", "end"];
		Pcg32.Shuffle(ref state, values.AsSpan(1, 4));
		Assert.Equal("start", values[0]);
		Assert.Equal("end", values[^1]);
		Assert.Equal(new[] { "a", "b", "c", "d" }, values.Skip(1).Take(4).OrderBy(value => value));
	}

	[Fact]
	public void TrivialShuffles_DoNotConsumeState()
	{
		var state = Pcg32State.Create(42, 54);
		var snapshot = state;
		Pcg32.Shuffle<int>(ref state, Span<int>.Empty);
		int[] item = [7];
		Pcg32.Shuffle(ref state, item.AsSpan());
		Assert.Equal(7, item[0]);
		Assert.Equal(snapshot, state);
	}

	[Fact]
	public void SnapshotEquality_IncludesTheStream()
	{
		var state = new Pcg32State(5, 2);
		Assert.Equal(state, new Pcg32State(5, 2));
		Assert.True(state == new Pcg32State(5, 2));
		Assert.True(state != new Pcg32State(5, 3));
		Assert.NotEqual(state, new Pcg32State(6, 2));
		Assert.Equal(state.GetHashCode(), new Pcg32State(5, 2).GetHashCode());
	}
}
