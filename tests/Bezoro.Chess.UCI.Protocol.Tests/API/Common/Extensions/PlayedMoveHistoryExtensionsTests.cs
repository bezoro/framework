using Bezoro.Chess.UCI.Protocol.API.Common.Extensions;
using FluentAssertions;
using JetBrains.Annotations;

namespace Bezoro.Chess.UCI.Protocol.Tests.API.Common.Extensions;

[TestSubject(typeof(PlayedMoveHistoryExtensions))]
public class PlayedMoveHistoryExtensionsTests
{
	[Fact]
	public void ResolveCurrentAdvantage_WhenHistoryIsEmpty_ShouldUseCurrentPositionAnalysis()
	{
		var analyses = new Dictionary<string, PositionAnalysisResult>
		{
			[Fen.Default.Raw] = new(PositionAdvantage.FromScore(new(17, null)), [])
		};

		var advantage = Array.Empty<PlayedMove>().ResolveCurrentAdvantage(
			Fen.Default.Raw,
			20,
			positionKey => analyses.TryGetValue(positionKey, out var analysis) ? analysis : null
		);

		advantage.Score.Cp.Should().Be(17);
	}

	[Fact]
	public void ResolveCurrentAdvantage_WhenLastMoveScoreExists_ShouldUsePlayedMoveScore()
	{
		PlayedMove[] history =
		[
			new(1, 'w', "e2e4", Fen.Default.Raw, "fen-after-e2e4")
		];

		var analyses = new Dictionary<string, PositionAnalysisResult>
		{
			[Fen.Default.Raw] = new(
				PositionAdvantage.GameStart(),
				[new("e2e4", new(22, null))]
			)
		};

		var advantage = history.ResolveCurrentAdvantage(
			"fen-after-e2e4",
			20,
			positionKey => analyses.TryGetValue(positionKey, out var analysis) ? analysis : null
		);

		advantage.Score.Cp.Should().Be(22);
		advantage.Summary.Should().Contain("22 cp");
	}

	[Fact]
	public void ToDisplayLines_WhenScoresExist_ShouldFormatHistory()
	{
		PlayedMove[] history =
		[
			new(
				1,
				'w',
				"e2e4",
				"fen-0",
				"fen-1",
				MoveClassification.CreateStructural(
					MoveClassificationFlags.Normal | MoveClassificationFlags.DoublePawnPush,
					'P'
				).WithTacticalOutcome(false, false, false)
			),
			new(
				1,
				'b',
				"e7e5",
				"fen-1",
				"fen-2",
				MoveClassification.CreateStructural(
					MoveClassificationFlags.Normal | MoveClassificationFlags.DoublePawnPush,
					'p'
				).WithTacticalOutcome(false, false, false)
			)
		];

		var scores = new Dictionary<string, PositionScore>
		{
			["e2e4"] = new(17, null),
			["e7e5"] = new(5, null)
		};

		string[] lines = history.ToDisplayLines(move => scores.TryGetValue(move.Move, out var score) ? score : null
		);

		lines.Should().Equal(
			"Move history:",
			"  1. e2e4    +17 cp [normal,double-pawn-push]",
			"  1... e7e5  +5 cp [normal,double-pawn-push]"
		);
	}
}
