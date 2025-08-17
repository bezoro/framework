using Bezoro.UCI.API.Types;
using FluentAssertions;
using JetBrains.Annotations;

namespace Bezoro.UCI.Tests.Unit.API.Types;

[TestSubject(typeof(SearchResult))]
public class SearchResultUnitTests
{
	[Fact]
	public void TryParse_WhenLineWithScoreMate_ReturnsTrueAndValidObject()
	{
		const string line =
			"info depth 8 seldepth 12 multipv 1 score mate 5 nodes 50000 tbhits 0 time 1500 pv e2e4 e7e5 g1f3 b8c6 f1b5 a7a6";

		string[] lines = [line, "bestmove e2e4 ponder e7e5"];

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
	public void TryParse_WhenValidLine_ReturnsTrueAndValidObject()
	{
		const string line =
			"info depth 8 seldepth 12 multipv 1 score cp 120 nodes 50000 tbhits 0 time 1500 pv e2e4 e7e5 g1f3 b8c6 f1b5 a7a6";

		string[] lines = [line, "bestmove e2e4 ponder e7e5"];

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
