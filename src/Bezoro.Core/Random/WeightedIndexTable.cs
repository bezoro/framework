namespace Bezoro.Core.Randomness;

/// <summary>An immutable, exact integer-weight distribution with allocation-free sampling.</summary>
/// <remarks>
/// Construction stores cumulative weights in one owned array. Zero weights are allowed but cannot be
/// selected. Sampling takes O(log n) time and does not retain caller-owned storage. Concurrent readers
/// may share a table if each worker owns its random state.
/// </remarks>
public sealed class WeightedIndexTable
{
	private readonly ulong[] _cumulativeWeights;

	/// <summary>Copies a nonempty distribution whose total is positive and fits in <see cref="ulong"/>.</summary>
	/// <exception cref="ArgumentException">The distribution is empty, all zero, or its total overflows.</exception>
	public WeightedIndexTable(ReadOnlySpan<ulong> weights)
	{
		if (weights.IsEmpty) throw new ArgumentException("At least one weight is required.", nameof(weights));
		ulong total = 0;
		foreach (ulong weight in weights)
		{
			if (ulong.MaxValue - total < weight)
				throw new ArgumentException("The sum of weights exceeds UInt64.MaxValue.", nameof(weights));
			total += weight;
		}
		if (total == 0) throw new ArgumentException("At least one weight must be positive.", nameof(weights));

		TotalWeight = total;
		_cumulativeWeights = new ulong[weights.Length];
		ulong cumulative = 0;
		for (int index = 0; index < weights.Length; index++)
		{
			cumulative += weights[index];
			_cumulativeWeights[index] = cumulative;
		}
	}

	/// <summary>The number of entries, including zero-weight entries.</summary>
	public int Count => _cumulativeWeights.Length;

	/// <summary>The sum of all weights and exclusive upper bound for a selection ticket.</summary>
	public ulong TotalWeight { get; }

	/// <summary>Returns the original weight at an index.</summary>
	public ulong GetWeight(int index)
	{
		if (index < 0 || index >= Count) throw new ArgumentOutOfRangeException(nameof(index));
		return _cumulativeWeights[index] - (index == 0 ? 0 : _cumulativeWeights[index - 1]);
	}

	/// <summary>Selects an index from an unbiased ticket in <c>[0, TotalWeight)</c>.</summary>
	public int Pick(ref Pcg32State state) => GetIndexForTicket(Pcg32.NextUInt64(ref state, TotalWeight));

	/// <summary>Maps an exact ticket in <c>[0, TotalWeight)</c> to its positive-weight entry.</summary>
	/// <remarks>This also permits deterministic selection from a caller-provided ticket source.</remarks>
	public int GetIndexForTicket(ulong ticket)
	{
		if (ticket >= TotalWeight) throw new ArgumentOutOfRangeException(nameof(ticket));
		int lower = 0;
		int upper = _cumulativeWeights.Length - 1;
		while (lower < upper)
		{
			int middle = lower + ((upper - lower) >> 1);
			if (ticket < _cumulativeWeights[middle]) upper = middle;
			else lower = middle + 1;
		}
		return lower;
	}
}
