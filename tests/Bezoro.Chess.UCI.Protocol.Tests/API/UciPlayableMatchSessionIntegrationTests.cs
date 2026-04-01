using Bezoro.Chess.UCI.Protocol.Tests.Attributes;
using Bezoro.Chess.UCI.Protocol.Tests.TestHelpers;
using FluentAssertions;
using JetBrains.Annotations;

namespace Bezoro.Chess.UCI.Protocol.Tests.API;

[Collection("Stockfish")]
[TestSubject(typeof(UciPlayableMatchSession))]
public class UciPlayableMatchSessionIntegrationTests(StockfishFixture fixture)
{
	[IntegrationTest]
	public async Task RefreshAsync_WhenHumanMoveIsPlayedAfterOpeningAnalysis_ShouldReuseSameScoreForCurrentAdvantage()
	{
		await using var playingClient = new UciEngineClient(fixture.StockfishPath);
		await using var analysisClient = new UciEngineClient(fixture.StockfishPath);
		await using var moveListClient = new UciEngineClient(fixture.StockfishPath);
		await Task.WhenAll(
			playingClient.StartAsync(CancellationToken.None),
			analysisClient.StartAsync(CancellationToken.None),
			moveListClient.StartAsync(CancellationToken.None)
		);

		var session = new UciPlayableMatchSession(
			playingClient,
			analysisClient,
			moveListClient,
			playerColor: 'w',
			engineMoveTimeMs: 100,
			moveListAnalysisTimeMs: 500,
			moveListFallbackTimeMs: 100
		);

		await session.StartNewGameAsync(CancellationToken.None);
		await session.RefreshAsync(CancellationToken.None);
		var openingAnalysis = await session.GetLegalMoveAnalysisAsync(CancellationToken.None);
		var e2e4Score = openingAnalysis.Evaluations.Single(static evaluation => evaluation.Move == "e2e4").Score;

		session.ApplyHumanMove("e2e4");
		var afterMove = await session.RefreshAsync(CancellationToken.None);

		afterMove.Advantage.Score.Should().Be(e2e4Score);
		session.MoveHistory.Should().ContainSingle();
		session.TryGetPlayedMoveScore(session.MoveHistory[^1], out var resolvedScore).Should().BeTrue();
		resolvedScore.Should().Be(e2e4Score);
		session.CancelAnalysis();
	}

	[IntegrationTest]
	public async Task RefreshAsync_WhenEngineMoveIsPlayedAfterParentAnalysis_ShouldReuseSameScoreForCurrentAdvantage()
	{
		await using var playingClient = new UciEngineClient(fixture.StockfishPath);
		await using var analysisClient = new UciEngineClient(fixture.StockfishPath);
		await using var moveListClient = new UciEngineClient(fixture.StockfishPath);
		await Task.WhenAll(
			playingClient.StartAsync(CancellationToken.None),
			analysisClient.StartAsync(CancellationToken.None),
			moveListClient.StartAsync(CancellationToken.None)
		);

		var session = new UciPlayableMatchSession(
			playingClient,
			analysisClient,
			moveListClient,
			playerColor: 'w',
			engineMoveTimeMs: 100,
			moveListAnalysisTimeMs: 500,
			moveListFallbackTimeMs: 100
		);

		await session.StartNewGameAsync(CancellationToken.None);
		await session.RefreshAsync(CancellationToken.None);
		await session.GetLegalMoveAnalysisAsync(CancellationToken.None);
		session.ApplyHumanMove("e2e4");
		await session.RefreshAsync(CancellationToken.None);
		var afterHumanAnalysis = await session.GetLegalMoveAnalysisAsync(CancellationToken.None);

		var engineMove = await session.PlayEngineMoveAsync(CancellationToken.None);
		var expectedScore = afterHumanAnalysis.Evaluations.Single(evaluation => evaluation.Move == engineMove.Move).Score;
		var afterEngine = await session.RefreshAsync(CancellationToken.None);

		afterEngine.Advantage.Score.Should().Be(expectedScore);
		session.TryGetPlayedMoveScore(session.MoveHistory[^1], out var resolvedScore).Should().BeTrue();
		resolvedScore.Should().Be(expectedScore);
		session.CancelAnalysis();
	}

	[IntegrationTest]
	public async Task WaitForCurrentMoveClassificationsAsync_WhenMateMoveExists_ShouldResolveCheckAndMate()
	{
		await using var playingClient = new UciEngineClient(fixture.StockfishPath);
		await using var analysisClient = new UciEngineClient(fixture.StockfishPath);
		await using var moveListClient = new UciEngineClient(fixture.StockfishPath);
		await Task.WhenAll(
			playingClient.StartAsync(CancellationToken.None),
			analysisClient.StartAsync(CancellationToken.None),
			moveListClient.StartAsync(CancellationToken.None)
		);

		var session = new UciPlayableMatchSession(
			playingClient,
			analysisClient,
			moveListClient,
			playerColor: 'w',
			engineMoveTimeMs: 100,
			moveListAnalysisTimeMs: 500,
			moveListFallbackTimeMs: 100
		);

		await session.LoadPositionAsync(Fen.Parse("7k/5Q2/7K/8/8/8/8/8 w - - 0 1")!.Value, [], CancellationToken.None);

		await session.RefreshAsync(CancellationToken.None);
		var classifications = await session.WaitForCurrentMoveClassificationsAsync(CancellationToken.None);

		classifications["f7g7"].IsCheck.Should().BeTrue();
		classifications["f7g7"].IsMate.Should().BeTrue();
		classifications["f7g7"].IsResolved.Should().BeTrue();
		session.CancelAnalysis();
	}

	[IntegrationTest]
	public async Task WaitForCurrentMoveClassificationsAsync_WhenStalemateMoveExists_ShouldResolveStalemate()
	{
		await using var playingClient = new UciEngineClient(fixture.StockfishPath);
		await using var analysisClient = new UciEngineClient(fixture.StockfishPath);
		await using var moveListClient = new UciEngineClient(fixture.StockfishPath);
		await Task.WhenAll(
			playingClient.StartAsync(CancellationToken.None),
			analysisClient.StartAsync(CancellationToken.None),
			moveListClient.StartAsync(CancellationToken.None)
		);

		var session = new UciPlayableMatchSession(
			playingClient,
			analysisClient,
			moveListClient,
			playerColor: 'w',
			engineMoveTimeMs: 100,
			moveListAnalysisTimeMs: 500,
			moveListFallbackTimeMs: 100
		);

		await session.LoadPositionAsync(Fen.Parse("k7/1QK5/8/8/8/8/8/8 w - - 0 1")!.Value, [], CancellationToken.None);

		await session.RefreshAsync(CancellationToken.None);
		var classifications = await session.WaitForCurrentMoveClassificationsAsync(CancellationToken.None);

		classifications["b7b6"].IsStalemate.Should().BeTrue();
		classifications["b7b6"].IsResolved.Should().BeTrue();
		session.CancelAnalysis();
	}

	[IntegrationTest]
	public async Task UndoMoves_WhenLastMoveIsUndone_ShouldRestorePreviousPositionAndTrimHistory()
	{
		await using var playingClient = new UciEngineClient(fixture.StockfishPath);
		await using var analysisClient = new UciEngineClient(fixture.StockfishPath);
		await using var moveListClient = new UciEngineClient(fixture.StockfishPath);
		await Task.WhenAll(
			playingClient.StartAsync(CancellationToken.None),
			analysisClient.StartAsync(CancellationToken.None),
			moveListClient.StartAsync(CancellationToken.None)
		);

		var session = new UciPlayableMatchSession(
			playingClient,
			analysisClient,
			moveListClient,
			playerColor: 'w',
			engineMoveTimeMs: 100,
			moveListAnalysisTimeMs: 500,
			moveListFallbackTimeMs: 100
		);

		await session.StartNewGameAsync(CancellationToken.None);
		await session.RefreshAsync(CancellationToken.None);
		session.ApplyHumanMove("e2e4");
		var afterHuman = await session.RefreshAsync(CancellationToken.None);

		await session.PlayEngineMoveAsync(CancellationToken.None);
		await session.RefreshAsync(CancellationToken.None);

		session.CanUndoMoves().Should().BeTrue();
		session.UndoMoves();

		session.PlayedMoves.Should().Equal(["e2e4"]);
		session.MoveHistory.Select(static move => move.Move).Should().Equal(["e2e4"]);

		var afterUndo = await session.RefreshAsync(CancellationToken.None);

		afterUndo.PositionKey.Should().Be(afterHuman.PositionKey);
		afterUndo.Advantage.Score.Should().Be(afterHuman.Advantage.Score);
		afterUndo.MoveHistory.Select(static move => move.Move).Should().Equal(["e2e4"]);
		session.CancelAnalysis();
	}

	[IntegrationTest]
	public async Task UndoMoves_WhenPendingUnrefreshedMoveIsUndone_ShouldRestoreCurrentPrefix()
	{
		await using var playingClient = new UciEngineClient(fixture.StockfishPath);
		await using var analysisClient = new UciEngineClient(fixture.StockfishPath);
		await using var moveListClient = new UciEngineClient(fixture.StockfishPath);
		await Task.WhenAll(
			playingClient.StartAsync(CancellationToken.None),
			analysisClient.StartAsync(CancellationToken.None),
			moveListClient.StartAsync(CancellationToken.None)
		);

		var session = new UciPlayableMatchSession(
			playingClient,
			analysisClient,
			moveListClient,
			playerColor: 'w',
			engineMoveTimeMs: 100,
			moveListAnalysisTimeMs: 500,
			moveListFallbackTimeMs: 100
		);

		await session.StartNewGameAsync(CancellationToken.None);
		var opening = await session.RefreshAsync(CancellationToken.None);

		session.ApplyHumanMove("e2e4");
		session.CanUndoMoves().Should().BeTrue();

		session.UndoMoves();

		session.PlayedMoves.Should().BeEmpty();
		session.MoveHistory.Should().BeEmpty();

		var afterUndo = await session.RefreshAsync(CancellationToken.None);

		afterUndo.PositionKey.Should().Be(opening.PositionKey);
		afterUndo.LegalMoves.Should().BeEquivalentTo(opening.LegalMoves);
		session.CancelAnalysis();
	}

	[IntegrationTest]
	public async Task UndoMoves_WhenMultipleMovesAreUndone_ShouldRestoreEarlierPrefix()
	{
		await using var playingClient = new UciEngineClient(fixture.StockfishPath);
		await using var analysisClient = new UciEngineClient(fixture.StockfishPath);
		await using var moveListClient = new UciEngineClient(fixture.StockfishPath);
		await Task.WhenAll(
			playingClient.StartAsync(CancellationToken.None),
			analysisClient.StartAsync(CancellationToken.None),
			moveListClient.StartAsync(CancellationToken.None)
		);

		var session = new UciPlayableMatchSession(
			playingClient,
			analysisClient,
			moveListClient,
			playerColor: 'w',
			engineMoveTimeMs: 100,
			moveListAnalysisTimeMs: 500,
			moveListFallbackTimeMs: 100
		);

		await session.StartNewGameAsync(CancellationToken.None);
		var opening = await session.RefreshAsync(CancellationToken.None);

		session.ApplyHumanMove("e2e4");
		await session.RefreshAsync(CancellationToken.None);
		await session.PlayEngineMoveAsync(CancellationToken.None);
		await session.RefreshAsync(CancellationToken.None);
		session.ApplyHumanMove("g1f3");
		await session.RefreshAsync(CancellationToken.None);

		session.CanUndoMoves(3).Should().BeTrue();
		session.UndoMoves(3);

		session.PlayedMoves.Should().BeEmpty();
		session.MoveHistory.Should().BeEmpty();
		session.CanUndoMoves().Should().BeFalse();

		var afterUndo = await session.RefreshAsync(CancellationToken.None);

		afterUndo.PositionKey.Should().Be(opening.PositionKey);
		afterUndo.LegalMoves.Should().BeEquivalentTo(opening.LegalMoves);
		afterUndo.MoveHistory.Should().BeEmpty();
		session.CancelAnalysis();
	}

	[Fact]
	public void UndoMoves_WhenCountIsNotPositive_ShouldThrowArgumentOutOfRangeException()
	{
		var session = CreateUnstartedSession();

		session.Invoking(static value => value.UndoMoves(0))
			   .Should()
			   .Throw<ArgumentOutOfRangeException>();
	}

	[Fact]
	public void UndoMoves_WhenMoreMovesThanPlayedAreRequested_ShouldThrowInvalidOperationException()
	{
		var session = CreateUnstartedSession();

		session.Invoking(static value => value.UndoMoves())
			   .Should()
			   .Throw<InvalidOperationException>();
	}

	[Fact]
	public void CanUndoMoves_WhenRequestedCountExceedsPlayedMoves_ShouldReturnFalse()
	{
		var session = CreateUnstartedSession();

		session.CanUndoMoves().Should().BeFalse();
		session.CanUndoMoves(2).Should().BeFalse();
	}

	private UciPlayableMatchSession CreateUnstartedSession()
	{
		var playingClient = new UciEngineClient(fixture.StockfishPath);
		var analysisClient = new UciEngineClient(fixture.StockfishPath);
		var moveListClient = new UciEngineClient(fixture.StockfishPath);

		return new UciPlayableMatchSession(
			playingClient,
			analysisClient,
			moveListClient,
			playerColor: 'w',
			engineMoveTimeMs: 100,
			moveListAnalysisTimeMs: 500,
			moveListFallbackTimeMs: 100
		);
	}
}
