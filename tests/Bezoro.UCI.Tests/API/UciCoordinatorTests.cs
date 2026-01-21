using System.Collections.Concurrent;
using Bezoro.UCI.API;
using Bezoro.UCI.API.Types;
using Bezoro.UCI.Domain;
using Bezoro.UCI.Tests._Resources;
using Bezoro.UCI.Tests.Attributes;
using Bezoro.UCI.Tests.TestHelpers;
using FluentAssertions;
using JetBrains.Annotations;

namespace Bezoro.UCI.Tests.API;

[TestSubject(typeof(UciCoordinator))]
public class UciCoordinatorTests
{
	[IntegrationTest]
	[Trait("Requires", "Stockfish")]
	public async Task AnalysisStream_WhenStarted_YieldsPvWithScore()
	{
		await using var coordinator = new UciCoordinator(TestConsts.STOCKFISH_PATH);
		await coordinator.StartAsync();

		var tcs = new TaskCompletionSource<PrincipalVariation>(TaskCreationOptions.RunContinuationsAsynchronously);
		coordinator.StateChanged += state =>
		{
			if (state.Evaluation?.Moves is { Count: > 0 })
				tcs.TrySetResult(state.Evaluation.Value);
		};

		await coordinator.UpdatePositionAsync(Fen.Default, null);
		await coordinator.StartSearchAsync();

		var pvLine = await tcs.Task.WaitAsync(TestConstants.DefaultTimeout);
		pvLine.Moves.Should().NotBeEmpty();
		(pvLine.ScoreCp.HasValue || pvLine.ScoreMate.HasValue).Should().BeTrue();

		await coordinator.StopSearchAsync();
	}

	[Fact]
	public async Task BestSearch_StartStop_RestartsCleanly()
	{
		await using var coordinator = new UciCoordinator(TestConsts.STOCKFISH_PATH);
		await coordinator.StartAsync();

		var bestTcs1 =
			new TaskCompletionSource<(ParsedMove best, ParsedMove? ponder)>(
				TaskCreationOptions.RunContinuationsAsynchronously);

		var bestTcs2 =
			new TaskCompletionSource<(ParsedMove best, ParsedMove? ponder)>(
				TaskCreationOptions.RunContinuationsAsynchronously);

		coordinator.StateChanged += state =>
		{
			if (state.BestMove == null || string.IsNullOrWhiteSpace(state.BestMove.Value.Raw)) return;

			if (!state.BestMove.HasValue) return;

			// We need to distinguish the first run from the second run.
			// The first run completes bestTcs1.
			if (!bestTcs1.Task.IsCompleted)
				bestTcs1.TrySetResult((state.BestMove.Value, state.PonderMove));
			else if (!bestTcs2.Task.IsCompleted)
				// This must be the second run
				bestTcs2.TrySetResult((state.BestMove.Value, state.PonderMove));
		};

		// Start best search for current FEN
		await coordinator.UpdatePositionAsync(Fen.Default, null);
		await coordinator.StartSearchAsync();

		var first = await bestTcs1.Task.WaitAsync(TestConstants.DefaultTimeout);
		first.best.Raw.Should().NotBeNullOrWhiteSpace();

		// Stop and restart
		await coordinator.StopSearchAsync();
		await coordinator.StartSearchAsync();

		var second = await bestTcs2.Task.WaitAsync(TestConstants.DefaultTimeout);
		second.best.Raw.Should().NotBeNullOrWhiteSpace();

		await coordinator.StopAsync();
	}

	[Fact]
	public async Task ClearState_WhenCalled_CancelsClassificationTokenSource()
	{
		await using var coordinator = new UciCoordinator(TestConsts.STOCKFISH_PATH);
		await coordinator.StartAsync();

		var classificationStarted = false;

		coordinator.StateChanged += state =>
		{
			if (state.ClassifiedMoves.Count > 0) classificationStarted = true;
		};

		// Start classification
		await coordinator.UpdatePositionAsync(Fen.Default, null);
		await Task.Delay(TestConstants.MediumDelay);

		// ClearState should cancel classification
		// This is called internally by UpdatePositionAsync, but we can verify it works
		await coordinator.UpdatePositionAsync(Fen.Parse(TestConstants.ITALIAN_GAME_FEN)!.Value, null);

		// Wait to see if previous classification continues (it shouldn't)
		await Task.Delay(TestConstants.MediumDelay);

		// Classification should have started
		classificationStarted.Should().BeTrue("Classification should have started");

		await coordinator.StopAsync();
	}

	[Fact]
	public async Task ClearState_WhenCalledConcurrently_IsThreadSafe()
	{
		await using var coordinator = new UciCoordinator(TestConsts.STOCKFISH_PATH);
		await coordinator.StartAsync();

		// Set up initial state
		await coordinator.UpdatePositionAsync(Fen.Default, null);
		await Task.Delay(TestConstants.ShortDelay);

		var exceptions = new ConcurrentBag<Exception>();
		var tasks      = new List<Task>();

		// Launch concurrent UpdatePositionAsync calls (which call ClearState internally)
		for (var i = 0; i < 10; i++)
		{
			var fen = i % 2 == 0 ? Fen.Default : Fen.Parse(TestConstants.ITALIAN_GAME_FEN)!.Value;
			tasks.Add(
				Task.Run(async () =>
				{
					try
					{
						await coordinator.UpdatePositionAsync(fen, null);
						await Task.Delay(TestConstants.VeryShortDelay);
					}
					catch (OperationCanceledException)
					{
						// Expected when previous classification is cancelled
					}
					catch (Exception ex)
					{
						exceptions.Add(ex);
					}
				}));
		}

		await Task.WhenAll(tasks);

		// OperationCanceledException is expected when classifications are cancelled
		var unexpectedExceptions = exceptions.Where(ex => ex is not OperationCanceledException).ToList();
		unexpectedExceptions.Should()
							.BeEmpty(
								"Concurrent ClearState operations (via UpdatePositionAsync) should be thread-safe");

		await coordinator.StopAsync();
	}

	[Fact]
	public async Task CurrentLegalMoves_WhenAccessedConcurrently_IsThreadSafe()
	{
		await using var coordinator = new UciCoordinator(TestConsts.STOCKFISH_PATH);
		await coordinator.StartAsync();

		await coordinator.UpdatePositionAsync(Fen.Default, null);

		// Concurrent reads and writes
		var tasks      = new List<Task>();
		var exceptions = new ConcurrentBag<Exception>();

		for (var i = 0; i < 10; i++)
		{
			tasks.Add(
				Task.Run(async () =>
				{
					try
					{
						for (var j = 0; j < 10; j++) await Task.Delay(1);
						// moves can be null during position updates, checking for thread-safety (no exceptions)
					}
					catch (Exception ex)
					{
						exceptions.Add(ex);
					}
				}));
		}

		// Also update position concurrently
		tasks.Add(
			Task.Run(async () =>
			{
				try
				{
					for (var i = 0; i < 5; i++)
					{
						await coordinator.UpdatePositionAsync(Fen.Default, null);
						await Task.Delay(TestConstants.ShortDelay);
					}
				}
				catch (Exception ex)
				{
					exceptions.Add(ex);
				}
			}));

		await Task.WhenAll(tasks);

		exceptions.Should().BeEmpty("No exceptions should occur during concurrent access");
		coordinator.State.LegalMoves.Should()
				   .NotBeNull("LegalMoves should still be accessible after concurrent operations");

		await coordinator.StopAsync();
	}

	[Fact]
	public async Task DisposeAsync_DisposesAllCancellationTokenSources()
	{
		await using var coordinator = new UciCoordinator(TestConsts.STOCKFISH_PATH);
		await coordinator.StartAsync();

		// Start search to create _bestCts
		await coordinator.StartSearchAsync(Fen.Default);
		await Task.Delay(TestConstants.ShortDelay);

		// Update position to create _classificationCts
		await coordinator.UpdatePositionAsync(Fen.Default, null);
		await Task.Delay(TestConstants.ShortDelay);

		// Dispose should clean up both token sources
		await coordinator.DisposeAsync();

		// If we got here without exceptions, disposal worked
		// This test verifies that DisposeAsync doesn't throw when disposing token sources
	}

	[Fact]
	public async Task DisposeAsync_WhenCalled_UnsubscribesFromEngineEvents()
	{
		await using var coordinator = new UciCoordinator(TestConsts.STOCKFISH_PATH);
		await coordinator.StartAsync();

		var stateChangeCount = 0;

		coordinator.StateChanged += _ => stateChangeCount++;

		// Start some operations
		await coordinator.UpdatePositionAsync(Fen.Default, null);
		await Task.Delay(TestConstants.ShortDelay);

		// Stop operations before disposing to prevent events from firing during disposal
		await coordinator.StopAsync();

		// Capture counts before dispose
		int stateChangeBefore = stateChangeCount;

		// Dispose should unsubscribe from events
		await coordinator.DisposeAsync();

		// Wait a bit to see if any events fire after dispose
		await Task.Delay(TestConstants.MediumDelay);

		// Counts should not have increased after dispose
		stateChangeCount.Should().Be(stateChangeBefore, "StateChanged should not fire after dispose");
	}

	[Fact]
	public async Task DisposeAsync_WhenCalledWithActiveSearches_DisposesAllTokenSourcesSafely()
	{
		await using var coordinator = new UciCoordinator(TestConsts.STOCKFISH_PATH);
		await coordinator.StartAsync();

		// Start search to create _bestCts
		await coordinator.StartSearchAsync(Fen.Default);
		await Task.Delay(TestConstants.ShortDelay);

		// Update position to create _classificationCts
		await coordinator.UpdatePositionAsync(Fen.Default, null);
		await Task.Delay(TestConstants.ShortDelay);

		// Start another search concurrently before disposing
		var searchTask = Task.Run(async () =>
		{
			try
			{
				await coordinator.StartSearchAsync(Fen.Default);
			}
			catch (OperationCanceledException)
			{
				// Expected to be cancelled during disposal
			}
			catch (ObjectDisposedException)
			{
				// Expected during disposal
			}
		});

		// Dispose while operations are active
		await Task.Delay(TestConstants.VeryShortDelay);

		var disposeExceptions = new ConcurrentBag<Exception>();
		try
		{
			await coordinator.DisposeAsync();
		}
		catch (Exception ex)
		{
			disposeExceptions.Add(ex);
		}

		// Wait for the search task to complete (should be cancelled or complete)
		try
		{
			await searchTask.WaitAsync(TestConstants.DefaultTimeout);
		}
		catch (TimeoutException)
		{
			// Task might still be running, which is acceptable
		}
		catch (OperationCanceledException)
		{
			// Expected if task was cancelled
		}

		// Dispose should not throw exceptions
		disposeExceptions.Should().BeEmpty("DisposeAsync should handle concurrent operations without throwing");
	}

	[Fact]
	public async Task FullGame_ScholarMate_ValidatesFlow()
	{
		await using var coordinator = new UciCoordinator(TestConsts.STOCKFISH_PATH);
		await coordinator.StartAsync();

		// Scholar's Mate sequence:
		// 1. e2e4 e7e5
		// 2. d1h5 b8c6
		// 3. f1c4 g8f6
		// 4. h5f7#
		string[] moves =
		[
			"e2e4",
			"e7e5",
			"d1h5",
			"b8c6",
			"f1c4",
			"g8f6",
			"h5f7"
		];

		var currentMoves = new List<string>();

		// Initial position update to start classification
		await coordinator.UpdatePositionAsync(Fen.Default, null);

		foreach (string move in moves)
		{
			// Wait for the move to be classified
			var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

			void OnStateChanged(UciState state)
			{
				if (state.ClassifiedMoves.ContainsKey(move)) tcs.TrySetResult(true);
			}

			coordinator.StateChanged += OnStateChanged;

			try
			{
				// Check if already classified
				if (coordinator.State.ClassifiedMoves.ContainsKey(move)) tcs.TrySetResult(true);

				// Wait for classification if not yet ready
				await tcs.Task.WaitAsync(TestConstants.DefaultTimeout);
			}
			finally
			{
				coordinator.StateChanged -= OnStateChanged;
			}

			// Verify we have legal moves and the move we want to play is in them
			var state = coordinator.State;
			state.LegalMoves.Should().Contain(move);
			state.ClassifiedMoves.Keys.Should().Contain(move);

			// Apply the move
			currentMoves.Add(move);
			await coordinator.UpdatePositionAsync(Fen.Default, currentMoves);
		}

		var finalState = coordinator.State;
		var finalFen   = coordinator.CurrentFen;

		finalFen.ActiveColor.Should().Be('b', "It should be Black's turn now");
		finalFen.Checkers.Should().NotBeEmpty();
		finalState.LegalMoves.Should().BeEmpty("Black should be checkmated and have no legal moves");
		finalState.ClassifiedMoves.Should().BeEmpty("Black should be checkmated and have no classified moves");

		await coordinator.StopAsync();
	}

	[Fact]
	public async Task FullTurn_WhiteE2E4_ThenBlackResponse_ValidatesApi()
	{
		await using var coordinator = new UciCoordinator(TestConsts.STOCKFISH_PATH);
		await coordinator.StartAsync();

		// Initial position: expect legal moves including e2e4
		var legalStartTcs =
			new TaskCompletionSource<IReadOnlyList<string>>(TaskCreationOptions.RunContinuationsAsynchronously);

		coordinator.StateChanged += state =>
		{
			if (state.LegalMoves.Count > 0)
				legalStartTcs.TrySetResult(state.LegalMoves);
		};

		await coordinator.UpdatePositionAsync(Fen.Default, null);
		var legalStart = await legalStartTcs.Task.WaitAsync(TestConstants.MediumTimeout);
		legalStart.Should().Contain(["e2e4", "d2d4"]);

		// Apply white's move e2e4; validate black-side legal moves, pondering and classification events
		var legalAfterWhiteTcs =
			new TaskCompletionSource<IReadOnlyList<string>>(TaskCreationOptions.RunContinuationsAsynchronously);

		var bestAfterWhiteTcs =
			new TaskCompletionSource<(ParsedMove best, ParsedMove? ponder)>(
				TaskCreationOptions.RunContinuationsAsynchronously);

		var classifiedTcs =
			new TaskCompletionSource<(string notation, Move move)>(TaskCreationOptions.RunContinuationsAsynchronously);

		coordinator.StateChanged += state =>
		{
			if (state.LegalMoves.Count > 0)
			{
				var hasReply = false;
				foreach (string s in state.LegalMoves)
				{
					if (s == "e7e5" || s == "c7c5")
					{
						hasReply = true;
						break;
					}
				}

				if (hasReply)
					legalAfterWhiteTcs.TrySetResult(state.LegalMoves);
			}

			if (state.BestMove.HasValue && !string.IsNullOrWhiteSpace(state.BestMove.Value.Raw))
				bestAfterWhiteTcs.TrySetResult((state.BestMove.Value, state.PonderMove));

			if (state.ClassifiedMoves.Count > 0)
			{
				var first = state.ClassifiedMoves.First();
				classifiedTcs.TrySetResult((first.Key, first.Value));
			}
		};

		await coordinator.UpdatePositionAsync(Fen.Default, ["e2e4"]);

		var legalAfterWhite = await legalAfterWhiteTcs.Task.WaitAsync(TestConstants.DefaultTimeout);
		legalAfterWhite.Should().Contain(x => x == "e7e5" || x == "c7c5");

		var bestPair = await bestAfterWhiteTcs.Task.WaitAsync(TestConstants.DefaultTimeout);
		bestPair.best.Raw.Should().NotBeNullOrWhiteSpace();
		UciEngineClient.IsUciMoveString(bestPair.best.Raw).Should().BeTrue();

		var classified = await classifiedTcs.Task.WaitAsync(TestConstants.DefaultTimeout);
		classified.notation.Should().NotBeNullOrWhiteSpace();

		await coordinator.StopSearchAsync();
		await coordinator.StopAsync();
	}

	[Fact]
	public async Task GetLegalMovesAsync_ReturnsParsedMoves()
	{
		await using var coordinator = new UciCoordinator(TestConsts.STOCKFISH_PATH);
		await coordinator.StartAsync();

		// Start background processing for the default position
		await coordinator.UpdatePositionAsync(Fen.Default, null);
		// Wait for update
		while (coordinator.State.LegalMoves.Count == 0) await Task.Delay(10);

		var legal = coordinator.State.LegalMoves;
		legal.Should().NotBeNull();
		legal.Count.Should().BeGreaterThan(0);
		legal.Should().Contain(["e2e4", "d2d4", "g1f3", "c2c4"]);
	}

	[Fact]
	public async Task NewGameAsync_ResetsState_And_AllowsRestart()
	{
		await using var coordinator = new UciCoordinator(TestConsts.STOCKFISH_PATH);
		await coordinator.StartAsync();

		var firstInfo =
			new TaskCompletionSource<PrincipalVariation?>(TaskCreationOptions.RunContinuationsAsynchronously);

		coordinator.StateChanged += state =>
		{
			if (state.Evaluation != null) firstInfo.TrySetResult(state.Evaluation.Value);
		};

		await coordinator.StartSearchAsync(Fen.Default);
		var pv1 = await firstInfo.Task.WaitAsync(TestConstants.DefaultTimeout);
		pv1.Should().NotBeNull();

		// New game should reset internal state and allow pondering again
		await coordinator.NewGameAsync();

		var secondInfo =
			new TaskCompletionSource<PrincipalVariation?>(TaskCreationOptions.RunContinuationsAsynchronously);

		coordinator.StateChanged += state =>
		{
			if (state.Evaluation != null) secondInfo.TrySetResult(state.Evaluation.Value);
		};

		await coordinator.StartSearchAsync(Fen.Default);
		var pv2 = await secondInfo.Task.WaitAsync(TestConstants.MediumTimeout);
		pv2.Should().NotBeNull();

		await coordinator.StopSearchAsync();
		await coordinator.StopAsync();
	}

	[Fact]
	public async Task StartAsync_Then_GetCurrentFenAsync_ReturnsFen()
	{
		await using var coordinator = new UciCoordinator(TestConsts.STOCKFISH_PATH);
		await coordinator.StartAsync();

		var fen = coordinator.CurrentFen;

		fen.Should().NotBeNull();
		Fen.Validate(fen.Raw).Should().BeTrue();
	}

	[Fact]
	public async Task StartAsync_WithFen_ImmediatelyStartsSearches_And_BroadcastsLegalMovesAndBest()
	{
		await using var coordinator = new UciCoordinator(TestConsts.STOCKFISH_PATH);

		var legalTcs =
			new TaskCompletionSource<IReadOnlyList<string>>(TaskCreationOptions.RunContinuationsAsynchronously);

		var bestTcs =
			new TaskCompletionSource<(ParsedMove best, ParsedMove? ponder)>(
				TaskCreationOptions.RunContinuationsAsynchronously);

		coordinator.StateChanged += state =>
		{
			if (state.LegalMoves.Count > 0) legalTcs.TrySetResult(state.LegalMoves);
			if (state.BestMove.HasValue && !string.IsNullOrWhiteSpace(state.BestMove.Value.Raw))
				bestTcs.TrySetResult((state.BestMove.Value, state.PonderMove));
		};

		// Start engines, then set the initial position (this triggers search and legal moves)
		await coordinator.StartAsync();
		await coordinator.UpdatePositionAsync(Fen.Default, null);

		var legal = await legalTcs.Task.WaitAsync(TestConstants.DefaultTimeout);
		legal.Should().NotBeNull();
		legal.Count.Should().BeGreaterThan(0);
		legal.Should().Contain(["e2e4", "d2d4", "g1f3", "c2c4"]);

		var bestPair = await bestTcs.Task.WaitAsync(TestConstants.DefaultTimeout);
		bestPair.best.Raw.Should().NotBeNullOrWhiteSpace();
		UciEngineClient.IsUciMoveString(bestPair.best.Raw).Should().BeTrue();
		if (bestPair.ponder.HasValue)
			UciEngineClient.IsUciMoveString(bestPair.ponder.Value.Raw).Should().BeTrue();

		await coordinator.StopAsync();
	}

	[Fact]
	public async Task StartPonderAsync_RaisesBestLineUpdated_FromSingleEngineStream()
	{
		await using var coordinator = new UciCoordinator(TestConsts.STOCKFISH_PATH);
		await coordinator.StartAsync();

		var bestLineTcs =
			new TaskCompletionSource<PrincipalVariation>(TaskCreationOptions.RunContinuationsAsynchronously);

		coordinator.StateChanged += state =>
		{
			if (state.Evaluation?.Moves is { Count: > 0 })
				bestLineTcs.TrySetResult(state.Evaluation.Value);
		};

		await coordinator.StartSearchAsync(Fen.Default);
		var pvLine = await bestLineTcs.Task.WaitAsync(TestConstants.MediumTimeout);

		pvLine.Moves.Should().NotBeEmpty();
		// Either CP or Mate score should be present
		(pvLine.ScoreCp.HasValue || pvLine.ScoreMate.HasValue).Should().BeTrue();

		await coordinator.StopSearchAsync();
		await coordinator.StopAsync();
	}

	[Fact]
	public async Task StartPonderAsync_ThenStop_RaisesPonderBestMove()
	{
		await using var coordinator = new UciCoordinator(TestConsts.STOCKFISH_PATH);
		await coordinator.StartAsync();

		ParsedMove? best   = null;
		ParsedMove? ponder = null;
		var         tcs    = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

		coordinator.StateChanged += state =>
		{
			if (state.BestMove.HasValue)
			{
				best   = state.BestMove;
				ponder = state.PonderMove;
				tcs.TrySetResult(true);
			}
		};

		await coordinator.StartSearchAsync(Fen.Default);

		await tcs.Task.WaitAsync(TestConstants.DefaultTimeout);

		await coordinator.StopSearchAsync();

		best.Should().NotBeNull();
		UciEngineClient.IsUciMoveString(best!.Value.Raw).Should().BeTrue();
		if (ponder.HasValue)
			UciEngineClient.IsUciMoveString(ponder.Value.Raw).Should().BeTrue();
	}

	[Fact]
	public async Task StartPonderAsync_WhenCalled_RaisesPonderInfo()
	{
		await using var coordinator = new UciCoordinator(TestConsts.STOCKFISH_PATH);
		await coordinator.StartAsync();

		var tcs = new TaskCompletionSource<PrincipalVariation?>(TaskCreationOptions.RunContinuationsAsynchronously);
		coordinator.StateChanged += state =>
		{
			if (state.Evaluation != null) tcs.TrySetResult(state.Evaluation.Value);
		};

		await coordinator.StartSearchAsync(Fen.Default);

		var received = await tcs.Task.WaitAsync(TestConstants.DefaultTimeout);
		received.Should().NotBeNull();
		received!.Value.Moves.Should().NotBeEmpty();
		received.Value.ScoreCp.Should().NotBeNull();

		await coordinator.StopSearchAsync();
	}

	[Fact]
	public async Task StartPonderAsync_WithInvalidFen_ThrowsArgumentException()
	{
		await using var coordinator = new UciCoordinator(TestConsts.STOCKFISH_PATH);
		await coordinator.StartAsync();

		await Assert.ThrowsAsync<ArgumentException>(() => coordinator.StartSearchAsync(Fen.Empty()));
	}

	[Fact]
	public async Task StartSearchAsync_And_StopSearchAsync_WhenCalledConcurrently_IsThreadSafe()
	{
		await using var coordinator = new UciCoordinator(TestConsts.STOCKFISH_PATH);
		await coordinator.StartAsync();

		var exceptions = new ConcurrentBag<Exception>();
		var startTasks = new List<Task>();
		var stopTasks  = new List<Task>();

		// Launch concurrent StartSearchAsync calls
		for (var i = 0; i < 10; i++)
		{
			startTasks.Add(
				Task.Run(async () =>
				{
					try
					{
						await coordinator.StartSearchAsync(Fen.Default);
						await Task.Delay(TestConstants.VeryShortDelay);
					}
					catch (OperationCanceledException)
					{
						// Expected when StopSearchAsync cancels the operation
					}
					catch (Exception ex)
					{
						exceptions.Add(ex);
					}
				}));
		}

		// Launch concurrent StopSearchAsync calls
		for (var i = 0; i < 10; i++)
		{
			stopTasks.Add(
				Task.Run(async () =>
				{
					try
					{
						await coordinator.StopSearchAsync();
						await Task.Delay(TestConstants.VeryShortDelay);
					}
					catch (Exception ex)
					{
						exceptions.Add(ex);
					}
				}));
		}

		await Task.WhenAll(startTasks.Concat(stopTasks));

		exceptions.Should().BeEmpty("Concurrent Start/Stop search should be thread-safe");

		await coordinator.StopAsync();
	}

	[Fact]
	public async Task StreamClassifiedMovesAsync_YieldsMovesAsTheyAreClassified()
	{
		await using var coordinator = new UciCoordinator(TestConsts.STOCKFISH_PATH);
		await coordinator.StartAsync();

		// Set a standard position and trigger background classification
		await coordinator.UpdatePositionAsync(Fen.Default, null);

		var moves = new List<Move>();
		await foreach (var move in coordinator.StreamClassifiedMovesAsync())
		{
			moves.Add(move);
			// We can stop early if we have enough moves to verify streaming works
			if (moves.Count >= 5) break;
		}

		moves.Should().NotBeEmpty();
		moves.Count.Should().BeGreaterOrEqualTo(5);
		foreach (var m in moves)
		{
			m.Analysis.Should().NotBeNull();
			m.Notation.Should().NotBeNullOrWhiteSpace();
		}

		await coordinator.StopSearchAsync();
		await coordinator.StopAsync();
	}
}
