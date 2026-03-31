using FluentAssertions;
using JetBrains.Annotations;

namespace Bezoro.Chess.UCI.Protocol.Tests.API.Types;

[TestSubject(typeof(PositionAdvantage))]
public class PositionAdvantageTests
{
	[Fact]
	public void FromEngineScore_WhenScoreIsEven_ShouldProduceNeutralSummary()
	{
		var advantage = PositionAdvantage.FromEngineScore(
			rawCpScore: 0,
			rawMateScore: null,
			sideToMove: 'w',
			playerColor: 'w',
			baselineCp: 0
		);

		advantage.Normalized.Should().Be(0);
		advantage.Summary.Should().Be("Advantage 0.00 | Even (0 cp)");
		advantage.Score.Cp.Should().Be(0);
	}

	[Fact]
	public void FromEngineScore_WhenMateScoreFavoursPlayer_ShouldDescribeWinningMate()
	{
		var advantage = PositionAdvantage.FromEngineScore(
			rawCpScore: null,
			rawMateScore: 2,
			sideToMove: 'w',
			playerColor: 'w',
			baselineCp: 0
		);

		advantage.Normalized.Should().BePositive();
		advantage.Summary.Should().Contain("You mate in 2");
		advantage.Score.Mate.Should().Be(2);
	}
}
