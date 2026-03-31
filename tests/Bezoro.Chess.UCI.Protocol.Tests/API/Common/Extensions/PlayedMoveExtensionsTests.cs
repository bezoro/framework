using Bezoro.Chess.UCI.Protocol.API.Common.Extensions;
using FluentAssertions;
using JetBrains.Annotations;

namespace Bezoro.Chess.UCI.Protocol.Tests.API.Common.Extensions;

[TestSubject(typeof(PlayedMoveExtensions))]
public class PlayedMoveExtensionsTests
{
	[Fact]
	public void TryResolveScore_WhenParentAnalysisContainsMove_ShouldReturnPlayedMoveScore()
	{
		var move = new PlayedMove(1, 'w', "e2e4", Fen.Default.Raw, "fen-after-e2e4");
		var analyses = new Dictionary<string, PositionAnalysisResult>
		{
			[Fen.Default.Raw] = new(
				PositionAdvantage.GameStart(),
				[new("e2e4", new(22, null))]
			)
		};

		bool resolved = move.TryResolveScore(
			positionKey => analyses.TryGetValue(positionKey, out var analysis) ? analysis : null,
			out var score
		);

		resolved.Should().BeTrue();
		score.Should().Be(new PositionScore(22, null));
	}
}
