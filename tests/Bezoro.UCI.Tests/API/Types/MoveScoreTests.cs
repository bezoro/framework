using Bezoro.UCI.API.Types;
using Bezoro.UCI.Tests.TestHelpers;
using FluentAssertions;
using JetBrains.Annotations;
using Xunit.Abstractions;

namespace Bezoro.UCI.Tests.API.Types;

[TestSubject(typeof(MoveScore))]
public class MoveScoreTests(ITestOutputHelper output) : UnitTestBase(output)
{
	[Fact]
	public void FromSearchResult_WhenMateScoreAvailable_ShouldUseMate()
	{
		Log("Testing FromSearchResult with mate score");
		var pv = new PrincipalVariation(
			10,
			10,
			1,
			null,
			-2,
			1000,
			100_000,
			0,
			50,
			["f7g7"],
			"f7g7");

		var result = new SearchResult(
			10,
			10,
			1,
			1000,
			0,
			50,
			[pv],
			"f7g7",
			string.Empty);

		var score = MoveScore.FromSearchResult(result);

		score.ScoreMate.Should().Be(-2);
		score.ScoreCp.Should().BeNull();
	}

	[Fact]
	public void FromSearchResult_WhenPrimaryPvMissingCp_ShouldFallBackToFirstAvailable()
	{
		Log("Testing FromSearchResult fallback to first available");
		var pv1 = new PrincipalVariation(
			8,
			8,
			1,
			null,
			null,
			500,
			50_000,
			0,
			20,
			["e2e4"],
			"e2e4");

		var pv2 = new PrincipalVariation(
			8,
			9,
			2,
			25,
			null,
			600,
			60_000,
			0,
			25,
			["d2d4"],
			"d2d4");

		var result = new SearchResult(
			8,
			9,
			2,
			1100,
			0,
			45,
			[pv1, pv2],
			"e2e4",
			string.Empty);

		var score = MoveScore.FromSearchResult(result);

		score.ScoreCp.Should().Be(25);
		score.ScoreMate.Should().BeNull();
	}

	[Fact]
	public void TryParse_WhenInvalidInput_ShouldReturnFalseAndNull()
	{
		Log("Testing TryParse with invalid input");
		var  line    = "invalid";
		bool success = MoveScore.TryParse(line, out var result);

		success.Should().BeFalse();
		result.Should().BeNull();
	}

	[Fact]
	public void TryParse_WhenValidInput_ShouldReturnTrueAndValidObject()
	{
		Log("Testing TryParse with valid input");
		var line =
			"info depth 12 seldepth 20 multipv 1 score cp 34 nodes 1000 nps 5000 tbhits 2 time 50 pv e2e4 e7e5 g1f3";

		bool success = MoveScore.TryParse(line, out var moveScore);

		success.Should().BeTrue();
		moveScore.Should().NotBeNull();
		moveScore!.Value.ScoreCp.Should().Be(34);
	}
}
