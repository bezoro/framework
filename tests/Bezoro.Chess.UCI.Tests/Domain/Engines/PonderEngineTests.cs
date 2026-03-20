using Bezoro.Chess.UCI.API.Types;
using Bezoro.Chess.UCI.Domain;
using Bezoro.Chess.UCI.Domain.Engines;
using Bezoro.Chess.UCI.Tests.TestHelpers;
using FluentAssertions;
using JetBrains.Annotations;

namespace Bezoro.Chess.UCI.Tests.Domain.Engines;

[TestSubject(typeof(PonderEngine))]
[Trait("Category", "Integration")]
public class PonderEngineTests
{
	[Fact]
	public async Task BestMove_WhenCpScoreImproves_ShouldRaiseBestMove()
	{
		await using var engine = new PonderEngine(TestResourcePaths.STOCKFISH_PATH);
		await engine.StartAsync();
		await engine.SetOptionAsync("MultiPv", "1");

		var bestMoveCount = 0;
		var tcs           = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

		engine.BestMove += (best, ponder) =>
		{
			bestMoveCount++;
			// BestMove should be raised when score improves
			// We expect at least one BestMove event as the engine finds better moves
			if (bestMoveCount >= 1)
				tcs.TrySetResult(true);
		};

		await engine.StartSearchAsync(Fen.Default, null);
		await tcs.Task.WaitAsync(TestConstants.ExtendedTimeout);
		await engine.StopSearchAsync();

		bestMoveCount.Should().BeGreaterThan(0, "BestMove should be raised when score improves");
	}

	[Fact]
	public async Task BestMove_WhenInfoPvContainsBestAndPonderMoves_ShouldMatchParsedPvMoves()
	{
		await using var engine = new PonderEngine(TestResourcePaths.STOCKFISH_PATH);
		await engine.StartAsync();

		// Ensure a single PV stream
		await engine.SetOptionAsync("MultiPv", "1");

		PrincipalVariation? lastPv = null;
		var                 tcs    = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

		engine.InfoPv += pv =>
		{
			// Capture a PV with at least two moves to validate both best and ponder
			if (pv.Moves is { Count: >= 2 })
				lastPv = pv;
		};

		engine.BestMove += (best, ponder) =>
		{
			// Validate only when we have a captured PV
			if (lastPv is { } pv && pv.Moves is { Count: >= 1 })
			{
				var expectedBest = ParsedMove.FromNotation(pv.Moves[0]);

				// Exact-value checks
				best.Raw.Should().Be(expectedBest.Raw);
				best.From.Should().Be(expectedBest.From);
				best.To.Should().Be(expectedBest.To);
				best.Notation.Should().Be(expectedBest.Notation);

				// Internal consistency checks
				UciEngineClient.IsUciMoveString(best.Raw).Should().BeTrue();
				best.From.Length.Should().Be(2);
				best.To.Length.Should().Be(2);
				best.Notation.Length.Should().Be(4);

				if (pv.Moves.Count >= 2)
				{
					var expectedPonder = ParsedMove.FromNotation(pv.Moves[1]);

					ponder.Should().NotBeNull();
					ponder!.Value.Raw.Should().Be(expectedPonder.Raw);
					ponder.Value.From.Should().Be(expectedPonder.From);
					ponder.Value.To.Should().Be(expectedPonder.To);
					ponder.Value.Notation.Should().Be(expectedPonder.Notation);

					UciEngineClient.IsUciMoveString(ponder.Value.Raw).Should().BeTrue();
					ponder.Value.From.Length.Should().Be(2);
					ponder.Value.To.Length.Should().Be(2);
					ponder.Value.Notation.Length.Should().Be(4);
				}

				tcs.TrySetResult(true);
			}
		};

		await engine.StartSearchAsync(Fen.Default, null);

		await tcs.Task.WaitAsync(TestConstants.ExtendedTimeout);

		await engine.StopSearchAsync();
	}

	[Fact]
	public async Task BestMove_WhenInfoPvIsReceivedConcurrently_ShouldBeThreadSafe()
	{
		// Arrange
		var engine = new PonderEngine(TestResourcePaths.STOCKFISH_PATH);
		engine.EnableOutputForwardingForTests();

		var bestMoveCount = 0;
		var lockObj       = new object();
		engine.BestMove += (best, ponder) =>
		{
			lock (lockObj)
			{
				bestMoveCount++;
			}
		};

		// Create multiple PVs with improving scores
		var pvs = Enumerable.Range(1, 20)
							.Select(i => TestDataBuilders.PrincipalVariation()
														 .WithScoreCp(i * 10)                    // Improving cp scores
														 .WithMoves($"e{i % 8 + 2}e{i % 8 + 4}") // Valid move notation
														 .Build()
							)
							.ToArray();

		// Act: Invoke concurrently
		var tasks = pvs.Select(pv => Task.Run(() => engine.OnClientInfoPvReceived(pv)))
					   .ToArray();

		await Task.WhenAll(tasks);

		// Assert: Should have raised BestMove multiple times (at least for improvements)
		// Thread safety means no exceptions and consistent state
		bestMoveCount.Should().BeGreaterThan(0, "BestMove should be raised for score improvements");
		// All tasks should complete without exceptions (thread safety)
		tasks.All(t => t.IsCompletedSuccessfully).Should().BeTrue();
	}

	[Fact]
	public async Task BestMove_WhenMateScoreIsFound_ShouldRaiseBestMove()
	{
		// Test with a mate-in-one position to verify mate score handling
		var mateFen = Fen.Parse(TestConstants.WHITE_MATE_IN_ONE_FEN);
		if (!mateFen.HasValue)
			throw new InvalidOperationException("Failed to parse mate FEN");

		await using var engine = new PonderEngine(TestResourcePaths.STOCKFISH_PATH);
		await engine.StartAsync();
		await engine.SetOptionAsync("MultiPv", "1");

		var bestMoveRaised = false;
		var tcs            = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

		engine.BestMove += (best, ponder) =>
		{
			bestMoveRaised = true;
			best.Raw.Should().NotBeNullOrWhiteSpace();
			tcs.TrySetResult(true);
		};

		await engine.StartSearchAsync(mateFen.Value, null);
		await tcs.Task.WaitAsync(TestConstants.ExtendedTimeout);
		await engine.StopSearchAsync();

		bestMoveRaised.Should().BeTrue("BestMove should be raised when mate score is found");
	}

	[Fact]
	public void BestMove_WhenNegativeMateScoreImproves_ShouldRaiseBestMove()
	{
		// Arrange: mate in -1 (better) vs mate in -5 (worse)
		var engine = new PonderEngine(TestResourcePaths.STOCKFISH_PATH);
		engine.EnableOutputForwardingForTests();

		var         bestMoveRaised = false;
		ParsedMove? receivedBest   = null;
		engine.BestMove += (best, ponder) =>
		{
			bestMoveRaised = true;
			receivedBest   = best;
		};

		// First PV: mate in -5 (losing, worse)
		var pv1 = TestDataBuilders.PrincipalVariation()
								  .WithScoreMate(-5)
								  .WithMoves("e2e4")
								  .Build();

		// Second PV: mate in -1 (losing, but better - less negative)
		var pv2 = TestDataBuilders.PrincipalVariation()
								  .WithScoreMate(-1)
								  .WithMoves("d2d4")
								  .Build();

		// Act: Simulate receiving worse mate first, then better mate
		engine.OnClientInfoPvReceived(pv1);
		bestMoveRaised.Should().BeTrue("First PV should raise BestMove");
		bestMoveRaised = false; // Reset

		engine.OnClientInfoPvReceived(pv2);
		bestMoveRaised.Should().BeTrue("Improved negative mate score should raise BestMove");
		receivedBest.Should().NotBeNull();
		receivedBest!.Value.Notation.Should().Be("d2d4");
	}

	[Fact]
	public void BestMove_WhenPositiveMateScoreImproves_ShouldRaiseBestMove()
	{
		// Arrange: mate in 1 (better) vs mate in 5 (worse)
		var engine = new PonderEngine(TestResourcePaths.STOCKFISH_PATH);
		engine.EnableOutputForwardingForTests();

		var         bestMoveRaised = false;
		ParsedMove? receivedBest   = null;
		engine.BestMove += (best, ponder) =>
		{
			bestMoveRaised = true;
			receivedBest   = best;
		};

		// First PV: mate in 5 (winning, worse)
		var pv1 = TestDataBuilders.PrincipalVariation()
								  .WithScoreMate(5)
								  .WithMoves("e2e4")
								  .Build();

		// Second PV: mate in 1 (winning, better - lower number)
		var pv2 = TestDataBuilders.PrincipalVariation()
								  .WithScoreMate(1)
								  .WithMoves("d2d4")
								  .Build();

		// Act: Simulate receiving worse mate first, then better mate
		engine.OnClientInfoPvReceived(pv1);
		bestMoveRaised.Should().BeTrue("First PV should raise BestMove");
		bestMoveRaised = false; // Reset

		engine.OnClientInfoPvReceived(pv2);
		bestMoveRaised.Should().BeTrue("Improved positive mate score should raise BestMove");
		receivedBest.Should().NotBeNull();
		receivedBest!.Value.Notation.Should().Be("d2d4");
	}

	[Fact]
	public void BestMove_WhenTransitioningFromCpToMate_ShouldRaiseBestMove()
	{
		// Arrange: cp score → mate score (mate is always better)
		var engine = new PonderEngine(TestResourcePaths.STOCKFISH_PATH);
		engine.EnableOutputForwardingForTests();

		var bestMoveRaised = false;
		engine.BestMove += (best, ponder) => { bestMoveRaised = true; };

		// First PV: cp score
		var pv1 = TestDataBuilders.PrincipalVariation()
								  .WithScoreCp(100)
								  .WithMoves("e2e4")
								  .Build();

		// Second PV: mate score (should be better)
		var pv2 = TestDataBuilders.PrincipalVariation()
								  .WithScoreMate(3)
								  .WithMoves("d2d4")
								  .Build();

		// Act
		engine.OnClientInfoPvReceived(pv1);
		bestMoveRaised.Should().BeTrue("First PV should raise BestMove");
		bestMoveRaised = false;

		engine.OnClientInfoPvReceived(pv2);
		bestMoveRaised.Should().BeTrue("Transition from cp to mate should raise BestMove");
	}

	[Fact]
	public void BestMove_WhenTransitioningFromNegativeMateToNegativeCp_ShouldCompareScoresCorrectly()
	{
		// Arrange: negative mate → negative cp (both losing, compare cp values)
		var engine = new PonderEngine(TestResourcePaths.STOCKFISH_PATH);
		engine.EnableOutputForwardingForTests();

		var bestMoveCount = 0;
		engine.BestMove += (best, ponder) => { bestMoveCount++; };

		// First PV: negative mate (losing)
		var pv1 = TestDataBuilders.PrincipalVariation()
								  .WithScoreMate(-3)
								  .WithMoves("e2e4")
								  .Build();

		// Second PV: negative cp, but higher (less negative) - should be better
		var pv2 = TestDataBuilders.PrincipalVariation()
								  .WithScoreCp(-50)
								  .WithMoves("d2d4")
								  .Build();

		// Third PV: negative cp, even higher (better)
		var pv3 = TestDataBuilders.PrincipalVariation()
								  .WithScoreCp(-20)
								  .WithMoves("c2c4")
								  .Build();

		// Act
		engine.OnClientInfoPvReceived(pv1);
		bestMoveCount.Should().Be(1, "First PV should raise BestMove");

		engine.OnClientInfoPvReceived(pv2);
		// Transition from mate to cp when both are losing: compare cp values
		// Since we're transitioning, we check if newCp > lastCp (if lastCp exists)
		// Initially lastCp is null, so this should raise BestMove
		bestMoveCount.Should().Be(
			2,
			"Transition from negative mate to negative cp should raise BestMove if cp is higher"
		);

		engine.OnClientInfoPvReceived(pv3);
		bestMoveCount.Should().Be(3, "Higher negative cp should raise BestMove");
	}

	[Fact]
	public void BestMove_WhenTransitioningFromNegativeMateToPositiveCp_ShouldRaiseBestMove()
	{
		// Arrange: negative mate (losing) → positive cp (winning cp is better)
		var engine = new PonderEngine(TestResourcePaths.STOCKFISH_PATH);
		engine.EnableOutputForwardingForTests();

		var bestMoveCount = 0;
		engine.BestMove += (best, ponder) => { bestMoveCount++; };

		// First PV: negative mate (losing)
		var pv1 = TestDataBuilders.PrincipalVariation()
								  .WithScoreMate(-3)
								  .WithMoves("e2e4")
								  .Build();

		// Second PV: positive cp (winning - better than losing mate)
		var pv2 = TestDataBuilders.PrincipalVariation()
								  .WithScoreCp(50)
								  .WithMoves("d2d4")
								  .Build();

		// Act
		engine.OnClientInfoPvReceived(pv1);
		bestMoveCount.Should().Be(1, "First PV should raise BestMove");

		engine.OnClientInfoPvReceived(pv2);
		bestMoveCount.Should().Be(2, "Transition from negative mate to positive cp should raise BestMove");
	}

	[Fact]
	public void BestMove_WhenTransitioningFromPositiveMateToCp_ShouldNotRaiseBestMove()
	{
		// Arrange: positive mate (winning) → cp (mate is always better)
		var engine = new PonderEngine(TestResourcePaths.STOCKFISH_PATH);
		engine.EnableOutputForwardingForTests();

		var bestMoveCount = 0;
		engine.BestMove += (best, ponder) => { bestMoveCount++; };

		// First PV: positive mate (winning)
		var pv1 = TestDataBuilders.PrincipalVariation()
								  .WithScoreMate(3)
								  .WithMoves("e2e4")
								  .Build();

		// Second PV: cp score (should NOT be better than winning mate)
		var pv2 = TestDataBuilders.PrincipalVariation()
								  .WithScoreCp(200)
								  .WithMoves("d2d4")
								  .Build();

		// Act
		engine.OnClientInfoPvReceived(pv1);
		bestMoveCount.Should().Be(1, "First PV should raise BestMove");

		engine.OnClientInfoPvReceived(pv2);
		bestMoveCount.Should().Be(1, "Transition from positive mate to cp should NOT raise BestMove");
	}

	[Fact]
	public async Task Dispose_WhenCalled_ShouldDisposeEngine()
	{
		var engine = new PonderEngine(TestResourcePaths.STOCKFISH_PATH);
		await engine.StartAsync();

		// ReSharper disable once MethodHasAsyncOverload
		engine.Dispose();

		engine.IsStarted.Should().BeFalse();
		engine.IsHealthy.Should().BeFalse();
		engine.Status.Should().Be(TransportStatus.Disposed);
	}

	[Fact]
	public async Task DisposeAsync_WhenCalled_ShouldDisposeEngine()
	{
		var engine = new PonderEngine(TestResourcePaths.STOCKFISH_PATH);
		await engine.StartAsync();

		await engine.DisposeAsync();

		engine.IsStarted.Should().BeFalse();
		engine.IsHealthy.Should().BeFalse();
		engine.Status.Should().Be(TransportStatus.Disposed);
	}

	[Fact]
	public async Task NewGameAsync_WhenCalledWhilePondering_ShouldStopSearchAndAllowRestart()
	{
		await using var engine = new PonderEngine(TestResourcePaths.STOCKFISH_PATH);
		await engine.StartAsync();

		var firstInfo =
			new TaskCompletionSource<PrincipalVariation?>(TaskCreationOptions.RunContinuationsAsynchronously);

		engine.InfoPv += pv => firstInfo.TrySetResult(pv);

		// Start search and wait for first info
		await engine.StartSearchAsync(Fen.Default, null);
		var pv1 = await firstInfo.Task.WaitAsync(TestConstants.MediumTimeout);
		pv1.Should().NotBeNull();

		// New game should stop search and reset internal state
		await engine.NewGameAsync();

		var secondInfo =
			new TaskCompletionSource<PrincipalVariation?>(TaskCreationOptions.RunContinuationsAsynchronously);

		engine.InfoPv += pv => secondInfo.TrySetResult(pv);

		// Start search again and ensure we receive info
		await engine.StartSearchAsync(Fen.Default, null);
		var pv2 = await secondInfo.Task.WaitAsync(TestConstants.MediumTimeout);
		pv2.Should().NotBeNull();

		await engine.StopSearchAsync();
	}

	[Fact]
	public async Task SetOptionAsync_WhenMultiPv2_ShouldEmitMultipv2InInfoStream()
	{
		await using var engine = new PonderEngine(TestResourcePaths.STOCKFISH_PATH);
		await engine.StartAsync();

		// Request multiple PVs
		await engine.SetOptionAsync("MultiPv", "2");

		var sawMultiPv2 = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
		engine.InfoPv += pv =>
		{
			// Some engines start from MultiPv=1 then emit MultiPv=2 shortly after
			if (pv.MultiPv == 2)
				sawMultiPv2.TrySetResult(true);
		};

		await engine.StartSearchAsync(Fen.Default, null);
		bool got = await sawMultiPv2.Task.WaitAsync(TestConstants.ExtendedTimeout);

		got.Should().BeTrue();

		await engine.StopSearchAsync();
	}

	[Fact]
	public async Task StartAsync_WhenCalled_ShouldStartEngine()
	{
		var engine = new PonderEngine(TestResourcePaths.STOCKFISH_PATH);

		await engine.StartAsync();

		engine.IsStarted.Should().BeTrue();
		engine.IsHealthy.Should().BeTrue();
		engine.Status.Should().Be(TransportStatus.Started);
	}

	[Fact]
	public async Task StartSearchAsync_WhenCalled_ShouldRaiseInfoPv()
	{
		await using var engine = new PonderEngine(TestResourcePaths.STOCKFISH_PATH);
		await engine.StartAsync();
		var tcs = new TaskCompletionSource<PrincipalVariation?>(TaskCreationOptions.RunContinuationsAsynchronously);
		engine.InfoPv += pv => tcs.TrySetResult(pv);

		await engine.StartSearchAsync(Fen.Default, null);

		var received = await tcs.Task.WaitAsync(TestConstants.DefaultTimeout);
		engine.Activity.Should().Be(EngineActivity.Searching);
		received?.Moves.Should().NotBeEmpty();
		received?.ScoreCp.Should().NotBeNull();
	}

	[Fact]
	public async Task StartSearchAsync_WhenCalledTwice_ShouldRemainSearching()
	{
		await using var engine = new PonderEngine(TestResourcePaths.STOCKFISH_PATH);
		await engine.StartAsync();

		var infoTcs = new TaskCompletionSource<PrincipalVariation?>(TaskCreationOptions.RunContinuationsAsynchronously);
		engine.InfoPv += pv => infoTcs.TrySetResult(pv);

		await engine.StartSearchAsync(Fen.Default, null);
		await infoTcs.Task.WaitAsync(TimeSpan.FromSeconds(6));
		engine.Activity.Should().Be(EngineActivity.Searching);

		// Calling again with the same position should not throw and should remain searching
		await engine.StartSearchAsync(Fen.Default, null);
		engine.Activity.Should().Be(EngineActivity.Searching);

		await engine.StopSearchAsync();
	}

	[Fact]
	public async Task StartSearchAsync_WhenRetargetedToDifferentPosition_ShouldRestartOnNewPosition()
	{
		var mateFen = Fen.Parse(TestConstants.WHITE_MATE_IN_ONE_FEN);
		mateFen.Should().NotBeNull();

		await using var engine = new PonderEngine(TestResourcePaths.STOCKFISH_PATH);
		await engine.StartAsync();

		var initialInfoTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
		var mateMoveTcs = new TaskCompletionSource<ParsedMove>(TaskCreationOptions.RunContinuationsAsynchronously);

		engine.InfoPv += _ => initialInfoTcs.TrySetResult(true);
		engine.BestMove += (best, _) =>
		{
			if (best.Raw == "f7g7")
				mateMoveTcs.TrySetResult(best);
		};

		await engine.StartSearchAsync(Fen.Default, null);
		await initialInfoTcs.Task.WaitAsync(TestConstants.ExtendedTimeout);

		await engine.StartSearchAsync(mateFen!.Value, null);

		var bestMove = await mateMoveTcs.Task.WaitAsync(TestConstants.ExtendedTimeout);
		bestMove.Raw.Should().Be("f7g7");

		await engine.StopSearchAsync();
	}

	[Fact]
	public async Task StartSearchAsync_WhenFenIsInvalid_ShouldThrowArgumentException()
	{
		await using var engine = new PonderEngine(TestResourcePaths.STOCKFISH_PATH);
		await engine.StartAsync();

		await Assert.ThrowsAsync<ArgumentException>(() => engine.StartSearchAsync(Fen.Empty(), null));
	}

	[Fact]
	public async Task StartSearchAsync_WhenInfoIsRaised_ShouldUpdateActivityTransitions()
	{
		await using var engine = new PonderEngine(TestResourcePaths.STOCKFISH_PATH);
		await engine.StartAsync();

		var infoTcs = new TaskCompletionSource<PrincipalVariation?>(TaskCreationOptions.RunContinuationsAsynchronously);
		engine.InfoPv += pv => infoTcs.TrySetResult(pv);

		await engine.StartSearchAsync(Fen.Default, null);
		var pv = await infoTcs.Task.WaitAsync(TestConstants.MediumTimeout);
		pv.Should().NotBeNull();
		engine.Activity.Should().Be(EngineActivity.Searching);

		await engine.StopSearchAsync();
		engine.Activity.Should().Be(EngineActivity.Idle);
	}

	[Fact]
	public async Task StartSearchAsync_WhenStopped_ShouldRaiseBestMove()
	{
		await using var engine = new PonderEngine(TestResourcePaths.STOCKFISH_PATH);
		await engine.StartAsync();

		ParsedMove? best   = null;
		ParsedMove? ponder = null;
		var         tcs    = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
		engine.BestMove += (b, p) =>
		{
			best   = b;
			ponder = p;
			tcs.TrySetResult(true);
		};

		await engine.StartSearchAsync(Fen.Default, null);
		await tcs.Task.WaitAsync(TestConstants.DefaultTimeout);
		await engine.StopAsync();

		best.Should().NotBeNull();
		UciEngineClient.IsUciMoveString(best!.Value.Raw).Should().BeTrue();
		if (ponder.HasValue)
			UciEngineClient.IsUciMoveString(ponder.Value.Raw).Should().BeTrue();
	}

	[Fact]
	public async Task StopAsync_WhenCalled_ShouldStopEngine()
	{
		await using var engine = new PonderEngine(TestResourcePaths.STOCKFISH_PATH);
		await engine.StartAsync();

		await engine.StopAsync();

		engine.IsStarted.Should().BeFalse();
		engine.Status.Should().Be(TransportStatus.Stopped);
	}

	[Fact]
	public async Task StopSearchAsync_WhenCalled_ShouldStopTheSearch()
	{
		await using var engine = new PonderEngine(TestResourcePaths.STOCKFISH_PATH);
		await engine.StartAsync();

		await engine.StartSearchAsync(Fen.Default, null);
		await engine.StopSearchAsync();

		engine.Activity.Should().Be(EngineActivity.Idle);
	}

	[Fact]
	public async Task StopSearchAsync_WhenCalledTwice_ShouldStopSearchWithoutThrowing()
	{
		await using var engine = new PonderEngine(TestResourcePaths.STOCKFISH_PATH);
		await engine.StartAsync();

		// First run
		await engine.StartSearchAsync(Fen.Default, null);
		await engine.StopSearchAsync();
		engine.Activity.Should().Be(EngineActivity.Idle);

		// Second run
		await engine.StartSearchAsync(Fen.Default, null);
		await engine.StopSearchAsync();
		engine.Activity.Should().Be(EngineActivity.Idle);
	}
}
