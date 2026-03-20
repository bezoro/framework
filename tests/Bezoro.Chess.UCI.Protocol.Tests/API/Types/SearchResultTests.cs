using FluentAssertions;
using JetBrains.Annotations;

namespace Bezoro.Chess.UCI.Protocol.Tests.API.Types;

[TestSubject(typeof(SearchResult))]
public class SearchResultTests
{
	[Fact]
	public void ContainsMove_WhenInputIsMixedCase_ShouldFindMoveAndVariations()
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
			["e2e4", "e7e5", "g1f3"],
			"e2e4 e7e5 g1f3"
		);

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
			["d2d4", "d7d5"],
			"d2d4 d7d5"
		);

		var result = new SearchResult(
			5,
			7,
			2,
			2500,
			0,
			120,
			[pv1, pv2],
			"e2e4",
			"e7e5"
		);

		result.ContainsMove("G1F3").Should().BeTrue();
		result.GetVariationContaining("g1f3")!.Value.Moves.Should().Contain("g1f3");
		result.GetVariationStartingWith("d2d4")!.Value.Moves[0].Should().Be("d2d4");
	}

	[Fact]
	public void MateScore_WhenMultipleMateScoresExist_ShouldKeepShortestMateSign()
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
			["e2e4"],
			"e2e4"
		);

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
			["d2d4"],
			"d2d4"
		);

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
			["c2c4"],
			"c2c4"
		);

		var result = new SearchResult(
			10,
			12,
			3,
			6,
			0,
			6,
			[pv1, pv2, pv3],
			"e2e4",
			"e7e5"
		);

		result.HasMate.Should().BeTrue();
		result.MateScore.Should().Be(-2);
	}

	[Fact]
	public void MateScore_WhenNoMateLineExists_ShouldReturnNull()
	{
		var pv = new PrincipalVariation(
			12,
			14,
			1,
			32,
			null,
			10,
			100,
			0,
			5,
			["e2e4", "e7e5"],
			"e2e4 e7e5"
		);

		var result = new SearchResult(
			12,
			14,
			1,
			10,
			0,
			5,
			[pv],
			"e2e4",
			"e7e5"
		);

		result.HasMate.Should().BeFalse();
		result.MateScore.Should().BeNull();
	}

	[Fact]
	public void BestPvAndBestCpScore_WhenPrimaryVariationExists_ShouldPreferMultiPvOne()
	{
		var bestLine = new PrincipalVariation(
			18,
			20,
			1,
			28,
			null,
			1000,
			100_000,
			0,
			20,
			["e2e4", "e7e5"],
			"e2e4 e7e5"
		);

		var secondaryLine = new PrincipalVariation(
			18,
			20,
			2,
			75,
			null,
			1000,
			100_000,
			0,
			20,
			["d2d4", "d7d5"],
			"d2d4 d7d5"
		);

		var result = new SearchResult(
			18,
			20,
			2,
			2000,
			0,
			40,
			[bestLine, secondaryLine],
			"e2e4",
			"e7e5"
		);

		result.BestCpScore.Should().Be(28);
		result.BestPv.Should().Be(bestLine);
	}

	[Fact]
	public void TryParse_WhenBestMoveLineIsMalformed_ShouldReturnFalse()
	{
		string[] lines =
		[
			"info depth 8 seldepth 12 multipv 1 score cp 120 nodes 50000 tbhits 0 time 1500 pv e2e4 e7e5",
			"bestmove ponder e7e5"
		];

		bool success = SearchResult.TryParse(lines, out var result);

		success.Should().BeFalse();
		result.BestMove.Should().BeNullOrEmpty();
		result.ReachedDepth.Should().Be(0);
	}

	[Fact]
	public void TryParse_WhenBestMoveLineIsMissing_ShouldReturnFalse()
	{
		string[] lines =
		[
			"info depth 8 seldepth 12 multipv 1 score cp 120 nodes 50000 tbhits 0 time 1500 pv e2e4 e7e5"
		];

		bool success = SearchResult.TryParse(lines, out var result);

		success.Should().BeFalse();
		result.BestMove.Should().BeNullOrEmpty();
		result.ReachedDepth.Should().Be(0);
	}

	[Fact]
	public void TryParse_WhenLineWithScoreMate_ShouldReturnTrueAndValidObject()
	{
		const string LINE =
			"info depth 8 seldepth 12 multipv 1 score mate 5 nodes 50000 tbhits 0 time 1500 pv e2e4 e7e5 g1f3 b8c6 f1b5 a7a6";

		string[] lines = [LINE, "bestmove e2e4 ponder e7e5"];

		bool success = SearchResult.TryParse(lines, out var resultWithMate);

		success.Should().BeTrue();
		resultWithMate.ReachedDepth.Should().Be(8u);
		resultWithMate.ReachedSelDepth.Should().Be(12u);
		resultWithMate.MultiPvValue.Should().Be(1u);
		resultWithMate.TotalNodesSearched.Should().Be(50000u);
		resultWithMate.TotalTbHits.Should().Be(0u);
		resultWithMate.TotalSearchTimeMs.Should().Be(1500u);
		resultWithMate.BestMove.Should().Be("e2e4");
		resultWithMate.PonderMove.Should().Be("e7e5");
		resultWithMate.PrincipalVariations.Should().ContainSingle();
		resultWithMate.PrincipalVariations[0].ScoreCp.Should().BeNull();
		resultWithMate.PrincipalVariations[0].ScoreMate.Should().Be(5);
	}

	[Fact]
	public void TryParse_WhenMultiplePvsAreProvided_ShouldAggregateAndComputeExpectedTotals()
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
		result.PrincipalVariations.Length.Should().Be(2);
		result.BestMove.Should().Be("e2e4");
		result.PonderMove.Should().Be("e7e5");
	}

	[Fact]
	public void TryParse_WhenDepthLinesArriveOutOfOrder_ShouldKeepDeepestDepth()
	{
		string[] lines =
		[
			"info depth 12 seldepth 18 multipv 1 score cp 35 nodes 3000 tbhits 0 time 30 pv e2e4 e7e5",
			"info depth 8 seldepth 10 multipv 2 score cp 20 nodes 1500 tbhits 0 time 15 pv d2d4 d7d5",
			"bestmove e2e4 ponder e7e5"
		];

		bool ok = SearchResult.TryParse(lines, out var result);

		ok.Should().BeTrue();
		result.ReachedDepth.Should().Be(12u);
		result.ReachedSelDepth.Should().Be(18u);
	}

	[Fact]
	public void TryParse_WhenValidLine_ShouldReturnTrueAndValidObject()
	{
		const string LINE =
			"info depth 8 seldepth 12 multipv 1 score cp 120 nodes 50000 tbhits 0 time 1500 pv e2e4 e7e5 g1f3 b8c6 f1b5 a7a6";

		string[] lines = [LINE, "bestmove e2e4 ponder e7e5"];

		bool success = SearchResult.TryParse(lines, out var resultWithScoreCp);

		success.Should().BeTrue();
		resultWithScoreCp.ReachedDepth.Should().Be(8u);
		resultWithScoreCp.ReachedSelDepth.Should().Be(12u);
		resultWithScoreCp.MultiPvValue.Should().Be(1u);
		resultWithScoreCp.TotalNodesSearched.Should().Be(50000u);
		resultWithScoreCp.TotalTbHits.Should().Be(0u);
		resultWithScoreCp.TotalSearchTimeMs.Should().Be(1500u);
		resultWithScoreCp.BestMove.Should().Be("e2e4");
		resultWithScoreCp.PonderMove.Should().Be("e7e5");
		resultWithScoreCp.PrincipalVariations.Should().ContainSingle();
		resultWithScoreCp.PrincipalVariations[0].ScoreCp.Should().Be(120);
		resultWithScoreCp.PrincipalVariations[0].ScoreMate.Should().BeNull();
	}
}
