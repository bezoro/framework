using Bezoro.Chess.UCI.Protocol.API;
using Bezoro.Chess.UCI.Protocol.API.Types;
using Bezoro.Chess.UCI.Protocol.ConsoleDemo;
using FluentAssertions;
using JetBrains.Annotations;

namespace Bezoro.Chess.UCI.Protocol.Tests.ConsoleDemo;

[TestSubject(typeof(PlayedPositionEvaluationResolver))]
public class PlayedPositionEvaluationResolverTests
{
	[Fact]
	public void TryResolveScore_WhenParentAnalysisContainsMove_ShouldReturnPlayedMoveScore()
	{
		var entry = new MoveHistoryEntry(1, 'w', "e2e4", Fen.Default.Raw, "fen-after-e2e4");
		var analyses = new Dictionary<string, PositionAnalysisResult>
		{
			[Fen.Default.Raw] = new(
				PositionAdvantage.GameStart(),
				[new("e2e4", new(22, null))]
			)
		};

		bool resolved = PlayedPositionEvaluationResolver.TryResolveScore(
			entry,
			positionKey => analyses.TryGetValue(positionKey, out var analysis) ? analysis : null,
			out var score
		);

		resolved.Should().BeTrue();
		score.Should().Be(new PositionScore(22, null));
	}

	[Fact]
	public void ResolveCurrentAdvantage_WhenPlayedMoveScoreExists_ShouldUseSameScoreAsMoveHistory()
	{
		MoveHistoryEntry[] history =
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

		var advantage = PlayedPositionEvaluationResolver.ResolveCurrentAdvantage(
			"fen-after-e2e4",
			history,
			legalMoveCount: 20,
			positionKey => analyses.TryGetValue(positionKey, out var analysis) ? analysis : null
		);

		advantage.Score.Cp.Should().Be(22);
		advantage.Summary.Should().Contain("22 cp");
	}

	[Fact]
	public void ResolveCurrentAdvantage_WhenNoMovesHaveBeenPlayed_ShouldUseCurrentPositionAnalysis()
	{
		var analyses = new Dictionary<string, PositionAnalysisResult>
		{
			[Fen.Default.Raw] = new(PositionAdvantage.FromScore(new(17, null)), [])
		};

		var advantage = PlayedPositionEvaluationResolver.ResolveCurrentAdvantage(
			Fen.Default.Raw,
			[],
			legalMoveCount: 20,
			positionKey => analyses.TryGetValue(positionKey, out var analysis) ? analysis : null
		);

		advantage.Score.Cp.Should().Be(17);
		advantage.Summary.Should().Contain("17 cp");
	}
}
