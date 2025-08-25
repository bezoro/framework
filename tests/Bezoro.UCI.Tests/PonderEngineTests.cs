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
	public async Task StartAsync_WhenCalled_StartsEngine()
	{
		var engine = new PonderEngine(STOCKFISH_PATH);

		await engine.StartAsync();

		engine.IsStarted.Should().BeTrue();
		engine.IsHealthy.Should().BeTrue();
		engine.Status.Should().Be(ProcessUciTransport.TransportStatus.Started);
	}

	[Fact]
	public async Task StartPonderAsync_ThenStopPonderAsync_RaisesBestMove()
	{
		await using var engine = new PonderEngine(STOCKFISH_PATH);
		await engine.StartAsync();

		string? best   = null;
		string? ponder = null;
		var     tcs    = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
		engine.BestMove += (b, p) =>
		{
			best   = b;
			ponder = p;
			tcs.TrySetResult(true);
		};

		await engine.StartPonderAsync(Fen.Default, null);
		await engine.StopPonderAsync();

		await tcs.Task.WaitAsync(TimeSpan.FromSeconds(5));

		best.Should().NotBeNullOrWhiteSpace();
		UciEngineClient.IsUciMoveString(best!).Should().BeTrue();
		if (!string.IsNullOrWhiteSpace(ponder))
			UciEngineClient.IsUciMoveString(ponder!).Should().BeTrue();
	}

	[Fact]
	public async Task StartPonderAsync_WhenCalled_RaisesInfoPv()
	{
		await using var engine = new PonderEngine(STOCKFISH_PATH);
		await engine.StartAsync();
		var tcs = new TaskCompletionSource<PrincipalVariation?>(TaskCreationOptions.RunContinuationsAsynchronously);
		engine.InfoPv += pv => tcs.TrySetResult(pv);

		await engine.StartPonderAsync(Fen.Default, null);

		var received = await tcs.Task.WaitAsync(TimeSpan.FromSeconds(3));
		engine.Activity.Should().Be(EngineActivity.Pondering);
		received?.Moves.Should().NotBeEmpty();
		received?.ScoreCp.Should().NotBeNull();
	}

	[Fact]
	public async Task StartPonderAsync_WithInvalidFen_ThrowsArgumentException()
	{
		await using var engine = new PonderEngine(STOCKFISH_PATH);
		await engine.StartAsync();

		await Assert.ThrowsAsync<ArgumentException>(() => engine.StartPonderAsync(Fen.Empty(), null));
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
	public async Task StopPonderAsync_WhenCalled_StopsTheSearch()
	{
		await using var engine = new PonderEngine(STOCKFISH_PATH);
		await engine.StartAsync();

		await engine.StartPonderAsync(Fen.Default, null);
		await engine.StopPonderAsync();

		engine.Activity.Should().Be(EngineActivity.Idle);
	}

	[Fact]
	public async Task NewGameAsync_WhilePondering_StopsSearch_And_AllowsRestart()
	{
		await using var engine = new PonderEngine(STOCKFISH_PATH);
		await engine.StartAsync();

		var firstInfo = new TaskCompletionSource<PrincipalVariation?>(TaskCreationOptions.RunContinuationsAsynchronously);
		engine.InfoPv += pv => firstInfo.TrySetResult(pv);

		// Start pondering and wait for first info
		await engine.StartPonderAsync(Fen.Default, null);
		var pv1 = await firstInfo.Task.WaitAsync(TimeSpan.FromSeconds(6));
		pv1.Should().NotBeNull();

		// New game should stop search and reset internal state
		await engine.NewGameAsync();

		var secondInfo = new TaskCompletionSource<PrincipalVariation?>(TaskCreationOptions.RunContinuationsAsynchronously);
		engine.InfoPv += pv => secondInfo.TrySetResult(pv);

		// Start pondering again and ensure we receive info
		await engine.StartPonderAsync(Fen.Default, null);
		var pv2 = await secondInfo.Task.WaitAsync(TimeSpan.FromSeconds(6));
		pv2.Should().NotBeNull();

		await engine.StopPonderAsync();
	}

	[Fact]
	public async Task StartSearchAsync_BestMode_RaisesInfo_And_ActivityTransitions()
	{
		await using var engine = new PonderEngine(STOCKFISH_PATH);
		await engine.StartAsync();

		var infoTcs = new TaskCompletionSource<PrincipalVariation?>(TaskCreationOptions.RunContinuationsAsynchronously);
		engine.InfoPv += pv => infoTcs.TrySetResult(pv);

		await engine.StartSearchAsync(Fen.Default, null, ponder: false);
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
	public async Task StopSearchAsync_StopsBothPonder_And_Best()
	{
		await using var engine = new PonderEngine(STOCKFISH_PATH);
		await engine.StartAsync();

		// Ponder mode
		await engine.StartPonderAsync(Fen.Default, null);
		await Task.Delay(150);
		await engine.StopSearchAsync();
		await Task.Delay(100);
		engine.Activity.Should().Be(EngineActivity.Idle);

		// Best (non-ponder) mode
		await engine.StartSearchAsync(Fen.Default, null, ponder: false);
		await Task.Delay(150);
		await engine.StopSearchAsync();
		await Task.Delay(100);
		engine.Activity.Should().Be(EngineActivity.Idle);
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

		await engine.StartSearchAsync(Fen.Default, null, ponder: false);
		var got = await sawMultiPv2.Task.WaitAsync(TimeSpan.FromSeconds(8));

		got.Should().BeTrue();

		await engine.StopSearchAsync();
	}

	[Fact]
	public async Task StartPonderAsync_SamePosition_Twice_IsNoOpAndRemainsPondering()
	{
		await using var engine = new PonderEngine(STOCKFISH_PATH);
		await engine.StartAsync();

		await engine.StartPonderAsync(Fen.Default, null);
		// Allow activity set
		await Task.Delay(100);
		engine.Activity.Should().Be(EngineActivity.Pondering);

		// Calling again with the same position should not throw and should remain pondering
		await engine.StartPonderAsync(Fen.Default, null);
		await Task.Delay(100);
		engine.Activity.Should().Be(EngineActivity.Pondering);

		await engine.StopPonderAsync();
	}
}
