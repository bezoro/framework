using Bezoro.UCI.API.Types;
using FluentAssertions;
using JetBrains.Annotations;

namespace Bezoro.UCI.Tests.Unit.API.Types;

[TestSubject(typeof(SearchResult))]
[Trait("Category", "Unit")]
public class SearchResultExtraUnitTests
{
	[Fact]
	public void MateScore_PicksShortestMateByAbsoluteValue_AndKeepsSign()
	{
		var pv1 = new PrincipalVariation(
			10,
			10,
			1,
			null,
			-5,
			1,
			1,
			0,
			1,
			new[] { "e2e4" },
			"e2e4");

		var pv2 = new PrincipalVariation(
			10,
			11,
			2,
			null,
			3,
			2,
			1,
			0,
			2,
			new[] { "d2d4" },
			"d2d4");

		var pv3 = new PrincipalVariation(
			10,
			12,
			3,
			null,
			-2,
			3,
			1,
			0,
			3,
			new[] { "c2c4" },
			"c2c4");

		var result = new SearchResult(
			10,
			12,
			3,
			6,
			0,
			6,
			new[] { pv1, pv2, pv3 },
			"e2e4",
			"e7e5");

		result.HasMate.Should().BeTrue();
		result.MateScore.Should().Be(-2);
	}

	[Fact]
	public void ContainsMove_And_GetVariationMethods_Work_ForLowercasedUci()
	{
		var pv1 = new PrincipalVariation(
			5,
			6,
			1,
			10,
			null,
			1000,
			100_000,
			0,
			50,
			new[] { "e2e4", "e7e5", "g1f3" },
			"e2e4 e7e5 g1f3");

		var pv2 = new PrincipalVariation(
			5,
			7,
			2,
			20,
			null,
			1500,
			120_000,
			0,
			70,
			new[] { "d2d4", "d7d5" },
			"d2d4 d7d5");

		var result = new SearchResult(
			5,
			7,
			2,
			2500,
			0,
			120,
			new[] { pv1, pv2 },
			"e2e4",
			"e7e5");

		result.ContainsMove("G1F3").Should().BeTrue();
		result.GetVariationContaining("g1f3")!.Value.Moves.Should().Contain("g1f3");
		result.GetVariationStartingWith("d2d4")!.Value.Moves[0].Should().Be("d2d4");
	}

	[Fact]
	public void TryParse_WithMultiplePvs_AggregatesAndComputesExpectedTotals()
	{
		string[] lines =
		[
			"info depth 6 seldepth 8 multipv 1 score cp 50 nodes 1000 tbhits 1 time 20 pv e2e4 e7e5",
			"info depth 6 seldepth 9 multipv 2 score cp 30 nodes 2000 tbhits 2 time 30 pv d2d4 d7d5",
			"bestmove e2e4 ponder e7e5"
		];

		bool ok = SearchResult.TryParse(lines, out var result);

		ok.Should().BeTrue();
		result.ReachedDepth.Should().Be(6u);
		result.ReachedSelDepth.Should().Be(9u);       // max seldepth across lines
		result.MultiPvValue.Should().Be(2u);          // last multipv observed
		result.TotalNodesSearched.Should().Be(3000u); // sum
		result.TotalTbHits.Should().Be(3u);           // sum
		result.TotalSearchTimeMs.Should().Be(50u);    // sum
		result.PrincipalVariations.Count.Should().Be(2);
		result.BestMove.Should().Be("e2e4");
		result.PonderMove.Should().Be("e7e5");
	}
}
