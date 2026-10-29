using FluentAssertions;
using JetBrains.Annotations;

namespace Bezoro.Chess.UCI.Protocol.Tests.API.Types;

[TestSubject(typeof(PositionScore))]
public class PositionScoreTests
{
	[Fact]
	public void FromEngineScore_WhenCentipawnScoreIsFromOpponentPerspective_ShouldFlipPerspective()
	{
		var score = PositionScore.FromEngineScore(
			rawCpScore: 120,
			rawMateScore: null,
			sideToMove: 'b',
			playerColor: 'w'
		);

		score.Cp.Should().Be(-120);
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
	[Fact]
	public void MissingEvaluationStaysUnknown()
	{
		var score = PositionScore.FromEngineScore(null, null, 'w', 'b');
		score.Cp.Should().BeNull();
		score.Mate.Should().BeNull();
		score.ToDisplayString().Should().Be("unknown");
	}

	[Fact]
	public void ExtremeMatesOutrankEveryCentipawnScoreAndDoNotOverflow()
	{
		new PositionScore(null, int.MaxValue).ToSortValue().Should()
			.BeGreaterThan(new PositionScore(int.MaxValue, null).ToSortValue());
		new PositionScore(null, int.MinValue).ToSortValue().Should()
			.BeLessThan(new PositionScore(int.MinValue, null).ToSortValue());
		new PositionScore(null, int.MinValue).ToDisplayString().Should().Be("-M2147483648");
		new PositionScore(null, 0).ToDisplayString().Should().Be("M0");
		new PositionScore(null, 1).ToSortValue().Should().BeGreaterThan(new PositionScore(null, 2).ToSortValue());
		new PositionScore(null, -2).ToSortValue().Should().BeGreaterThan(new PositionScore(null, -1).ToSortValue());
	}

	[Fact]
	public void UnrepresentablePerspectiveThrowsRatherThanWrapping()
	{
		Action convert = () => PositionScore.FromEngineScore(int.MinValue, null, 'w', 'b');
		convert.Should().Throw<OverflowException>();
	}

	[Theory]
	[InlineData('x', 'w')]
	[InlineData('w', 'x')]
	public void InvalidSidesAreRejected(char side, char player)
	{
		Action convert = () => PositionScore.FromEngineScore(0, null, side, player);
		convert.Should().Throw<ArgumentOutOfRangeException>();
	}

}
