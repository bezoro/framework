using Bezoro.Core.Common.Extensions;
using Bezoro.UCI.API.Types;
using Bezoro.UCI.Domain;
using Bezoro.UCI.Tests._Resources;
using FluentAssertions;
using JetBrains.Annotations;

namespace Bezoro.UCI.Tests.Domain;

[TestSubject(typeof(QuickInfoEngine))]
public class QuickInfoEngineTests
{
	[Fact]
	public async Task Dispose_WhenCalled_DisposesTheEngine()
	{
		var engine = new QuickInfoEngine(TestConsts.STOCKFISH_PATH);
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
		var engine = new QuickInfoEngine(TestConsts.STOCKFISH_PATH);
		await engine.StartAsync();

		await engine.DisposeAsync();

		engine.IsStarted.Should().BeFalse();
		engine.IsHealthy.Should().BeFalse();
		engine.Status.Should().Be(ProcessUciTransport.TransportStatus.Disposed);
	}


	[Fact]
	public async Task GetCurrentFenAsync_WhenCalled_GetsCurrentFen()
	{
		await using var engine = new QuickInfoEngine(TestConsts.STOCKFISH_PATH);
		await engine.StartAsync();

		var fen = await engine.GetCurrentFenAsync();

		fen.Should().NotBeNull();
		fen?.Raw.Should().Be(Fen.Default);
	}

	[Fact]
	public async Task GetLegalMovesAsync_WhenCalled_GetsLegalMoves()
	{
		await using var engine = new QuickInfoEngine(TestConsts.STOCKFISH_PATH);
		await engine.StartAsync();

		var moves = await engine.GetLegalMovesAsync();

		moves.Should().NotBeNull();
		moves.Count.Should().Be(20);
	}


	[Fact]
	public async Task QuickEvalAsync_WhenCalled_ReturnsValidSearchResult()
	{
		await using var engine = new QuickInfoEngine(TestConsts.STOCKFISH_PATH);
		await engine.StartAsync();
		var fen = await engine.GetCurrentFenAsync();
		fen.ThrowIfNull();

		var searchResult = await engine.QuickEvalAsync(fen.Value);

		searchResult.Should().NotBeNull();
		searchResult.BestMove.Should().NotBeNull();
		searchResult.BestCpScore.Should().NotBeNull();
	}

	[Fact]
	public async Task StartAsync_WhenCalled_StartsEngine()
	{
		await using var engine = new QuickInfoEngine(TestConsts.STOCKFISH_PATH);

		await engine.StartAsync();

		engine.IsStarted.Should().BeTrue();
		engine.IsHealthy.Should().BeTrue();
		engine.Status.Should().Be(ProcessUciTransport.TransportStatus.Started);
	}

	[Fact]
	public async Task StopAsync_WhenCalled_StopsEngine()
	{
		await using var engine = new QuickInfoEngine(TestConsts.STOCKFISH_PATH);
		await engine.StartAsync();

		await engine.StopAsync();

		engine.IsStarted.Should().BeFalse();
		engine.Status.Should().Be(ProcessUciTransport.TransportStatus.Stopped);
	}
}
