using FluentAssertions;
using JetBrains.Annotations;

namespace Bezoro.Chess.UCI.Protocol.Tests.API.Types;

[TestSubject(typeof(PositionScore))]
public class PositionScoreTests
{
	[Fact]
	public void FromEngineScore_WhenCentipawnScoreIsFromOpponentPerspective_ShouldFlipAndApplyBaseline()
	{
		var score = PositionScore.FromEngineScore(
			rawCpScore: 120,
			rawMateScore: null,
			sideToMove: 'b',
			playerColor: 'w',
			baselineCp: 30
		);

		score.Cp.Should().Be(-150);
		score.Mate.Should().BeNull();
	}

	[Fact]
	public void ToDisplayString_WhenMateScoreIsAvailable_ShouldPreferMateNotation()
	{
		var score = PositionScore.FromEngineScore(
			rawCpScore: null,
			rawMateScore: 3,
			sideToMove: 'w',
			playerColor: 'w'
		);

		score.ToDisplayString().Should().Be("+M3");
		score.ToSortValue().Should().BeGreaterThan(99_000);
	}
}
