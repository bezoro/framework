using Bezoro.UCI.API.Types;
using Bezoro.UCI.Domain;
using FluentAssertions;
using JetBrains.Annotations;

namespace Bezoro.UCI.Tests;

[TestSubject(typeof(PonderEngine))]
[Trait("Category", "Integration")]
public class PonderEngineTests
{
	public const string STOCKFISH_PATH = "Engine/stockfish/stockfish-windows-x86-64-avx2.exe";

	[Fact]
	public async Task BestMove_EmitsParsedBestAndPonderMatchingPv()
	{
		await using var engine = new PonderEngine(STOCKFISH_PATH);
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

		await tcs.Task.WaitAsync(TimeSpan.FromSeconds(8));

		await engine.StopSearchAsync();
	}

	[Fact]
	public async Task Dispose_WhenCalled_DisposesTheEngine()
	{
		var engine = new PonderEngine(STOCKFISH_PATH);
		await engine.StartAsync();

		// ReSharper disable once MethodHasAsyncOverload
		engine.Dispose();

		engine.IsStarted.Should().BeFalse();
		engine.IsHealthy.Should().BeFalse();
		engine.Status.Should().Be(ProcessUciTransport.TransportStatus.Disposed);
	}

	[Fact]
	public async Task DisposeAsync_WhenCalled_DisposesTheEngine()
	{
		var engine = new PonderEngine(STOCKFISH_PATH);
		await engine.StartAsync();

		await engine.DisposeAsync();

		engine.IsStarted.Should().BeFalse();
		engine.IsHealthy.Should().BeFalse();
		engine.Status.Should().Be(ProcessUciTransport.TransportStatus.Disposed);
	}

	[Fact]
	public async Task NewGameAsync_WhilePondering_StopsSearch_And_AllowsRestart()
	{
		await using var engine = new PonderEngine(STOCKFISH_PATH);
		await engine.StartAsync();

		var firstInfo =
			new TaskCompletionSource<PrincipalVariation?>(TaskCreationOptions.RunContinuationsAsynchronously);

		engine.InfoPv += pv => firstInfo.TrySetResult(pv);

		// Start search and wait for first info
		await engine.StartSearchAsync(Fen.Default, null);
		var pv1 = await firstInfo.Task.WaitAsync(TimeSpan.FromSeconds(6));
		pv1.Should().NotBeNull();

		// New game should stop search and reset internal state
		await engine.NewGameAsync();

		var secondInfo =
			new TaskCompletionSource<PrincipalVariation?>(TaskCreationOptions.RunContinuationsAsynchronously);

		engine.InfoPv += pv => secondInfo.TrySetResult(pv);

		// Start search again and ensure we receive info
		await engine.StartSearchAsync(Fen.Default, null);
		var pv2 = await secondInfo.Task.WaitAsync(TimeSpan.FromSeconds(6));
		pv2.Should().NotBeNull();

		await engine.StopSearchAsync();
	}

	[Fact]
	public async Task SetOptionAsync_MultiPv2_EmitsMultipv2InInfoStream()
	{
		await using var engine = new PonderEngine(STOCKFISH_PATH);
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
		bool got = await sawMultiPv2.Task.WaitAsync(TimeSpan.FromSeconds(8));

		got.Should().BeTrue();

		await engine.StopSearchAsync();
	}

	[Fact]
	public async Task StartAsync_WhenCalled_StartsEngine()
	{
		var engine = new PonderEngine(STOCKFISH_PATH);

		await engine.StartAsync();

		engine.IsStarted.Should().BeTrue();
		engine.IsHealthy.Should().BeTrue();
		engine.Status.Should().Be(ProcessUciTransport.TransportStatus.Started);
	}

	[Fact]
	public async Task StartSearchAsync_RaisesInfo_And_ActivityTransitions()
	{
		await using var engine = new PonderEngine(STOCKFISH_PATH);
		await engine.StartAsync();

		var infoTcs = new TaskCompletionSource<PrincipalVariation?>(TaskCreationOptions.RunContinuationsAsynchronously);
		engine.InfoPv += pv => infoTcs.TrySetResult(pv);

		await engine.StartSearchAsync(Fen.Default, null);
		// Engine should report Searching activity at least at some point
		// Give it a brief moment to flip state in background
		await Task.Delay(100);
		engine.Activity.Should().Be(EngineActivity.Searching);

		var pv = await infoTcs.Task.WaitAsync(TimeSpan.FromSeconds(6));
		pv.Should().NotBeNull();

		await engine.StopSearchAsync();
		// Give it a moment to settle
		await Task.Delay(100);
		engine.Activity.Should().Be(EngineActivity.Idle);
	}

	[Fact]
	public async Task StartSearchAsync_ThenStopAsync_RaisesBestMove()
	{
		await using var engine = new PonderEngine(STOCKFISH_PATH);
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
		await tcs.Task.WaitAsync(TimeSpan.FromSeconds(5));
		await engine.StopAsync();

		best.Should().NotBeNull();
		UciEngineClient.IsUciMoveString(best!.Value.Raw).Should().BeTrue();
		if (ponder.HasValue)
			UciEngineClient.IsUciMoveString(ponder.Value.Raw).Should().BeTrue();
	}

	[Fact]
	public async Task StartSearchAsync_Twice_RemainsSearching()
	{
		await using var engine = new PonderEngine(STOCKFISH_PATH);
		await engine.StartAsync();

		await engine.StartSearchAsync(Fen.Default, null);
		// Allow activity set
		await Task.Delay(100);
		engine.Activity.Should().Be(EngineActivity.Searching);

		// Calling again with the same position should not throw and should remain searching
		await engine.StartSearchAsync(Fen.Default, null);
		await Task.Delay(100);
		engine.Activity.Should().Be(EngineActivity.Searching);

		await engine.StopSearchAsync();
	}

	[Fact]
	public async Task StartSearchAsync_WhenCalled_RaisesInfoPv()
	{
		await using var engine = new PonderEngine(STOCKFISH_PATH);
		await engine.StartAsync();
		var tcs = new TaskCompletionSource<PrincipalVariation?>(TaskCreationOptions.RunContinuationsAsynchronously);
		engine.InfoPv += pv => tcs.TrySetResult(pv);

		await engine.StartSearchAsync(Fen.Default, null);

		var received = await tcs.Task.WaitAsync(TimeSpan.FromSeconds(5));
		engine.Activity.Should().Be(EngineActivity.Searching);
		received?.Moves.Should().NotBeEmpty();
		received?.ScoreCp.Should().NotBeNull();
	}

	[Fact]
	public async Task StartSearchAsync_WithInvalidFen_ThrowsArgumentException()
	{
		await using var engine = new PonderEngine(STOCKFISH_PATH);
		await engine.StartAsync();

		await Assert.ThrowsAsync<ArgumentException>(() => engine.StartSearchAsync(Fen.Empty(), null));
	}

	[Fact]
	public async Task StopAsync_WhenCalled_StopsEngine()
	{
		await using var engine = new PonderEngine(STOCKFISH_PATH);
		await engine.StartAsync();

		await engine.StopAsync();

		engine.IsStarted.Should().BeFalse();
		engine.Status.Should().Be(ProcessUciTransport.TransportStatus.Stopped);
	}

	[Fact]
	public async Task StopSearchAsync_StopsSearch_Twice()
	{
		await using var engine = new PonderEngine(STOCKFISH_PATH);
		await engine.StartAsync();

		// First run
		await engine.StartSearchAsync(Fen.Default, null);
		await Task.Delay(150);
		await engine.StopSearchAsync();
		await Task.Delay(100);
		engine.Activity.Should().Be(EngineActivity.Idle);

		// Second run
		await engine.StartSearchAsync(Fen.Default, null);
		await Task.Delay(150);
		await engine.StopSearchAsync();
		await Task.Delay(100);
		engine.Activity.Should().Be(EngineActivity.Idle);
	}

	[Fact]
	public async Task StopSearchAsync_WhenCalled_StopsTheSearch()
	{
		await using var engine = new PonderEngine(STOCKFISH_PATH);
		await engine.StartAsync();

		await engine.StartSearchAsync(Fen.Default, null);
		await engine.StopSearchAsync();

		engine.Activity.Should().Be(EngineActivity.Idle);
	}
}
