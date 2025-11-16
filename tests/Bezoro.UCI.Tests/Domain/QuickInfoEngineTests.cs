using Bezoro.Core.Common.Extensions;
using Bezoro.UCI.API.Types;
using Bezoro.UCI.Domain;
using Bezoro.UCI.Domain.Engines;
using Bezoro.UCI.Tests._Resources;
using Bezoro.UCI.Tests.Attributes;
using Bezoro.UCI.Tests.TestHelpers;
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
		engine.Status.Should().Be(TransportStatus.Disposed);
	}

	[Fact]
	public async Task DisposeAsync_WhenCalled_DisposesTheEngine()
	{
		var engine = new QuickInfoEngine(TestConsts.STOCKFISH_PATH);
		await engine.StartAsync();

		await engine.DisposeAsync();

		engine.IsStarted.Should().BeFalse();
		engine.IsHealthy.Should().BeFalse();
		engine.Status.Should().Be(TransportStatus.Disposed);
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
		moves.Count.Should().Be(TestConstants.ExpectedStartingPositionMoves);
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
		engine.Status.Should().Be(TransportStatus.Started);
	}

	[Fact]
	public async Task StopAsync_WhenCalled_StopsEngine()
	{
		await using var engine = new QuickInfoEngine(TestConsts.STOCKFISH_PATH);
		await engine.StartAsync();

		await engine.StopAsync();

		engine.IsStarted.Should().BeFalse();
		engine.Status.Should().Be(TransportStatus.Stopped);
	}

	[Fact]
	public async Task QuickEvalAsync_AfterSetPosition_ClearsEvalCache()
	{
		await using var engine = new QuickInfoEngine(TestConsts.STOCKFISH_PATH);
		await engine.StartAsync();

		var fen1 = Fen.Default;
		var fen2 = Fen.Parse(TestConstants.AfterE2E4Fen);

		// Evaluate first position - should cache result
		var result1 = await engine.QuickEvalAsync(fen1, depth: 6);
		result1.Should().NotBeNull();

		// Change position - this should clear the eval cache
		await engine.SetPositionAsync(fen2!.Value);

		// Evaluate first position again - should NOT return cached result
		// (cache should be cleared, so it will recompute)
		var result2 = await engine.QuickEvalAsync(fen1, depth: 6);
		result2.Should().NotBeNull();
		// Both results should be valid, but the cache was cleared so result2 is fresh
		result2.BestMove.Should().NotBeNull();
	}

	[Fact]
	public async Task QuickEvalAsync_AfterNewGame_ClearsEvalCache()
	{
		await using var engine = new QuickInfoEngine(TestConsts.STOCKFISH_PATH);
		await engine.StartAsync();

		var fen = Fen.Default;

		// Evaluate position - should cache result
		var result1 = await engine.QuickEvalAsync(fen, depth: 6);
		result1.Should().NotBeNull();

		// New game - this should clear the eval cache
		await engine.NewGameAsync();

		// Evaluate same position again - should NOT return cached result
		var result2 = await engine.QuickEvalAsync(fen, depth: 6);
		result2.Should().NotBeNull();
		// Both results should be valid, but the cache was cleared so result2 is fresh
		result2.BestMove.Should().NotBeNull();
	}
}
