using System;
using System.Linq;
using System.Threading.Tasks;
using Bezoro.Core.Randomness;
using Xunit;

namespace Bezoro.Core.Tests.Randomness;

public sealed class WeightedIndexTableTests
{
	[Fact]
	public void ExactTickets_ProduceEachWeightAndNeverSelectZeroWeightEntries()
	{
		ulong[] weights = [0, 2, 0, 3, 1, 0];
		var table = new WeightedIndexTable(weights);
		Assert.Equal(6, table.Count);
		Assert.Equal(6UL, table.TotalWeight);
		int[] counts = new int[weights.Length];
		for (ulong ticket = 0; ticket < table.TotalWeight; ticket++) counts[table.GetIndexForTicket(ticket)]++;
		Assert.Equal(new[] { 0, 2, 0, 3, 1, 0 }, counts);
		for (int i = 0; i < weights.Length; i++) Assert.Equal(weights[i], table.GetWeight(i));
	}

	[Fact]
	public void Constructor_DoesNotRetainMutableCallerStorage()
	{
		ulong[] weights = [1, 3];
		var table = new WeightedIndexTable(weights);
		Array.Fill(weights, ulong.MaxValue);
		Assert.Equal(4UL, table.TotalWeight);
		Assert.Equal(1UL, table.GetWeight(0));
		Assert.Equal(3UL, table.GetWeight(1));
		Assert.Equal(0, table.GetIndexForTicket(0));
		Assert.Equal(1, table.GetIndexForTicket(3));
	}

	[Fact]
	public void MaximumTotalWeight_DoesNotOverflowCumulativeSelection()
	{
		var table = new WeightedIndexTable(new ulong[] { 0, ulong.MaxValue - 1, 0, 1, 0 });
		Assert.Equal(ulong.MaxValue, table.TotalWeight);
		Assert.Equal(1, table.GetIndexForTicket(0));
		Assert.Equal(1, table.GetIndexForTicket(ulong.MaxValue - 2));
		Assert.Equal(3, table.GetIndexForTicket(ulong.MaxValue - 1));
		var state = Pcg32State.Create(1);
		for (int i = 0; i < 1_000; i++) Assert.Contains(table.Pick(ref state), new[] { 1, 3 });
	}

	[Fact]
	public void InvalidDistributions_AreRejectedBeforeSampling()
	{
		Assert.Throws<ArgumentException>(() => new WeightedIndexTable(ReadOnlySpan<ulong>.Empty));
		Assert.Throws<ArgumentException>(() => new WeightedIndexTable(new ulong[] { 0, 0 }));
		Assert.Throws<ArgumentException>(() => new WeightedIndexTable(new ulong[] { ulong.MaxValue, 1 }));
	}

	[Fact]
	public void TicketsAndIndices_RejectInvalidEndpoints()
	{
		var table = new WeightedIndexTable(new ulong[] { 2, 1 });
		Assert.Throws<ArgumentOutOfRangeException>(() => table.GetIndexForTicket(3));
		Assert.Throws<ArgumentOutOfRangeException>(() => table.GetIndexForTicket(ulong.MaxValue));
		Assert.Throws<ArgumentOutOfRangeException>(() => table.GetWeight(-1));
		Assert.Throws<ArgumentOutOfRangeException>(() => table.GetWeight(2));
	}

	[Fact]
	public void SinglePositiveEntry_IsAlwaysSelected()
	{
		var table = new WeightedIndexTable(new ulong[] { 0, 0, 1, 0 });
		var state = Pcg32State.Create(17);
		for (int i = 0; i < 1_000; i++) Assert.Equal(2, table.Pick(ref state));
	}

	[Fact]
	public void Pick_UsesTheSameStateAndTicketContractAsTheExplicitSampler()
	{
		var table = new WeightedIndexTable(new ulong[] { 1, 10, 100, 1_000 });
		var actual = Pcg32State.Create(42, 54);
		var expected = actual;
		for (int i = 0; i < 1_000; i++)
		{
			int index = table.GetIndexForTicket(Pcg32.NextUInt64(ref expected, table.TotalWeight));
			Assert.Equal(index, table.Pick(ref actual));
		}
		Assert.Equal(expected, actual);
	}

	[Fact]
	public void SharedTable_WithWorkerOwnedStates_IsReproducibleAcrossConcurrentReaders()
	{
		var table = new WeightedIndexTable(new ulong[] { 1, 0, 7, 3, 0 });
		var outputs = new int[8][];
		Parallel.For(0, outputs.Length, worker =>
		{
			var state = Pcg32State.Create(78, 29);
			var values = new int[1_000];
			for (int i = 0; i < values.Length; i++) values[i] = table.Pick(ref state);
			outputs[worker] = values;
		});
		foreach (int[] values in outputs.Skip(1)) Assert.Equal(outputs[0], values);
	}
}
