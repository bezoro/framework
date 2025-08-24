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
}
