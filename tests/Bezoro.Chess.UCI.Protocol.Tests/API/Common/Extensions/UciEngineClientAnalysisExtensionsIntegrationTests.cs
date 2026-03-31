using Bezoro.Chess.UCI.Protocol.API.Common.Extensions;
using Bezoro.Chess.UCI.Protocol.Tests.Attributes;
using Bezoro.Chess.UCI.Protocol.Tests.TestHelpers;
using FluentAssertions;
using JetBrains.Annotations;

namespace Bezoro.Chess.UCI.Protocol.Tests.API.Common.Extensions;

[Collection("Stockfish")]
[TestSubject(typeof(UciEngineClientAnalysisExtensions))]
public class UciEngineClientAnalysisExtensionsIntegrationTests(StockfishFixture fixture)
{
	[IntegrationTest]
	public async Task AnalyzeLegalMovesAsync_WhenEngineSupportsMultiPv_ShouldReturnSortedMoveEvaluations()
	{
		await using var client = new UciEngineClient(fixture.StockfishPath);
		await client.StartAsync(CancellationToken.None);
		await client.SetPositionAsync(Fen.Default, [], CancellationToken.None);

		var legalMoves = await client.GetLegalMovesViaPerftAsync(CancellationToken.None);
		var evaluations = await client.AnalyzeLegalMovesAsync(
			sideToMove: 'w',
			playerColor: 'w',
			legalMoves: legalMoves,
			currentScore: PositionScore.FromEngineScore(0, null, 'w', 'w'),
			baselineCp: 0,
			ct: CancellationToken.None
		);

		evaluations.Should().NotBeEmpty();
		evaluations.Should().BeInDescendingOrder(static evaluation => evaluation.SortValue);
		evaluations.Select(static evaluation => evaluation.Move).Should().OnlyContain(move => legalMoves.Contains(move));
	}
}
