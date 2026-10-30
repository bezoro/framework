namespace Bezoro.Core.Randomness;

/// <summary>Allocation-free random operations with explicit, caller-owned PCG state.</summary>
/// <remarks>
/// This generator is suitable for deterministic game simulation, not secrets or security.
/// All argument validation happens before advancing the state. Rejection sampling can consume
/// more than one raw draw. Do not share one state variable between concurrent writers.
/// </remarks>
public static class Pcg32
{
	/// <summary>Identifies the stream, range, floating-point conversion, and shuffle rules used for replay.</summary>
	public const string AlgorithmId = "pcg32-xsh-rr-64-32-v1";

	/// <summary>Returns a value covering the complete <see cref="uint"/> range, consuming one raw draw.</summary>
	public static uint NextUInt32(ref Pcg32State state)
	{
		state = state.Next(out uint value);
		return value;
	}

	/// <summary>Returns an unbiased value in <c>[0, exclusiveUpperBound)</c>.</summary>
	/// <exception cref="ArgumentOutOfRangeException">The bound is zero.</exception>
	public static uint NextUInt32(ref Pcg32State state, uint exclusiveUpperBound)
	{
		if (exclusiveUpperBound == 0) throw new ArgumentOutOfRangeException(nameof(exclusiveUpperBound));
		uint threshold = unchecked(0u - exclusiveUpperBound) % exclusiveUpperBound;
		while (true)
		{
			uint value = NextUInt32(ref state);
			if (value >= threshold) return value % exclusiveUpperBound;
		}
	}

	/// <summary>Returns a full-width value by concatenating two raw draws, first draw in the high bits.</summary>
	public static ulong NextUInt64(ref Pcg32State state) =>
		((ulong)NextUInt32(ref state) << 32) | NextUInt32(ref state);

	/// <summary>Returns an unbiased value in <c>[0, exclusiveUpperBound)</c> using full-width rejection sampling.</summary>
	/// <exception cref="ArgumentOutOfRangeException">The bound is zero.</exception>
	public static ulong NextUInt64(ref Pcg32State state, ulong exclusiveUpperBound)
	{
		if (exclusiveUpperBound == 0) throw new ArgumentOutOfRangeException(nameof(exclusiveUpperBound));
		ulong threshold = unchecked(0UL - exclusiveUpperBound) % exclusiveUpperBound;
		while (true)
		{
			ulong value = NextUInt64(ref state);
			if (value >= threshold) return value % exclusiveUpperBound;
		}
	}

	/// <summary>Returns a value covering the complete signed 32-bit range.</summary>
	public static int NextInt32(ref Pcg32State state) => unchecked((int)NextUInt32(ref state));

	/// <summary>Returns an unbiased value in <c>[minInclusive, maxExclusive)</c>, including ranges crossing zero.</summary>
	/// <exception cref="ArgumentOutOfRangeException">The range is empty or reversed.</exception>
	public static int NextInt32(ref Pcg32State state, int minInclusive, int maxExclusive)
	{
		if (minInclusive >= maxExclusive) throw new ArgumentOutOfRangeException(nameof(maxExclusive));
		uint width = (uint)((long)maxExclusive - minInclusive);
		return (int)(minInclusive + (long)NextUInt32(ref state, width));
	}

	/// <summary>Returns a value covering the complete signed 64-bit range, consuming two raw draws.</summary>
	public static long NextInt64(ref Pcg32State state) => unchecked((long)NextUInt64(ref state));

	/// <summary>Returns an unbiased value in <c>[minInclusive, maxExclusive)</c>, including ranges crossing zero.</summary>
	/// <exception cref="ArgumentOutOfRangeException">The range is empty or reversed.</exception>
	public static long NextInt64(ref Pcg32State state, long minInclusive, long maxExclusive)
	{
		if (minInclusive >= maxExclusive) throw new ArgumentOutOfRangeException(nameof(maxExclusive));
		ulong width = unchecked((ulong)maxExclusive - (ulong)minInclusive);
		ulong offset = NextUInt64(ref state, width);
		return unchecked((long)((ulong)minInclusive + offset));
	}

	/// <summary>Returns a value in <c>[0, 1)</c> with 24 random bits, consuming one raw draw.</summary>
	public static float NextSingle(ref Pcg32State state) => (NextUInt32(ref state) >> 8) * (1.0f / 16777216.0f);

	/// <summary>Returns a value in <c>[0, 1)</c> with 53 random bits from the high bits of two concatenated draws.</summary>
	public static double NextDouble(ref Pcg32State state) => (NextUInt64(ref state) >> 11) * (1.0 / 9007199254740992.0);

	/// <summary>Shuffles a caller-owned span in place using descending Fisher-Yates and unbiased indices.</summary>
	/// <remarks>Empty and single-element spans consume no random values.</remarks>
	public static void Shuffle<T>(ref Pcg32State state, Span<T> values)
	{
		for (int index = values.Length - 1; index > 0; index--)
		{
			int selected = (int)NextUInt32(ref state, (uint)index + 1);
			(values[index], values[selected]) = (values[selected], values[index]);
		}
	}
}
