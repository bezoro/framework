using Bezoro.Chess.UCI.Protocol.API.Common.Extensions;
using Bezoro.Chess.UCI.Protocol.Tests.Attributes;
using Bezoro.Chess.UCI.Protocol.Tests.TestHelpers;
using FluentAssertions;
using JetBrains.Annotations;

namespace Bezoro.Chess.UCI.Protocol.Tests.API;

[Collection("Stockfish")]
[TestSubject(typeof(UciPositionAnalysisCoordinator))]
public class UciPositionAnalysisCoordinatorIntegrationTests(StockfishFixture fixture)
{
	[IntegrationTest]
	public async Task GetAnalysisAsync_WhenPositionIsQueued_ShouldReturnAdvantageAndMoveEvaluations()
	{
		await using var client = new UciEngineClient(fixture.StockfishPath);
		await client.StartAsync(CancellationToken.None);
		await client.SetPositionAsync(Fen.Default, [], CancellationToken.None);
		var legalMoves = (await client.GetLegalMovesViaPerftAsync(CancellationToken.None)).NormalizeUciMoves();

		var coordinator = new UciPositionAnalysisCoordinator(client, multiPvMoveTimeMs: 500, fallbackMoveTimeMs: 100);
		coordinator.Enqueue(Fen.Default.Raw, [], 'w', 'w', legalMoves);

		var analysis = await coordinator.GetAnalysisAsync(Fen.Default.Raw);

		analysis.Advantage.Score.Cp.Should().HaveValue();
		analysis.Evaluations.Should().NotBeEmpty();
		analysis.Evaluations.Select(static evaluation => evaluation.Move).Should().OnlyContain(move => legalMoves.Contains(move));
	}

	[IntegrationTest]
	public async Task GetAnalysisAsync_WhenMultiplePositionsAreQueued_ShouldRetainAndCompleteEachPositionInOrder()
	{
		await using var client = new UciEngineClient(fixture.StockfishPath);
		await using var helperClient = new UciEngineClient(fixture.StockfishPath);
		await Task.WhenAll(client.StartAsync(CancellationToken.None), helperClient.StartAsync(CancellationToken.None));

		await helperClient.SetPositionAsync(Fen.Default, [], CancellationToken.None);
		var startMoves = (await helperClient.GetLegalMovesViaPerftAsync(CancellationToken.None)).NormalizeUciMoves();

		await helperClient.SetPositionAsync(Fen.Default, ["e2e4"], CancellationToken.None);
		var replyMoves = (await helperClient.GetLegalMovesViaPerftAsync(CancellationToken.None)).NormalizeUciMoves();
		var replyFen = (await helperClient.TryGetFenViaDisplayBoardAsync(CancellationToken.None))!.Value;

		var coordinator = new UciPositionAnalysisCoordinator(client, multiPvMoveTimeMs: 750, fallbackMoveTimeMs: 100);
		coordinator.Enqueue(Fen.Default.Raw, [], 'w', 'w', startMoves);
		coordinator.Enqueue(replyFen.Raw, ["e2e4"], 'b', 'w', replyMoves);

		var startAnalysis = await coordinator.GetAnalysisAsync(Fen.Default.Raw);
		var replyAnalysis = await coordinator.GetAnalysisAsync(replyFen.Raw);

		startAnalysis.Evaluations.Should().NotBeEmpty();
		replyAnalysis.Evaluations.Should().NotBeEmpty();
		startAnalysis.Advantage.Score.Cp.Should().HaveValue();
		replyAnalysis.Advantage.Score.Cp.Should().HaveValue();
		coordinator.TryGetAnalysis(Fen.Default.Raw, out var cachedStartAnalysis).Should().BeTrue();
		coordinator.TryGetAnalysis(replyFen.Raw, out var cachedReplyAnalysis).Should().BeTrue();
		cachedStartAnalysis.Advantage.Should().Be(startAnalysis.Advantage);
		cachedReplyAnalysis.Advantage.Should().Be(replyAnalysis.Advantage);
	}

	[IntegrationTest]
	public async Task TryGetAnalysis_WhenPositionIsQueuedButNotCompleted_ShouldReturnFalse()
	{
		await using var client = new UciEngineClient(fixture.StockfishPath);
		await client.StartAsync(CancellationToken.None);
		await client.SetPositionAsync(Fen.Default, [], CancellationToken.None);
		var legalMoves = (await client.GetLegalMovesViaPerftAsync(CancellationToken.None)).NormalizeUciMoves();

		var coordinator = new UciPositionAnalysisCoordinator(client, multiPvMoveTimeMs: 2_000, fallbackMoveTimeMs: 100);
		coordinator.Enqueue(Fen.Default.Raw, [], 'w', 'w', legalMoves);

		coordinator.TryGetAnalysis(Fen.Default.Raw, out _).Should().BeFalse();
	}
}
