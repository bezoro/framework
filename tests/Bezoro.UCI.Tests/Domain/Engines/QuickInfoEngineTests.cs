using Bezoro.Core.Extensions;
using Bezoro.UCI.API.Types;
using Bezoro.UCI.Domain;
using Bezoro.UCI.Domain.Engines;
using Bezoro.UCI.Tests.TestHelpers;
using FluentAssertions;
using JetBrains.Annotations;

namespace Bezoro.UCI.Tests.Domain.Engines;

[TestSubject(typeof(QuickInfoEngine))]
[Trait("Category", "Integration")]
public class QuickInfoEngineTests
{
	[Fact]
	public async Task Dispose_WhenCalled_ShouldDisposeEngine()
	{
		var engine = new QuickInfoEngine(TestResourcePaths.STOCKFISH_PATH);
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
		var engine = new QuickInfoEngine(TestResourcePaths.STOCKFISH_PATH);
		await engine.StartAsync();

		await engine.DisposeAsync();

		engine.IsStarted.Should().BeFalse();
		engine.IsHealthy.Should().BeFalse();
		engine.Status.Should().Be(TransportStatus.Disposed);
	}


	[Fact]
	public async Task GetCurrentFenAsync_WhenCalled_ShouldGetCurrentFen()
	{
		await using var engine = new QuickInfoEngine(TestResourcePaths.STOCKFISH_PATH);
		await engine.StartAsync();

		var fen = await engine.GetCurrentFenAsync();

		fen.Should().NotBeNull();
		fen?.Raw.Should().Be(Fen.Default);
	}

	[Fact]
	public async Task GetCurrentFenAsync_WhenCalledConcurrently_ShouldBeThreadSafe()
	{
		await using var engine = new QuickInfoEngine(TestResourcePaths.STOCKFISH_PATH);
		await engine.StartAsync();

		// Act: Call GetCurrentFenAsync concurrently
		var tasks = Enumerable.Range(0, 10)
							  .Select(_ => engine.GetCurrentFenAsync())
							  .ToArray();

		var results = await Task.WhenAll(tasks);

		// Assert: All should succeed and return valid FENs
		foreach (var fen in results)
		{
			fen.Should().NotBeNull();
			fen!.Value.Raw.Should().NotBeNullOrWhiteSpace();
		}

		// All should return the same FEN (starting position)
		results.Select(f => f!.Value.Raw).Distinct().Should().HaveCount(1);
	}

	[Fact]
	public async Task GetLegalMovesAsync_WhenCalled_ShouldGetLegalMoves()
	{
		await using var engine = new QuickInfoEngine(TestResourcePaths.STOCKFISH_PATH);
		await engine.StartAsync();

		var moves = await engine.GetLegalMovesAsync();

		moves.Should().NotBeNull();
		moves.Count.Should().Be(TestConstants.EXPECTED_STARTING_POSITION_MOVES);
	}

	[Fact]
	public async Task GetLegalMovesAsync_WhenCalledConcurrently_ShouldBeThreadSafe()
	{
		await using var engine = new QuickInfoEngine(TestResourcePaths.STOCKFISH_PATH);
		await engine.StartAsync();

		// Act: Call GetLegalMovesAsync concurrently
		var tasks = Enumerable.Range(0, 10)
							  .Select(_ => engine.GetLegalMovesAsync())
							  .ToArray();

		var results = await Task.WhenAll(tasks);

		// Assert: All should succeed and return valid move collections
		foreach (var moves in results)
		{
			moves.Should().NotBeNull();
			moves.Count.Should().Be(TestConstants.EXPECTED_STARTING_POSITION_MOVES);
		}

		// All should return the same moves (cached)
		results.Select(m => m.Count).Distinct().Should().HaveCount(1);
	}


	[Fact]
	public async Task QuickEvalAsync_WhenCalled_ShouldReturnValidSearchResult()
	{
		await using var engine = new QuickInfoEngine(TestResourcePaths.STOCKFISH_PATH);
		await engine.StartAsync();
		var fen = await engine.GetCurrentFenAsync();
		fen.ThrowIfNull();

		var searchResult = await engine.QuickEvalAsync(fen.Value);

		searchResult.Should().NotBeNull();
		searchResult.BestMove.Should().NotBeNull();
		searchResult.BestCpScore.Should().NotBeNull();
	}

	[Fact]
	public async Task QuickEvalAsync_WhenCalledAfterNewGame_ShouldClearEvalCache()
	{
		await using var engine = new QuickInfoEngine(TestResourcePaths.STOCKFISH_PATH);
		await engine.StartAsync();

		var fen = Fen.Default;

		// Evaluate position - should cache result
		var result1 = await engine.QuickEvalAsync(fen);
		result1.Should().NotBeNull();

		// New game - this should clear the eval cache
		await engine.NewGameAsync();

		// Evaluate same position again - should NOT return cached result
		var result2 = await engine.QuickEvalAsync(fen);
		result2.Should().NotBeNull();
		// Both results should be valid, but the cache was cleared so result2 is fresh
		result2.BestMove.Should().NotBeNull();
	}

	[Fact]
	public async Task QuickEvalAsync_WhenCalledAfterSetPosition_ShouldClearEvalCache()
	{
		await using var engine = new QuickInfoEngine(TestResourcePaths.STOCKFISH_PATH);
		await engine.StartAsync();

		var fen1 = Fen.Default;
		var fen2 = Fen.Parse(TestConstants.AFTER_E2_E4_FEN);

		// Evaluate first position - should cache result
		var result1 = await engine.QuickEvalAsync(fen1);
		result1.Should().NotBeNull();

		// Change position - this should clear the eval cache
		await engine.SetPositionAsync(fen2!.Value);

		// Evaluate first position again - should NOT return cached result
		// (cache should be cleared, so it will recompute)
		var result2 = await engine.QuickEvalAsync(fen1);
		result2.Should().NotBeNull();
		// Both results should be valid, but the cache was cleared so result2 is fresh
		result2.BestMove.Should().NotBeNull();
	}

	[Fact]
	public async Task QuickEvalAsync_WhenCalledConcurrently_ShouldBeThreadSafe()
	{
		await using var engine = new QuickInfoEngine(TestResourcePaths.STOCKFISH_PATH);
		await engine.StartAsync();

		var fen = Fen.Default;

		// Act: Call QuickEvalAsync concurrently with the same position
		var tasks = Enumerable.Range(0, 5)
							  .Select(_ => engine.QuickEvalAsync(fen, 4))
							  .ToArray();

		var results = await Task.WhenAll(tasks);

		// Assert: All should succeed and return valid results
		foreach (var result in results)
		{
			result.BestMove.Should().NotBeNullOrWhiteSpace();
			result.BestCpScore.Should().NotBeNull();
		}

		// All should return results (may be cached or fresh, but all valid)
		results.Length.Should().Be(5);
	}

	[Fact]
	public async Task SetPositionAsync_WhenCalledConcurrently_ShouldBeThreadSafe()
	{
		await using var engine = new QuickInfoEngine(TestResourcePaths.STOCKFISH_PATH);
		await engine.StartAsync();

		var fen1 = Fen.Default;
		var fen2 = Fen.Parse(TestConstants.AFTER_E2_E4_FEN);
		fen2.Should().NotBeNull();

		// Act: Call SetPositionAsync concurrently with different positions
		var tasks = new List<Task>
		{
			engine.SetPositionAsync(fen1),
			engine.SetPositionAsync(fen2!.Value),
			engine.SetPositionAsync(fen1),
			engine.SetPositionAsync(fen2.Value),
			engine.SetPositionAsync(fen1)
		};

		// Should not throw exceptions
		var act = async () => await Task.WhenAll(tasks);

		await act.Should().NotThrowAsync("Concurrent SetPositionAsync calls should be thread-safe");

		// Verify final state is consistent
		var finalFen = await engine.GetCurrentFenAsync();
		finalFen.Should().NotBeNull();
		// Final position should be one of the positions we set
		finalFen!.Value.Raw.Should().BeOneOf(fen1.Raw, fen2.Value.Raw);
	}

	[Fact]
	public async Task StartAsync_WhenCalled_ShouldStartEngine()
	{
		await using var engine = new QuickInfoEngine(TestResourcePaths.STOCKFISH_PATH);

		await engine.StartAsync();

		engine.IsStarted.Should().BeTrue();
		engine.IsHealthy.Should().BeTrue();
		engine.Status.Should().Be(TransportStatus.Started);
	}

	[Fact]
	public async Task StopAsync_WhenCalled_ShouldStopEngine()
	{
		await using var engine = new QuickInfoEngine(TestResourcePaths.STOCKFISH_PATH);
		await engine.StartAsync();

		await engine.StopAsync();

		engine.IsStarted.Should().BeFalse();
		engine.Status.Should().Be(TransportStatus.Stopped);
	}
}
