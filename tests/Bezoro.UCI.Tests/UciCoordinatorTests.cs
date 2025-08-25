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
	public async Task ClassifyMovesAsync_WhenCalled_YieldsAnalyses()
	{
		await using var coordinator = new UciCoordinator(STOCKFISH_PATH);
		await coordinator.StartAsync();

		var fen   = Fen.Default;
		var board = BoardState.FromFen(fen)!.Value;

		var results = new List<Move>();
		await foreach (var item in coordinator.ClassifyMovesAsync(fen, 4))
		{
			results.Add(item);
			if (results.Count >= 3) break;
		}

		results.Should().NotBeNull();
		results.Count.Should().BeGreaterThan(0);

		foreach (var move in results)
		{
			move.Notation.Should().NotBeNullOrWhiteSpace();
			UciEngineClient.IsUciMoveString(move.Notation).Should().BeTrue();

			// Score should have either Cp or Mate populated (or both for robustness)
			(move.Analysis.Score.ScoreCp.HasValue || move.Analysis.Score.ScoreMate.HasValue).Should().BeTrue();
		}
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
	public async Task StartAsync_Then_GetCurrentFenAsync_ReturnsFen()
	{
		await using var coordinator = new UciCoordinator(STOCKFISH_PATH);
		await coordinator.StartAsync();

		var fen = await coordinator.GetCurrentFenAsync();

		fen.Should().NotBeNull();
		Fen.Validate(fen!.Value.Raw).Should().BeTrue();
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

		await coordinator.StartPonderAsync(Fen.Default, null);
		await coordinator.StopPonderAsync();

		await tcs.Task.WaitAsync(TimeSpan.FromSeconds(5));

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

		await coordinator.StartPonderAsync(Fen.Default, null);

		var received = await tcs.Task.WaitAsync(TimeSpan.FromSeconds(5));
		received.Should().NotBeNull();
		received!.Value.Moves.Should().NotBeEmpty();
		received.Value.ScoreCp.Should().NotBeNull();

		await coordinator.StopPonderAsync();
	}

	[Fact]
	public async Task StartPonderAsync_WithInvalidFen_ThrowsArgumentException()
	{
		await using var coordinator = new UciCoordinator(STOCKFISH_PATH);
		await coordinator.StartAsync();

		await Assert.ThrowsAsync<ArgumentException>(() => coordinator.StartPonderAsync(Fen.Empty(), null));
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

		// Start pondering the initial position and wait for first info
		await coordinator.StartPonderAsync(Fen.Default, null);
		await tcsFirst.Task.WaitAsync(TimeSpan.FromSeconds(5));

		// Now request an update to a different position; expect another info after restart
		var newFen = Fen.Parse("r1bqkbnr/pppp1ppp/2n5/4p3/2B1P3/5N2/PPPP1PPP/RNBQK2R w KQkq - 2 3")!
						.Value; // Italian-ish setup

		var board = BoardState.FromFen(newFen)!.Value;
		await coordinator.UpdatePositionAsync(newFen, null);

		await tcsSecond.Task.WaitAsync(TimeSpan.FromSeconds(6));

		await coordinator.StopPonderAsync();
		infoCount.Should().BeGreaterThanOrEqualTo(2);
	}

	[Fact]
	public async Task GetLegalMovesWithClassificationsAsync_ReturnsParsedMoves_And_IncrementsClassified()
	{
		await using var coordinator = new UciCoordinator(STOCKFISH_PATH);
		await coordinator.StartAsync();

		// Subscribe to classification to ensure background pipeline is running
		var classifiedTcs = new TaskCompletionSource<Move>(TaskCreationOptions.RunContinuationsAsynchronously);
		coordinator.MoveClassified += (_, move) => classifiedTcs.TrySetResult(move);

		// Start background classification for the default position
		await coordinator.UpdatePositionAsync(Fen.Default, null);

		// Snapshot should return legal moves as ParsedMove
		var snapshot = await coordinator.GetLegalMovesWithClassificationsAsync();
		snapshot.Should().NotBeNull();
		snapshot.Legal.Should().NotBeNull();
		snapshot.Classified.Should().NotBeNull();

		// Common openers should be present in ParsedMove form
		snapshot.Legal.Any(m => m.Raw == "e2e4").Should().BeTrue();
		snapshot.Legal.Any(m => m.Raw == "d2d4").Should().BeTrue();

		// Initially classified may be empty; wait for the first classification event
		var firstClassified = await classifiedTcs.Task.WaitAsync(TimeSpan.FromSeconds(8));
		firstClassified.Notation.Should().NotBeNullOrWhiteSpace();

		// Snapshot should now include this move in the Classified dictionary keyed by ParsedMove
		var after  = await coordinator.GetLegalMovesWithClassificationsAsync();
		var parsed = ParsedMove.FromNotation(firstClassified.Notation);
		after.Classified.ContainsKey(parsed).Should().BeTrue();
		after.Classified[parsed].Notation.Should().Be(firstClassified.Notation);
	}

	[Fact]
	public async Task UpdatePositionAsync_CancelsPreviousClassification_And_ResetsSnapshot()
	{
		await using var coordinator = new UciCoordinator(STOCKFISH_PATH);
		await coordinator.StartAsync();

		string? firstKey  = null;
		string? secondKey = null;

		var firstClassifiedArrived = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
		var secondClassifiedArrived =
			new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

		coordinator.MoveClassified += (key, _) =>
		{
			// Capture first then second distinct keys
			if (firstKey is null)
			{
				firstKey = key;
				firstClassifiedArrived.TrySetResult(true);
			}
			else if (secondKey is null && key != firstKey)
			{
				secondKey = key;
				secondClassifiedArrived.TrySetResult(true);
			}
		};

		// Start classification on initial position, wait for at least one classified move
		await coordinator.UpdatePositionAsync(Fen.Default, null);
		await firstClassifiedArrived.Task.WaitAsync(TimeSpan.FromSeconds(8));

		// Change to a different position; snapshot should reset Classified immediately
		var newFen = Fen.Parse("r1bqkbnr/pppp1ppp/2n5/4p3/2B1P3/5N2/PPPP1PPP/RNBQK2R w KQkq - 2 3")!.Value;
		await coordinator.UpdatePositionAsync(newFen, null);

		var snapshotAfterUpdate = await coordinator.GetLegalMovesWithClassificationsAsync();
		snapshotAfterUpdate.Classified.Should().NotBeNull();
		snapshotAfterUpdate.Classified.Count.Should().Be(0);

		// And subsequent classifications should correspond to a new position key
		await secondClassifiedArrived.Task.WaitAsync(TimeSpan.FromSeconds(8));
		firstKey.Should().NotBeNull();
		secondKey.Should().NotBeNull();
		secondKey.Should().NotBe(firstKey);
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

		coordinator.BestMoveUpdated += (b, p) =>
		{
			if (!string.IsNullOrWhiteSpace(b)) bestTcs.TrySetResult((b, p));
		};

		// Start with initial position (should auto-start best/ponder searches and classification)
		await coordinator.StartAsync(Fen.Default);

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
	public async Task MoveMadeAsync_RestartsSearches_And_BroadcastsAgain()
	{
		await using var coordinator = new UciCoordinator(STOCKFISH_PATH);

		var firstBestTcs =
			new TaskCompletionSource<(string best, string ponder)>(TaskCreationOptions.RunContinuationsAsynchronously);

		var secondBestTcs =
			new TaskCompletionSource<(string best, string ponder)>(TaskCreationOptions.RunContinuationsAsynchronously);

		var bestCount = 0;
		coordinator.BestMoveUpdated += (b, p) =>
		{
			if (string.IsNullOrWhiteSpace(b)) return;

			if (Interlocked.Increment(ref bestCount) == 1)
				firstBestTcs.TrySetResult((b, p));
			else if (bestCount >= 2)
				secondBestTcs.TrySetResult((b, p));
		};

		var legalPulse =
			new TaskCompletionSource<IReadOnlyCollection<string>>(TaskCreationOptions.RunContinuationsAsynchronously);

		coordinator.LegalMovesUpdated += moves =>
		{
			if (moves is { Count: > 0 }) legalPulse.TrySetResult(moves);
		};

		await coordinator.StartAsync(Fen.Default);

		// Wait for initial best
		var first = await firstBestTcs.Task.WaitAsync(TimeSpan.FromSeconds(8));
		first.best.Should().NotBeNullOrWhiteSpace();

		// Make a legal move and ensure new best arrives after restart
		await legalPulse.Task.WaitAsync(TimeSpan.FromSeconds(5));
		await coordinator.MoveMadeAsync("e2e4");

		var second = await secondBestTcs.Task.WaitAsync(TimeSpan.FromSeconds(8));
		second.best.Should().NotBeNullOrWhiteSpace();

		await coordinator.StopAsync();
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
		coordinator.BestMoveUpdated += (b, p) =>
		{
			if (string.IsNullOrWhiteSpace(b)) return;

			if (Interlocked.Increment(ref count) == 1) bestTcs1.TrySetResult((b, p));
			else if (count >= 2) bestTcs2.TrySetResult((b, p));
		};

		// Start best search for current FEN
		await coordinator.UpdatePositionAsync(Fen.Default, null);
		await coordinator.StartBestAsync();

		var first = await bestTcs1.Task.WaitAsync(TimeSpan.FromSeconds(8));
		first.best.Should().NotBeNullOrWhiteSpace();

		// Stop and restart
		await coordinator.StopBestAsync();
		await Task.Delay(200); // tiny pause to ensure stop settles
		await coordinator.StartBestAsync();

		var second = await bestTcs2.Task.WaitAsync(TimeSpan.FromSeconds(8));
		second.best.Should().NotBeNullOrWhiteSpace();

		await coordinator.StopAsync();
	}
}
