using Bezoro.UCI.API;
using Bezoro.UCI.API.Types;
using Bezoro.UCI.Domain;
using FluentAssertions;
using JetBrains.Annotations;

namespace Bezoro.UCI.Tests;

[TestSubject(typeof(UciCoordinator))]
public class UciCoordinatorTests
{
	private const string STOCKFISH_PATH = "Engine/stockfish/stockfish-windows-x86-64-avx2.exe";

	[Fact]
	public async Task AnalysisStream_WhenStarted_YieldsPvWithScore()
	{
		await using var coordinator = new UciCoordinator(STOCKFISH_PATH);
		await coordinator.StartAsync();

		var tcs = new TaskCompletionSource<PrincipalVariation>(TaskCreationOptions.RunContinuationsAsynchronously);
		coordinator.PonderInfo += pv =>
		{
			if (pv.Moves is { Count: > 0 })
				tcs.TrySetResult(pv);
		};

		await coordinator.UpdatePositionAsync(Fen.Default, null);
		await coordinator.StartSearchAsync();

		var pvLine = await tcs.Task.WaitAsync(TimeSpan.FromSeconds(8));
		pvLine.Moves.Should().NotBeEmpty();
		(pvLine.ScoreCp.HasValue || pvLine.ScoreMate.HasValue).Should().BeTrue();

		await coordinator.StopSearchAsync();
	}

	[Fact]
	public async Task BestSearch_StartStop_RestartsCleanly()
	{
		await using var coordinator = new UciCoordinator(STOCKFISH_PATH);
		await coordinator.StartAsync();

		var bestTcs1 =
			new TaskCompletionSource<(string best, string ponder)>(TaskCreationOptions.RunContinuationsAsynchronously);

		var bestTcs2 =
			new TaskCompletionSource<(string best, string ponder)>(TaskCreationOptions.RunContinuationsAsynchronously);

		var count = 0;
		coordinator.PonderBestMove += (b, p) =>
		{
			if (string.IsNullOrWhiteSpace(b)) return;

			if (Interlocked.Increment(ref count) == 1) bestTcs1.TrySetResult((b, p));
			else if (count >= 2) bestTcs2.TrySetResult((b, p));
		};

		// Start best search for current FEN
		await coordinator.UpdatePositionAsync(Fen.Default, null);
		await coordinator.StartSearchAsync();

		var first = await bestTcs1.Task.WaitAsync(TimeSpan.FromSeconds(8));
		first.best.Should().NotBeNullOrWhiteSpace();

		// Stop and restart
		await coordinator.StopSearchAsync();
		await Task.Delay(200); // tiny pause to ensure stop settles
		await coordinator.StartSearchAsync();

		var second = await bestTcs2.Task.WaitAsync(TimeSpan.FromSeconds(8));
		second.best.Should().NotBeNullOrWhiteSpace();

		await coordinator.StopAsync();
	}

	[Fact]
	public async Task FullTurn_WhiteE2E4_ThenBlackResponse_ValidatesApi()
	{
		await using var coordinator = new UciCoordinator(STOCKFISH_PATH);
		await coordinator.StartAsync();

		// Initial position: expect legal moves including e2e4
		var legalStartTcs =
			new TaskCompletionSource<IReadOnlyCollection<string>>(TaskCreationOptions.RunContinuationsAsynchronously);

		coordinator.LegalMovesUpdated += moves =>
		{
			if (moves is { Count: > 0 })
				legalStartTcs.TrySetResult(moves);
		};

		await coordinator.UpdatePositionAsync(Fen.Default, null);
		var legalStart = await legalStartTcs.Task.WaitAsync(TimeSpan.FromSeconds(6));
		legalStart.Should().Contain(new[] { "e2e4", "d2d4" });

		// Apply white's move e2e4; validate black-side legal moves, pondering and classification events
		var legalAfterWhiteTcs =
			new TaskCompletionSource<IReadOnlyCollection<string>>(TaskCreationOptions.RunContinuationsAsynchronously);

		var bestAfterWhiteTcs =
			new TaskCompletionSource<(string best, string ponder)>(TaskCreationOptions.RunContinuationsAsynchronously);

		var classifiedTcs =
			new TaskCompletionSource<(string notation, Move move)>(TaskCreationOptions.RunContinuationsAsynchronously);

		coordinator.LegalMovesUpdated += moves =>
		{
			if (moves is { Count: > 0 })
			{
				var hasReply = false;
				foreach (string s in moves)
				{
					if (s == "e7e5" || s == "c7c5")
					{
						hasReply = true;
						break;
					}
				}

				if (hasReply)
					legalAfterWhiteTcs.TrySetResult(moves);
			}
		};

		coordinator.PonderBestMove += (best, ponder) =>
		{
			if (!string.IsNullOrWhiteSpace(best))
				bestAfterWhiteTcs.TrySetResult((best, ponder));
		};

		coordinator.NewMoveClassified += (notation, move) =>
		{
			if (!string.IsNullOrWhiteSpace(notation))
				classifiedTcs.TrySetResult((notation, move));
		};

		await coordinator.UpdatePositionAsync(Fen.Default, ["e2e4"]);

		var legalAfterWhite = await legalAfterWhiteTcs.Task.WaitAsync(TimeSpan.FromSeconds(8));
		legalAfterWhite.Should().Contain(x => x == "e7e5" || x == "c7c5");

		var bestPair = await bestAfterWhiteTcs.Task.WaitAsync(TimeSpan.FromSeconds(8));
		bestPair.best.Should().NotBeNullOrWhiteSpace();
		UciEngineClient.IsUciMoveString(bestPair.best).Should().BeTrue();

		var classified = await classifiedTcs.Task.WaitAsync(TimeSpan.FromSeconds(8));
		classified.notation.Should().NotBeNullOrWhiteSpace();

		await coordinator.StopSearchAsync();
		await coordinator.StopAsync();
	}

	[Fact]
	public async Task GetLegalMovesAsync_ReturnsParsedMoves()
	{
		await using var coordinator = new UciCoordinator(STOCKFISH_PATH);
		await coordinator.StartAsync();

		// Start background processing for the default position
		await coordinator.UpdatePositionAsync(Fen.Default, null);

		// Legal moves should contain common openers
		var legal = await coordinator.GetLegalMovesAsync();
		legal.Should().NotBeNull();
		legal.Count.Should().BeGreaterThan(0);
		legal.Should().Contain(new[] { "e2e4", "d2d4", "g1f3", "c2c4" });
	}

	[Fact]
	public async Task GetLegalMovesAsync_WhenCalled_ReturnsCommonOpeners()
	{
		await using var coordinator = new UciCoordinator(STOCKFISH_PATH);
		await coordinator.StartAsync();

		var legalMoves = await coordinator.GetLegalMovesAsync();

		legalMoves.Should().Contain(new[] { "e2e4", "d2d4", "g1f3", "c2c4" });
	}

	[Fact]
	public async Task NewGameAsync_ResetsState_And_AllowsRestart()
	{
		await using var coordinator = new UciCoordinator(STOCKFISH_PATH);
		await coordinator.StartAsync();

		var firstInfo =
			new TaskCompletionSource<PrincipalVariation?>(TaskCreationOptions.RunContinuationsAsynchronously);

		coordinator.PonderInfo += pv => firstInfo.TrySetResult(pv);

		await coordinator.StartSearchAsync(Fen.Default);
		var pv1 = await firstInfo.Task.WaitAsync(TimeSpan.FromSeconds(5));
		pv1.Should().NotBeNull();

		// New game should reset internal state and allow pondering again
		await coordinator.NewGameAsync();

		var secondInfo =
			new TaskCompletionSource<PrincipalVariation?>(TaskCreationOptions.RunContinuationsAsynchronously);

		coordinator.PonderInfo += pv => secondInfo.TrySetResult(pv);

		await coordinator.StartSearchAsync(Fen.Default);
		var pv2 = await secondInfo.Task.WaitAsync(TimeSpan.FromSeconds(6));
		pv2.Should().NotBeNull();

		await coordinator.StopSearchAsync();
		await coordinator.StopAsync();
	}

	[Fact]
	public async Task StartAsync_Then_GetCurrentFenAsync_ReturnsFen()
	{
		await using var coordinator = new UciCoordinator(STOCKFISH_PATH);
		await coordinator.StartAsync();

		var fen = await coordinator.GetCurrentFenAsync();

		fen.Should().NotBeNull();
		Fen.Validate(fen!.Value.Raw).Should().BeTrue();
	}

	[Fact]
	public async Task StartAsync_WithFen_ImmediatelyStartsSearches_And_BroadcastsLegalMovesAndBest()
	{
		await using var coordinator = new UciCoordinator(STOCKFISH_PATH);

		var legalTcs =
			new TaskCompletionSource<IReadOnlyCollection<string>>(TaskCreationOptions.RunContinuationsAsynchronously);

		var bestTcs =
			new TaskCompletionSource<(string best, string ponder)>(TaskCreationOptions.RunContinuationsAsynchronously);

		coordinator.LegalMovesUpdated += moves =>
		{
			if (moves is { Count: > 0 }) legalTcs.TrySetResult(moves);
		};

		coordinator.PonderBestMove += (b, p) =>
		{
			if (!string.IsNullOrWhiteSpace(b)) bestTcs.TrySetResult((b, p));
		};

		// Start engines, then set the initial position (this triggers search and legal moves)
		await coordinator.StartAsync();
		await coordinator.UpdatePositionAsync(Fen.Default, null);

		var legal = await legalTcs.Task.WaitAsync(TimeSpan.FromSeconds(5));
		legal.Should().NotBeNull();
		legal.Count.Should().BeGreaterThan(0);
		legal.Should().Contain(new[] { "e2e4", "d2d4", "g1f3", "c2c4" });

		var bestPair = await bestTcs.Task.WaitAsync(TimeSpan.FromSeconds(8));
		bestPair.best.Should().NotBeNullOrWhiteSpace();
		UciEngineClient.IsUciMoveString(bestPair.best).Should().BeTrue();
		if (!string.IsNullOrWhiteSpace(bestPair.ponder))
			UciEngineClient.IsUciMoveString(bestPair.ponder!).Should().BeTrue();

		await coordinator.StopAsync();
	}

	[Fact]
	public async Task StartPonderAsync_RaisesBestLineUpdated_FromSingleEngineStream()
	{
		await using var coordinator = new UciCoordinator(STOCKFISH_PATH);
		await coordinator.StartAsync();

		var bestLineTcs =
			new TaskCompletionSource<PrincipalVariation>(TaskCreationOptions.RunContinuationsAsynchronously);

		coordinator.PonderInfo += pv =>
		{
			if (pv.Moves is { Count: > 0 })
				bestLineTcs.TrySetResult(pv);
		};

		await coordinator.StartSearchAsync(Fen.Default);
		var pvLine = await bestLineTcs.Task.WaitAsync(TimeSpan.FromSeconds(6));

		pvLine.Moves.Should().NotBeEmpty();
		// Either CP or Mate score should be present
		(pvLine.ScoreCp.HasValue || pvLine.ScoreMate.HasValue).Should().BeTrue();

		await coordinator.StopSearchAsync();
		await coordinator.StopAsync();
	}

	[Fact]
	public async Task StartPonderAsync_ThenStop_RaisesPonderBestMove()
	{
		await using var coordinator = new UciCoordinator(STOCKFISH_PATH);
		await coordinator.StartAsync();

		string? best   = null;
		string? ponder = null;
		var     tcs    = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
		coordinator.PonderBestMove += (b, p) =>
		{
			best   = b;
			ponder = p;
			tcs.TrySetResult(true);
		};

		await coordinator.StartSearchAsync(Fen.Default);

		await tcs.Task.WaitAsync(TimeSpan.FromSeconds(5));

		await coordinator.StopSearchAsync();

		best.Should().NotBeNullOrWhiteSpace();
		UciEngineClient.IsUciMoveString(best!).Should().BeTrue();
		if (!string.IsNullOrWhiteSpace(ponder))
			UciEngineClient.IsUciMoveString(ponder!).Should().BeTrue();
	}

	[Fact]
	public async Task StartPonderAsync_WhenCalled_RaisesPonderInfo()
	{
		await using var coordinator = new UciCoordinator(STOCKFISH_PATH);
		await coordinator.StartAsync();

		var tcs = new TaskCompletionSource<PrincipalVariation?>(TaskCreationOptions.RunContinuationsAsynchronously);
		coordinator.PonderInfo += pv => tcs.TrySetResult(pv);

		await coordinator.StartSearchAsync(Fen.Default);

		var received = await tcs.Task.WaitAsync(TimeSpan.FromSeconds(5));
		received.Should().NotBeNull();
		received!.Value.Moves.Should().NotBeEmpty();
		received.Value.ScoreCp.Should().NotBeNull();

		await coordinator.StopSearchAsync();
	}

	[Fact]
	public async Task StartPonderAsync_WithInvalidFen_ThrowsArgumentException()
	{
		await using var coordinator = new UciCoordinator(STOCKFISH_PATH);
		await coordinator.StartAsync();

		await Assert.ThrowsAsync<ArgumentException>(() => coordinator.StartSearchAsync(Fen.Empty()));
	}

	[Fact]
	public async Task UpdatePositionAsync_RaisesLegalMovesUpdated_Immediately()
	{
		await using var coordinator = new UciCoordinator(STOCKFISH_PATH);
		await coordinator.StartAsync();

		var tcs = new TaskCompletionSource<IReadOnlyCollection<string>>(
			TaskCreationOptions.RunContinuationsAsynchronously);

		coordinator.LegalMovesUpdated += moves =>
		{
			if (moves is { Count: > 0 })
				tcs.TrySetResult(moves);
		};

		// Use a non-starting position to avoid short-circuit and ensure event fires for the new state
		var newFen = Fen.Parse("r1bqkbnr/pppp1ppp/2n5/4p3/2B1P3/5N2/PPPP1PPP/RNBQK2R w KQkq - 2 3")!.Value;
		await coordinator.UpdatePositionAsync(newFen, null);

		var updatedMoves = await tcs.Task.WaitAsync(TimeSpan.FromSeconds(5));
		updatedMoves.Should().NotBeNull();
		updatedMoves.Count.Should().BeGreaterThan(0);

		await coordinator.StopAsync();
	}

	[Fact]
	public async Task UpdatePositionAsync_RestartsSearch_And_RaisesInfoAgain()
	{
		await using var coordinator = new UciCoordinator(STOCKFISH_PATH);
		await coordinator.StartAsync();

		var tcsFirst  = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
		var tcsSecond = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
		var count     = 0;

		coordinator.PonderInfo += _ =>
		{
			if (Interlocked.Increment(ref count) == 1) tcsFirst.TrySetResult(true);
			else if (count >= 2) tcsSecond.TrySetResult(true);
		};

		// Start on initial position and wait for first info
		await coordinator.UpdatePositionAsync(Fen.Default, null);
		await coordinator.StartSearchAsync();
		await tcsFirst.Task.WaitAsync(TimeSpan.FromSeconds(8));

		// Switch to a different position; expect another info after restart
		var newFen = Fen.Parse("r1bqkbnr/pppp1ppp/2n5/4p3/2B1P3/5N2/PPPP1PPP/RNBQK2R w KQkq - 2 3")!.Value;
		await coordinator.UpdatePositionAsync(newFen, null);

		await tcsSecond.Task.WaitAsync(TimeSpan.FromSeconds(8));
		count.Should().BeGreaterThanOrEqualTo(2);

		await coordinator.StopSearchAsync();
	}

	[Fact]
	public async Task UpdatePositionAsync_WhenCalled_RestartsPonderAndRaisesInfo()
	{
		await using var coordinator = new UciCoordinator(STOCKFISH_PATH);
		await coordinator.StartAsync();

		var infoCount = 0;
		var tcsFirst  = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
		var tcsSecond = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

		coordinator.PonderInfo += _ =>
		{
			infoCount++;
			if (infoCount == 1) tcsFirst.TrySetResult(true);
			if (infoCount >= 2) tcsSecond.TrySetResult(true);
		};

		// Start search the initial position and wait for first info
		await coordinator.StartSearchAsync(Fen.Default);
		await tcsFirst.Task.WaitAsync(TimeSpan.FromSeconds(5));

		// Now request an update to a different position; expect another info after restart
		var newFen = Fen.Parse("r1bqkbnr/pppp1ppp/2n5/4p3/2B1P3/5N2/PPPP1PPP/RNBQK2R w KQkq - 2 3")!
						.Value; // Italian-ish setup

		var board = BoardState.FromFen(newFen)!.Value;
		await coordinator.UpdatePositionAsync(newFen, null);

		await tcsSecond.Task.WaitAsync(TimeSpan.FromSeconds(6));

		await coordinator.StopSearchAsync();
		infoCount.Should().BeGreaterThanOrEqualTo(2);
	}
}
