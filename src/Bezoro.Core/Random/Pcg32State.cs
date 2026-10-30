namespace Bezoro.Core.Randomness;

/// <summary>An immutable snapshot of the PCG XSH RR 64/32 random generator.</summary>
/// <remarks>
/// Copy a state to branch a deterministic simulation. Mutating one caller-owned copy does not
/// affect another. The default value is a valid raw state with value zero and stream zero;
/// use <see cref="Create"/> for the reference algorithm's seed initialization.
/// </remarks>
public readonly struct Pcg32State : IEquatable<Pcg32State>
{
	/// <summary>The largest distinct stream selector, whose increment is <see cref="ulong.MaxValue"/>.</summary>
	public const ulong MaxStream = ulong.MaxValue >> 1;

	/// <summary>Restores a raw snapshot without running the seed initialization sequence.</summary>
	/// <param name="value">The saved 64-bit internal state.</param>
	/// <param name="stream">The saved 63-bit stream selector.</param>
	public Pcg32State(ulong value, ulong stream)
	{
		if (stream > MaxStream) throw new ArgumentOutOfRangeException(nameof(stream));
		Value = value;
		Stream = stream;
	}

	/// <summary>The internal state to persist with <see cref="Stream"/> and <see cref="Pcg32.AlgorithmId"/>.</summary>
	public ulong Value { get; }

	/// <summary>The stream selector. Its corresponding LCG increment is <c>(Stream &lt;&lt; 1) | 1</c>.</summary>
	public ulong Stream { get; }

	/// <summary>Initializes a stream using the reference PCG seed procedure.</summary>
	/// <param name="seed">The seed added between two initialization steps.</param>
	/// <param name="stream">A distinct stream selector in <c>0..2^63-1</c>.</param>
	public static Pcg32State Create(ulong seed, ulong stream = 0)
	{
		var state = new Pcg32State(0, stream).Next(out _);
		return new Pcg32State(unchecked(state.Value + seed), stream).Next(out _);
	}

	/// <summary>Returns the next state and the output from this state, without modifying this value.</summary>
	public Pcg32State Next(out uint value)
	{
		ulong previous = Value;
		uint shifted = unchecked((uint)(((previous >> 18) ^ previous) >> 27));
		int rotation = (int)(previous >> 59);
		value = (shifted >> rotation) | (shifted << ((-rotation) & 31));
		ulong next = unchecked(previous * 6364136223846793005UL + ((Stream << 1) | 1));
		return new Pcg32State(next, Stream);
	}

	/// <summary>Compares both the raw state and stream selector.</summary>
	public bool Equals(Pcg32State other) => Value == other.Value && Stream == other.Stream;

	/// <inheritdoc/>
	public override bool Equals(object? obj) => obj is Pcg32State other && Equals(other);

	/// <inheritdoc/>
	public override int GetHashCode() => HashCode.Combine(Value, Stream);

	/// <summary>Compares two snapshots.</summary>
	public static bool operator ==(Pcg32State left, Pcg32State right) => left.Equals(right);

	/// <summary>Compares two snapshots for inequality.</summary>
	public static bool operator !=(Pcg32State left, Pcg32State right) => !left.Equals(right);
}
