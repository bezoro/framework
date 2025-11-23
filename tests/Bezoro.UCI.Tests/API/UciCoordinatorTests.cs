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
		coordinator.PonderInfo += pv =>
		{
			if (pv.Moves is { Count: > 0 })
				tcs.TrySetResult(pv);
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

		var count = 0;
		coordinator.PonderBestMove += (b, p) =>
		{
			if (string.IsNullOrWhiteSpace(b.Raw)) return;

			if (Interlocked.Increment(ref count) == 1) bestTcs1.TrySetResult((b, p));
			else if (count >= 2) bestTcs2.TrySetResult((b, p));
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

		coordinator.NewMoveClassified += (_, _) => { classificationStarted = true; };

		// Start classification
		await coordinator.UpdatePositionAsync(Fen.Default, null);
		await Task.Delay(TestConstants.MediumDelay);

		// ClearState should cancel classification
		// This is called internally by UpdatePositionAsync, but we can verify it works
		await coordinator.UpdatePositionAsync(Fen.Parse(TestConstants.ItalianGameFen)!.Value, null);

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
			var fen = i % 2 == 0 ? Fen.Default : Fen.Parse(TestConstants.ItalianGameFen)!.Value;
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
			int taskId = i;
			tasks.Add(
				Task.Run(async () =>
				{
					try
					{
						for (var j = 0; j < 10; j++)
						{
							var moves = coordinator.CurrentLegalMoves;
							await Task.Delay(1);
							// moves can be null during position updates, checking for thread-safety (no exceptions)
						}
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
		coordinator.CurrentLegalMoves.Should()
				   .NotBeNull("CurrentLegalMoves should still be accessible after concurrent operations");

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

		var ponderInfoCount         = 0;
		var bestMoveCount           = 0;
		var moveClassifiedCount     = 0;
		var allMovesClassifiedCount = 0;

		coordinator.PonderInfo         += _ => ponderInfoCount++;
		coordinator.PonderBestMove     += (_, _) => bestMoveCount++;
		coordinator.NewMoveClassified  += (_, _) => moveClassifiedCount++;
		coordinator.AllMovesClassified += _ => allMovesClassifiedCount++;

		// Start some operations
		await coordinator.UpdatePositionAsync(Fen.Default, null);
		await Task.Delay(TestConstants.ShortDelay);

		// Stop operations before disposing to prevent events from firing during disposal
		await coordinator.StopAsync();

		// Capture counts before dispose
		int ponderInfoBefore         = ponderInfoCount;
		int bestMoveBefore           = bestMoveCount;
		int moveClassifiedBefore     = moveClassifiedCount;
		int allMovesClassifiedBefore = allMovesClassifiedCount;

		// Dispose should unsubscribe from events
		await coordinator.DisposeAsync();

		// Wait a bit to see if any events fire after dispose
		await Task.Delay(TestConstants.MediumDelay);

		// Counts should not have increased after dispose
		ponderInfoCount.Should().Be(ponderInfoBefore, "PonderInfo should not fire after dispose");
		bestMoveCount.Should().Be(bestMoveBefore, "PonderBestMove should not fire after dispose");
		moveClassifiedCount.Should().Be(moveClassifiedBefore, "NewMoveClassified should not fire after dispose");
		allMovesClassifiedCount.Should().Be(
			allMovesClassifiedBefore,
			"AllMovesClassified should not fire after dispose");
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

			void OnNewMoveClassified(string notation, Move _)
			{
				if (notation == move) tcs.TrySetResult(true);
			}

			coordinator.NewMoveClassified += OnNewMoveClassified;

			try
			{
				// Check if already classified
				var snapshot = await coordinator.GetLegalMovesWithClassificationsAsync();
				if (snapshot.Classified.Values.Any(m => m.Notation == move)) tcs.TrySetResult(true);

				// Wait for classification if not yet ready
				await tcs.Task.WaitAsync(TestConstants.DefaultTimeout);
			}
			finally
			{
				coordinator.NewMoveClassified -= OnNewMoveClassified;
			}

			// Verify we have legal moves and the move we want to play is in them
			var legalMoves = await coordinator.GetLegalMovesWithClassificationsAsync();
			legalMoves.Legal.Should().Contain(m => m.Raw == move);
			legalMoves.Classified.Values.Should().Contain(m => m.Notation == move);

			// Apply the move
			currentMoves.Add(move);
			await coordinator.UpdatePositionAsync(Fen.Default, currentMoves);
		}

		var finalLegalMoves = await coordinator.GetLegalMovesWithClassificationsAsync();
		var finalFen        = await coordinator.GetCurrentFenAsync();

		finalFen?.ActiveColor.Should().Be('b', "It should be Black's turn now");
		finalFen?.Checkers.Should().NotBeEmpty();
		finalLegalMoves.Legal.Should().BeEmpty("Black should be checkmated and have no legal moves");
		finalLegalMoves.Classified.Should().BeEmpty("Black should be checkmated and have no classified moves");

		await coordinator.StopAsync();
	}

	[Fact]
	public async Task FullTurn_WhiteE2E4_ThenBlackResponse_ValidatesApi()
	{
		await using var coordinator = new UciCoordinator(TestConsts.STOCKFISH_PATH);
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
		var legalStart = await legalStartTcs.Task.WaitAsync(TestConstants.MediumTimeout);
		legalStart.Should().Contain(new[] { "e2e4", "d2d4" });

		// Apply white's move e2e4; validate black-side legal moves, pondering and classification events
		var legalAfterWhiteTcs =
			new TaskCompletionSource<IReadOnlyCollection<string>>(TaskCreationOptions.RunContinuationsAsynchronously);

		var bestAfterWhiteTcs =
			new TaskCompletionSource<(ParsedMove best, ParsedMove? ponder)>(
				TaskCreationOptions.RunContinuationsAsynchronously);

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
			if (!string.IsNullOrWhiteSpace(best.Raw))
				bestAfterWhiteTcs.TrySetResult((best, ponder));
		};

		coordinator.NewMoveClassified += (notation, move) =>
		{
			if (!string.IsNullOrWhiteSpace(notation))
				classifiedTcs.TrySetResult((notation, move));
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

		// Legal moves should contain common openers
		var legal = await coordinator.GetLegalMovesAsync();
		legal.Should().NotBeNull();
		legal.Count.Should().BeGreaterThan(0);
		legal.Should().Contain(new[] { "e2e4", "d2d4", "g1f3", "c2c4" });
	}

	[Fact]
	public async Task GetLegalMovesAsync_WhenCalled_ReturnsCommonOpeners()
	{
		await using var coordinator = new UciCoordinator(TestConsts.STOCKFISH_PATH);
		await coordinator.StartAsync();

		var legalMoves = await coordinator.GetLegalMovesAsync();

		legalMoves.Should().Contain(new[] { "e2e4", "d2d4", "g1f3", "c2c4" });
	}

	[Fact]
	public async Task GetLegalMovesWithClassificationsAsync_WhenCalled_ReturnsMoveSnapshot()
	{
		await using var coordinator = new UciCoordinator(TestConsts.STOCKFISH_PATH);
		await coordinator.StartAsync();

		// Subscribe to capture at least one classification
		var classifiedTcs =
			new TaskCompletionSource<(string notation, Move move)>(TaskCreationOptions.RunContinuationsAsynchronously);

		coordinator.NewMoveClassified += (notation, move) =>
		{
			if (!string.IsNullOrWhiteSpace(notation))
				classifiedTcs.TrySetResult((notation, move));
		};

		// Set a standard position and trigger background classification
		await coordinator.UpdatePositionAsync(Fen.Default, null);

		// Await at least one classified move to ensure snapshot contains some classifications
		var firstClassified = await classifiedTcs.Task.WaitAsync(TestConstants.DefaultTimeout);

		// Get the snapshot: legal moves + any classifications ready so far
		var snapshot = await coordinator.GetLegalMovesWithClassificationsAsync();

		// Legal moves should be non-empty and contain common openers
		snapshot.Legal.Should().NotBeNull();
		snapshot.Legal.Count.Should().BeGreaterThan(0);
		snapshot.Legal.Should().Contain(m => m.Raw == "e2e4" || m.Raw == "d2d4");

		// There should be at least one classification captured
		snapshot.Classified.Should().NotBeNull();
		snapshot.Classified.Count.Should().BeGreaterThan(0);

		// Snapshot should include the classified move we observed
		var key = ParsedMove.FromNotation(firstClassified.notation);
		snapshot.Classified.TryGetValue(key, out var classifiedMove).Should().BeTrue();
		classifiedMove.Notation.Should().Be(firstClassified.notation);

		// All classified moves should be legal in the current position
		var legalSet = new HashSet<string>(StringComparer.Ordinal);
		foreach (var pm in snapshot.Legal) legalSet.Add(pm.Raw);
		foreach (var pm in snapshot.Classified.Keys)
			legalSet.Contains(pm.Raw).Should().BeTrue($"classified move {pm.Raw} must be legal");

		await coordinator.StopSearchAsync();
		await coordinator.StopAsync();
	}

	[Fact]
	public async Task NewGameAsync_ResetsState_And_AllowsRestart()
	{
		await using var coordinator = new UciCoordinator(TestConsts.STOCKFISH_PATH);
		await coordinator.StartAsync();

		var firstInfo =
			new TaskCompletionSource<PrincipalVariation?>(TaskCreationOptions.RunContinuationsAsynchronously);

		coordinator.PonderInfo += pv => firstInfo.TrySetResult(pv);

		await coordinator.StartSearchAsync(Fen.Default);
		var pv1 = await firstInfo.Task.WaitAsync(TestConstants.DefaultTimeout);
		pv1.Should().NotBeNull();

		// New game should reset internal state and allow pondering again
		await coordinator.NewGameAsync();

		var secondInfo =
			new TaskCompletionSource<PrincipalVariation?>(TaskCreationOptions.RunContinuationsAsynchronously);

		coordinator.PonderInfo += pv => secondInfo.TrySetResult(pv);

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

		var fen = await coordinator.GetCurrentFenAsync();

		fen.Should().NotBeNull();
		Fen.Validate(fen!.Value.Raw).Should().BeTrue();
	}

	[Fact]
	public async Task StartAsync_WithFen_ImmediatelyStartsSearches_And_BroadcastsLegalMovesAndBest()
	{
		await using var coordinator = new UciCoordinator(TestConsts.STOCKFISH_PATH);

		var legalTcs =
			new TaskCompletionSource<IReadOnlyCollection<string>>(TaskCreationOptions.RunContinuationsAsynchronously);

		var bestTcs =
			new TaskCompletionSource<(ParsedMove best, ParsedMove? ponder)>(
				TaskCreationOptions.RunContinuationsAsynchronously);

		coordinator.LegalMovesUpdated += moves =>
		{
			if (moves is { Count: > 0 }) legalTcs.TrySetResult(moves);
		};

		coordinator.PonderBestMove += (b, p) =>
		{
			if (!string.IsNullOrWhiteSpace(b.Raw)) bestTcs.TrySetResult((b, p));
		};

		// Start engines, then set the initial position (this triggers search and legal moves)
		await coordinator.StartAsync();
		await coordinator.UpdatePositionAsync(Fen.Default, null);

		var legal = await legalTcs.Task.WaitAsync(TestConstants.DefaultTimeout);
		legal.Should().NotBeNull();
		legal.Count.Should().BeGreaterThan(0);
		legal.Should().Contain(new[] { "e2e4", "d2d4", "g1f3", "c2c4" });

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

		coordinator.PonderInfo += pv =>
		{
			if (pv.Moves is { Count: > 0 })
				bestLineTcs.TrySetResult(pv);
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
		coordinator.PonderBestMove += (b, p) =>
		{
			best   = b;
			ponder = p;
			tcs.TrySetResult(true);
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
		coordinator.PonderInfo += pv => tcs.TrySetResult(pv);

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
						await Task.Delay(TestConstants.VeryShortDelay);
						await coordinator.StopSearchAsync();
					}
					catch (Exception ex)
					{
						exceptions.Add(ex);
					}
				}));
		}

		await Task.WhenAll(startTasks);
		await Task.WhenAll(stopTasks);

		// Filter out expected OperationCanceledException - only unexpected exceptions should be present
		var unexpectedExceptions = exceptions.Where(ex => ex is not OperationCanceledException).ToList();
		unexpectedExceptions.Should().BeEmpty(
			"Concurrent StartSearchAsync and StopSearchAsync should be thread-safe (OperationCanceledException is expected)");

		await coordinator.StopAsync();
	}

	[Fact]
	public async Task MakeMoveAsync_WhenCalled_AppendsMoveAndUpdatesPosition()
	{
		await using var coordinator = new UciCoordinator(TestConsts.STOCKFISH_PATH);
		await coordinator.StartAsync();

		// Start with default position
		await coordinator.UpdatePositionAsync(Fen.Default, null);

		// Make a move
		await coordinator.MakeMoveAsync("e2e4");

		// Verify position updated
		var fen = await coordinator.GetCurrentFenAsync();
		fen.Should().NotBeNull();
		// After e2e4, it should be black's turn
		fen!.Value.ActiveColor.Should().Be('b');

		// Verify legal moves are for black (e.g. e7e5, c7c5)
		var legalMoves = await coordinator.GetLegalMovesAsync();
		legalMoves.Should().Contain(new[] { "e7e5", "c7c5" });

		// Make another move
		await coordinator.MakeMoveAsync("e7e5");

		// Verify position updated again
		fen = await coordinator.GetCurrentFenAsync();
		fen!.Value.ActiveColor.Should().Be('w');

		await coordinator.StopAsync();
	}

	[Fact]
	public async Task UndoLastMoveAsync_WhenCalled_RevertsLastMoveAndUpdatesPosition()
	{
		await using var coordinator = new UciCoordinator(TestConsts.STOCKFISH_PATH);
		await coordinator.StartAsync();

		// Start with default position
		await coordinator.UpdatePositionAsync(Fen.Default, null);

		// Make a move
		await coordinator.MakeMoveAsync("e2e4");

		// Verify position updated (Black to move)
		var fen = await coordinator.GetCurrentFenAsync();
		fen!.Value.ActiveColor.Should().Be('b');

		// Undo the move
		bool undone = await coordinator.UndoLastMoveAsync();
		undone.Should().BeTrue();

		// Verify position reverted (White to move)
		fen = await coordinator.GetCurrentFenAsync();
		fen!.Value.ActiveColor.Should().Be('w');

		// Verify legal moves are back to start position
		var legalMoves = await coordinator.GetLegalMovesAsync();
		legalMoves.Should().Contain(new[] { "e2e4", "d2d4" });

		// Try to undo again (should fail as no moves left)
		undone = await coordinator.UndoLastMoveAsync();
		undone.Should().BeFalse();

		await coordinator.StopAsync();
	}

	[Fact]
	public async Task MoveCallbacks_WhenActionsPerformed_AreInvoked()
	{
		await using var coordinator = new UciCoordinator(TestConsts.STOCKFISH_PATH);
		await coordinator.StartAsync();

		// Start with default position
		await coordinator.UpdatePositionAsync(Fen.Default, null);

		string? madeMove   = null;
		string? undoneMove = null;

		coordinator.MoveMade   += m => madeMove   = m;
		coordinator.MoveUndone += m => undoneMove = m;

		// Make a move
		await coordinator.MakeMoveAsync("e2e4");

		madeMove.Should().Be("e2e4");
		undoneMove.Should().BeNull();

		// Undo the move
		await coordinator.UndoLastMoveAsync();

		undoneMove.Should().Be("e2e4");

		// Reset and try another move
		madeMove   = null;
		undoneMove = null;

		await coordinator.MakeMoveAsync("d2d4");
		madeMove.Should().Be("d2d4");

		await coordinator.StopAsync();
	}

	[Fact]
	public async Task StartSearchAsync_WhenCalledConcurrently_DoesNotCauseDoubleDispose()
	{
		await using var coordinator = new UciCoordinator(TestConsts.STOCKFISH_PATH);
		await coordinator.StartAsync();

		var exceptions = new ConcurrentBag<Exception>();
		var tasks      = new List<Task>();

		// Launch multiple concurrent StartSearchAsync calls
		for (var i = 0; i < 10; i++)
		{
			tasks.Add(
				Task.Run(async () =>
				{
					try
					{
						await coordinator.StartSearchAsync(Fen.Default);
						await Task.Delay(TestConstants.VeryShortDelay);
					}
					catch (OperationCanceledException)
					{
						// Expected when operations are cancelled
					}
					catch (Exception ex)
					{
						exceptions.Add(ex);
					}
				}));
		}

		await Task.WhenAll(tasks);

		// Should not have any ObjectDisposedException or InvalidOperationException
		// OperationCanceledException is expected and acceptable
		var unexpectedExceptions = exceptions.Where(ex => ex is not OperationCanceledException).ToList();
		unexpectedExceptions.Should().BeEmpty("Concurrent StartSearchAsync calls should not cause disposal errors");

		await coordinator.StopSearchAsync();
		await coordinator.StopAsync();
	}

	[Fact]
	public async Task StartSearchAsync_WhenCalledMultipleTimes_DisposesOldCancellationTokens()
	{
		await using var coordinator = new UciCoordinator(TestConsts.STOCKFISH_PATH);
		await coordinator.StartAsync();

		// Start first search
		await coordinator.StartSearchAsync(Fen.Default);
		await Task.Delay(TestConstants.ShortDelay);

		// Start second search - should dispose old token
		await coordinator.StartSearchAsync(Fen.Default);
		await Task.Delay(TestConstants.ShortDelay);

		// Start third search - should dispose previous token
		await coordinator.StartSearchAsync(Fen.Default);
		await Task.Delay(TestConstants.ShortDelay);

		// If we got here without exceptions, disposal is working
		await coordinator.StopSearchAsync();
		await coordinator.StopAsync();
	}

	[Fact]
	public async Task StartSearchAsync_WhenCalledMultipleTimesRapidly_ProperlyDisposesOldTokens()
	{
		await using var coordinator = new UciCoordinator(TestConsts.STOCKFISH_PATH);
		await coordinator.StartAsync();

		var exceptions = new ConcurrentBag<Exception>();

		// Rapidly start and stop searches
		for (var i = 0; i < 20; i++)
		{
			try
			{
				await coordinator.StartSearchAsync(Fen.Default);
				await Task.Delay(TestConstants.VeryShortDelay);
				await coordinator.StopSearchAsync();
				await Task.Delay(TestConstants.VeryShortDelay);
			}
			catch (OperationCanceledException)
			{
				// Expected when operations are cancelled
			}
			catch (Exception ex)
			{
				exceptions.Add(ex);
			}
		}

		// OperationCanceledException is expected and acceptable
		var unexpectedExceptions = exceptions.Where(ex => ex is not OperationCanceledException).ToList();
		unexpectedExceptions.Should()
							.BeEmpty("Rapid StartSearchAsync/StopSearchAsync cycles should properly dispose tokens");

		await coordinator.StopAsync();
	}

	[Fact]
	public async Task StartSearchAsync_WhenEffectiveFenIsNull_DisposesCancellationToken()
	{
		await using var coordinator = new UciCoordinator(TestConsts.STOCKFISH_PATH);
		await coordinator.StartAsync();

		// Set an invalid position that will cause GetCurrentFenAsync to return null
		// This tests the early return path where effectiveFen is null
		await coordinator.UpdatePositionAsync(Fen.Default, null);

		// Try to start search without FEN - should handle null FEN gracefully
		// Note: This may not actually return null in practice, but tests the code path
		await coordinator.StartSearchAsync();

		await coordinator.StopSearchAsync();
		await coordinator.StopAsync();
	}

	[Fact]
	public async Task StopSearchAsync_WhenCalledConcurrently_IsThreadSafe()
	{
		await using var coordinator = new UciCoordinator(TestConsts.STOCKFISH_PATH);
		await coordinator.StartAsync();

		// Start a search first
		await coordinator.StartSearchAsync(Fen.Default);
		await Task.Delay(TestConstants.ShortDelay);

		var exceptions = new ConcurrentBag<Exception>();
		var tasks      = new List<Task>();

		// Launch multiple concurrent StopSearchAsync calls
		for (var i = 0; i < 10; i++)
		{
			tasks.Add(
				Task.Run(async () =>
				{
					try
					{
						await coordinator.StopSearchAsync();
					}
					catch (Exception ex)
					{
						exceptions.Add(ex);
					}
				}));
		}

		await Task.WhenAll(tasks);

		exceptions.Should().BeEmpty("Concurrent StopSearchAsync calls should be thread-safe");

		await coordinator.StopAsync();
	}

	[Fact]
	public async Task UpdatePositionAsync_RaisesLegalMovesUpdated_Immediately()
	{
		await using var coordinator = new UciCoordinator(TestConsts.STOCKFISH_PATH);
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

		var updatedMoves = await tcs.Task.WaitAsync(TestConstants.DefaultTimeout);
		updatedMoves.Should().NotBeNull();
		updatedMoves.Count.Should().BeGreaterThan(0);

		await coordinator.StopAsync();
	}

	[Fact]
	public async Task UpdatePositionAsync_RestartsSearch_And_RaisesInfoAgain()
	{
		await using var coordinator = new UciCoordinator(TestConsts.STOCKFISH_PATH);
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
		await tcsFirst.Task.WaitAsync(TestConstants.DefaultTimeout);

		// Switch to a different position; expect another info after restart
		var newFen = Fen.Parse("r1bqkbnr/pppp1ppp/2n5/4p3/2B1P3/5N2/PPPP1PPP/RNBQK2R w KQkq - 2 3")!.Value;
		await coordinator.UpdatePositionAsync(newFen, null);

		await tcsSecond.Task.WaitAsync(TestConstants.DefaultTimeout);
		count.Should().BeGreaterThanOrEqualTo(2);

		await coordinator.StopSearchAsync();
	}

	[Fact]
	public async Task UpdatePositionAsync_WhenCalled_RestartsPonderAndRaisesInfo()
	{
		await using var coordinator = new UciCoordinator(TestConsts.STOCKFISH_PATH);
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
		await tcsFirst.Task.WaitAsync(TestConstants.DefaultTimeout);

		// Now request an update to a different position; expect another info after restart
		var newFen = Fen.Parse("r1bqkbnr/pppp1ppp/2n5/4p3/2B1P3/5N2/PPPP1PPP/RNBQK2R w KQkq - 2 3")!
						.Value; // Italian-ish setup

		var board = BoardState.FromFen(newFen)!.Value;
		await coordinator.UpdatePositionAsync(newFen, null);

		await tcsSecond.Task.WaitAsync(TestConstants.MediumTimeout);

		await coordinator.StopSearchAsync();
		infoCount.Should().BeGreaterThanOrEqualTo(2);
	}

	[Fact]
	public async Task UpdatePositionAsync_WhenCalledConcurrently_HandlesClassificationTokenSourceSafely()
	{
		await using var coordinator = new UciCoordinator(TestConsts.STOCKFISH_PATH);
		await coordinator.StartAsync();

		var exceptions = new ConcurrentBag<Exception>();
		var tasks      = new List<Task>();
		var fens = new[]
		{
			Fen.Default,
			Fen.Parse(TestConstants.ItalianGameFen)!.Value,
			Fen.Parse(TestConstants.AfterE2E4Fen)!.Value
		};

		// Launch multiple concurrent UpdatePositionAsync calls
		for (var i = 0; i < 15; i++)
		{
			var fen = fens[i % fens.Length];
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
							.BeEmpty("Concurrent UpdatePositionAsync calls should handle _classificationCts safely");

		await coordinator.StopAsync();
	}

	[Fact]
	public async Task UpdatePositionAsync_WhenCalledMultipleTimes_CancelsPreviousClassification()
	{
		await using var coordinator = new UciCoordinator(TestConsts.STOCKFISH_PATH);
		await coordinator.StartAsync();

		var classificationCount = 0;
		coordinator.NewMoveClassified += (_, _) => Interlocked.Increment(ref classificationCount);

		// Start first position and wait for at least one classification
		await coordinator.UpdatePositionAsync(Fen.Default, null);
		await Task.Delay(TestConstants.StandardDelay);

		int firstCount = classificationCount;
		firstCount.Should().BeGreaterThan(0, "At least one move should be classified initially");

		// Update to new position - should cancel previous classification and start new one
		var newFen = Fen.Parse(TestConstants.ItalianGameFen)!.Value;
		await coordinator.UpdatePositionAsync(newFen, null);
		await Task.Delay(TestConstants.StandardDelay);

		// Update to another position
		await coordinator.UpdatePositionAsync(Fen.Default, null);
		await Task.Delay(TestConstants.StandardDelay);

		// Classification should have progressed (not stuck on first position)
		classificationCount.Should().BeGreaterThan(firstCount, "Classification should continue after position updates");

		await coordinator.StopAsync();
	}

	[Fact]
	public async Task UpdatePositionAsync_WhenCalledMultipleTimesRapidly_ProperlyCancelsPreviousClassification()
	{
		await using var coordinator = new UciCoordinator(TestConsts.STOCKFISH_PATH);
		await coordinator.StartAsync();

		var exceptions          = new ConcurrentBag<Exception>();
		var classificationCount = 0;
		var fens = new[]
		{
			Fen.Default,
			Fen.Parse(TestConstants.ItalianGameFen)!.Value,
			Fen.Parse(TestConstants.AfterE2E4Fen)!.Value
		};

		coordinator.NewMoveClassified += (_, _) => Interlocked.Increment(ref classificationCount);

		// Rapidly update positions
		for (var i = 0; i < 10; i++)
		{
			try
			{
				var fen = fens[i % fens.Length];
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
		}

		await Task.Delay(TestConstants.StandardDelay);

		// OperationCanceledException is expected when classifications are cancelled
		var unexpectedExceptions = exceptions.Where(ex => ex is not OperationCanceledException).ToList();
		unexpectedExceptions.Should()
							.BeEmpty("Rapid UpdatePositionAsync calls should properly cancel previous classifications");

		classificationCount.Should().BeGreaterThan(0, "At least some classifications should complete");

		await coordinator.StopAsync();
	}
}
