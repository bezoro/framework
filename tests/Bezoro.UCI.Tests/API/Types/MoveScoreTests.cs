using Bezoro.UCI.API.Types;
using FluentAssertions;
using JetBrains.Annotations;

namespace Bezoro.UCI.Tests.API.Types;

[TestSubject(typeof(MoveScore))]
public class MoveScoreTests
{
	[Fact]
	public void TryParse_WhenInvalidInput_ReturnsFalseAndNull()
	{
		// Act
		var  line    = "invalid";
		bool success = MoveScore.TryParse(line, out var result);

		// Assert
		success.Should().BeFalse();
		result.Should().BeNull();
	}

	[Fact]
	public void TryParse_WhenValidInput_ReturnsTrueAndValidObject()
	{
		// Arrange
		var line =
			"info depth 12 seldepth 20 multipv 1 score cp 34 nodes 1000 nps 5000 tbhits 2 time 50 pv e2e4 e7e5 g1f3";

		// Act
		bool success = MoveScore.TryParse(line, out var moveScore);

		success.Should().BeTrue();
		moveScore.Should().NotBeNull();
		moveScore!.Value.ScoreCp.Should().Be(34);
	}

	[Fact]
	public void FromSearchResult_WhenMateScoreAvailable_UsesMate()
	{
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
			new[] { "f7g7" },
			"f7g7");

		var result = new SearchResult(
			10,
			10,
			1,
			1000,
			0,
			50,
			new[] { pv },
			"f7g7",
			string.Empty);

		var score = MoveScore.FromSearchResult(result);

		score.ScoreMate.Should().Be(-2);
		score.ScoreCp.Should().BeNull();
	}

	[Fact]
	public void FromSearchResult_WhenPrimaryPvMissingCp_FallsBackToFirstAvailable()
	{
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
			new[] { "e2e4" },
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
			new[] { "d2d4" },
			"d2d4");

		var result = new SearchResult(
			8,
			9,
			2,
			1100,
			0,
			45,
			new[] { pv1, pv2 },
			"e2e4",
			string.Empty);

		var score = MoveScore.FromSearchResult(result);

		score.ScoreCp.Should().Be(25);
		score.ScoreMate.Should().BeNull();
	}
}
