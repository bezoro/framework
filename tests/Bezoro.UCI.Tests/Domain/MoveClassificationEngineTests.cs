using Bezoro.UCI.API.Types;
using Bezoro.UCI.Domain.Engines;
using Bezoro.UCI.Tests.TestHelpers;
using FluentAssertions;
using JetBrains.Annotations;

namespace Bezoro.UCI.Tests.Domain;

[TestSubject(typeof(MoveClassificationEngine))]
public class MoveClassificationEngineTests
{
	[Fact]
	public async Task Classify_FullTurn_WhiteThenBlack_WorksForBothSides()
	{
		var start = Fen.Default;

		await using var engine = new MoveClassificationEngine(TestResourcePaths.StockfishPath);
		await engine.StartAsync();

		// White move
		var white = await engine.ClassifyMoveAsync(start, "e2e4");
		white.HasValue.Should().BeTrue();
		white.Value.Notation.Should().Be("e2e4");

		// Position after e2e4 (black to move)
		var afterE4 = Fen.Parse("rnbqkbnr/pppppppp/8/8/4P3/8/PPPP1PPP/RNBQKBNR b KQkq e3 0 1");

		// Classification stream for black should include a known reply (e7e5 or c7c5)
		var stream        = engine.ClassifyAsync(afterE4!.Value);
		var hasKnownReply = false;
		await foreach (var m in stream)
		{
			if (m.Notation != "e7e5" && m.Notation != "c7c5") continue;

			hasKnownReply = true;
			break;
		}

		hasKnownReply.Should().BeTrue();

		// Classify a specific black move to complete the full turn
		var black = await engine.ClassifyMoveAsync(afterE4.Value, "e7e5");
		black.HasValue.Should().BeTrue();
		black.Value.Notation.Should().Be("e7e5");
	}

	[Fact]
	public async Task ClassifyAsync_FromStartPosition_ContainsKnownLegalMoveE2E4()
	{
		var fen = Fen.Default;

		await using var engine = new MoveClassificationEngine(TestResourcePaths.StockfishPath);
		await engine.StartAsync();

		var stream = engine.ClassifyAsync(fen);

		var found = false;
		await foreach (var item in stream)
		{
			if (item.Notation == "e2e4")
			{
				found = true;
				break;
			}
		}

		found.Should().BeTrue("the classification stream should include known legal moves from the starting position");
	}

	[Fact]
	public async Task ClassifyAsync_WhenCalled_ReturnsClassifiedMovesStream()
	{
		var fen = Fen.Default;

		await using var engine = new MoveClassificationEngine(TestResourcePaths.StockfishPath);
		await engine.StartAsync();

		var moveStream = engine.ClassifyAsync(fen);

		moveStream.Should().NotBeNull();
		var moveCount = 0;
		await foreach (var move in moveStream)
		{
			move.Should().NotBeNull();
			move.Notation.Should().NotBeNull();
			move.Analysis.Should().NotBeNull();
			move.Analysis.Score.Should().NotBeNull();
			moveCount++;
		}

		moveCount.Should().Be(TestConstants.EXPECTED_STARTING_POSITION_MOVES);
	}

	[Fact]
	public async Task ClassifyAsync_WhenFenIsInvalid_ThrowsArgumentException()
	{
		await using var engine = new MoveClassificationEngine(TestResourcePaths.StockfishPath);
		await engine.StartAsync();

		await using var enumerator = engine.ClassifyAsync(Fen.Empty()).GetAsyncEnumerator();

		await Assert.ThrowsAsync<ArgumentException>(async () => await enumerator.MoveNextAsync());
	}

	[Fact]
	public async Task ClassifyAsync_WhenMateInOne_MoveIsFlaggedAsMateAndCheck()
	{
		// Position: Black king on h8, White queen on f7, White king on h6 (white to move).
		// Move f7g7 is checkmate.
		var fen = Fen.Parse("7k/5Q2/7K/8/8/8/8/8 w - - 0 1");

		await using var engine = new MoveClassificationEngine(TestResourcePaths.StockfishPath);
		await engine.StartAsync();

		var stream = engine.ClassifyAsync(fen.Value);

		var found = false;
		await foreach (var item in stream)
		{
			if (!item.Analysis.IsCheck || !item.Analysis.IsMate) continue;

			found = true;
			break;
		}

		found.Should().BeTrue();
	}

	[Fact]
	public async Task ClassifyAsync_WhenStalemateInOne_MoveIsFlaggedAsStalemate()
	{
		// Position: Black king on a8, White queen on b7, White king on c7 (white to move).
		// Move b7b6 stalemates Black.
		var fen = Fen.Parse("k7/1QK5/8/8/8/8/8/8 w - - 0 1");

		await using var engine = new MoveClassificationEngine(TestResourcePaths.StockfishPath);
		await engine.StartAsync();

		var stream = engine.ClassifyAsync(fen!.Value);

		var found = false;
		await foreach (var item in stream)
		{
			if (!item.Analysis.IsStalemate) continue;

			found = true;
			break;
		}

		found.Should().BeTrue();
	}

	[Fact]
	public async Task ClassifyAsync_WhileIsCheckmateAsyncRunning_NoInterference()
	{
		var fen = Fen.Parse(TestConstants.WHITE_MATE_IN_ONE_FEN);
		fen.Should().NotBeNull();

		await using var engine = new MoveClassificationEngine(TestResourcePaths.StockfishPath);
		await engine.StartAsync();

		// Act: Start classification stream and concurrently check for mate
		var classificationTask = Task.Run(async () =>
			{
				var count = 0;
				await foreach (var move in engine.ClassifyAsync(fen!.Value))
				{
					count++;
					if (count >= 5) break; // Limit to avoid long test
				}

				return count;
			}
		);

		// Start checkmate check concurrently - it should work correctly even with classification running
		var checkmateTask = engine.IsCheckmateAsync(fen.Value, "f7g7");

		await Task.WhenAll(classificationTask, checkmateTask);

		// Assert: Both operations should complete successfully
		classificationTask.IsCompletedSuccessfully.Should().BeTrue();
		checkmateTask.IsCompletedSuccessfully.Should().BeTrue();

		// Verify checkmate result is correct (position lock ensures no interference)
		bool isMate = await checkmateTask;
		isMate.Should().BeTrue("Checkmate check should work correctly even with concurrent classification");

		// Verify we can still check mate after classification completes (position should be restored)
		bool isMateAfter = await engine.IsCheckmateAsync(fen.Value, "f7g7");
		isMateAfter.Should().BeTrue("Checkmate check should still work after classification completes");
	}

	[Fact]
	public async Task ClassifyMoveAsync_WhenCalledConcurrently_ThreadSafe()
	{
		var fen = Fen.Default;

		await using var engine = new MoveClassificationEngine(TestResourcePaths.StockfishPath);
		await engine.StartAsync();

		// Act: Call ClassifyMoveAsync concurrently with different moves
		var tasks = new[]
		{
			engine.ClassifyMoveAsync(fen, "e2e4"),
			engine.ClassifyMoveAsync(fen, "d2d4"),
			engine.ClassifyMoveAsync(fen, "g1f3"),
			engine.ClassifyMoveAsync(fen, "c2c4"),
			engine.ClassifyMoveAsync(fen, "b1c3")
		};

		var results = await Task.WhenAll(tasks);

		// Assert: All should succeed and return valid results
		foreach (var result in results)
		{
			result.HasValue.Should().BeTrue();
			result!.Value.Notation.Should().NotBeNullOrWhiteSpace();
			result.Value.Analysis.Should().NotBeNull();
		}
	}

	[Fact]
	public async Task ClassifyMoveAsync_WhenFenIsInvalid_ThrowsArgumentException()
	{
		await using var engine = new MoveClassificationEngine(TestResourcePaths.StockfishPath);
		await engine.StartAsync();

		await Assert.ThrowsAsync<ArgumentException>(() => engine.ClassifyMoveAsync(Fen.Empty(), "e2e4"));
	}

	[Fact]
	public async Task ClassifyMoveAsync_WhenIllegalMove_Throws()
	{
		// Starting position: "e2e5" is illegal.
		var fen = Fen.Default;

		await using var engine = new MoveClassificationEngine(TestResourcePaths.StockfishPath);
		await engine.StartAsync();

		await Assert.ThrowsAsync<ArgumentException>(() => engine.ClassifyMoveAsync(fen, "e2e5"));
	}

	[Fact]
	public async Task ClassifyMoveAsync_WhenLegalMoveFromStart_ReturnsResult()
	{
		// Starting position: "e2e4" is legal.
		var fen = Fen.Default;

		await using var engine = new MoveClassificationEngine(TestResourcePaths.StockfishPath);
		await engine.StartAsync();

		var result = await engine.ClassifyMoveAsync(fen, "e2e4");

		result.HasValue.Should().BeTrue();
		var move = result.Value;
		move.Notation.Should().Be("e2e4");
		move.Analysis.Should().NotBeNull();
		move.Analysis.Score.Should().NotBeNull();
	}

	[Fact]
	public async Task ClassifyMoveAsync_WhenMateInOne_MoveIsFlaggedAsMateAndCheck()
	{
		// Position: Black king on h8, White queen on f7, White king on h6 (white to move).
		// Move f7g7 is checkmate.
		var fen = Fen.Parse("7k/5Q2/7K/8/8/8/8/8 w - - 0 1");

		await using var engine = new MoveClassificationEngine(TestResourcePaths.StockfishPath);
		await engine.StartAsync();

		var result = await engine.ClassifyMoveAsync(fen.Value, "f7g7");

		result.HasValue.Should().BeTrue();
		var move = result.Value;
		move.Notation.Should().Be("f7g7");
		move.Analysis.IsCheck.Should().BeTrue();
		move.Analysis.IsMate.Should().BeTrue();
	}

	[Fact]
	public async Task ClassifyMoveAsync_WhenStalemateInOne_MoveIsFlaggedAsStalemate()
	{
		// Position: Black king on a8, White queen on b7, White king on c7 (white to move).
		// Move b7b6 stalemates Black.
		var fen = Fen.Parse("k7/1QK5/8/8/8/8/8/8 w - - 0 1");

		await using var engine = new MoveClassificationEngine(TestResourcePaths.StockfishPath);
		await engine.StartAsync();

		var result = await engine.ClassifyMoveAsync(fen.Value, "b7b6");

		result.HasValue.Should().BeTrue();
		var move = result.Value;
		move.Notation.Should().Be("b7b6");
		move.Analysis.IsStalemate.Should().BeTrue();
	}

	[Fact]
	public async Task IsCheckmateAsync_AfterConcurrentOperations_PositionRestored()
	{
		var fen1 = Fen.Default;
		var fen2 = Fen.Parse(TestConstants.WHITE_MATE_IN_ONE_FEN);
		fen2.Should().NotBeNull();

		await using var engine = new MoveClassificationEngine(TestResourcePaths.StockfishPath);
		await engine.StartAsync();

		// Act: Perform concurrent operations that modify position
		var tasks = new Task[]
		{
			engine.IsCheckmateAsync(fen1,        "e2e4"), // Not mate
			engine.IsCheckmateAsync(fen2!.Value, "f7g7"), // Mate
			engine.IsStalemateAsync(fen1, "e2e4"),        // Not stalemate
			engine.ClassifyMoveAsync(fen1, "d2d4")
		};

		await Task.WhenAll(tasks);

		// Assert: After concurrent operations, verify position state is consistent
		// by checking that we can still perform operations correctly
		bool result1 = await engine.IsCheckmateAsync(fen1, "e2e4");
		result1.Should().BeFalse("Position should be correctly restored");

		bool result2 = await engine.IsCheckmateAsync(fen2.Value, "f7g7");
		result2.Should().BeTrue("Position should be correctly restored");
	}

	[Fact]
	public async Task IsCheckmateAsync_WhenCalledConcurrently_ThreadSafe()
	{
		var fen = Fen.Parse(TestConstants.WHITE_MATE_IN_ONE_FEN);
		fen.Should().NotBeNull();

		await using var engine = new MoveClassificationEngine(TestResourcePaths.StockfishPath);
		await engine.StartAsync();

		// Act: Call IsCheckmateAsync concurrently with different moves
		var tasks = new[]
		{
			engine.IsCheckmateAsync(fen!.Value, "f7g7"), // Mate move
			engine.IsCheckmateAsync(fen.Value,  "f7g7"),
			engine.IsCheckmateAsync(fen.Value,  "f7g7"),
			engine.IsCheckmateAsync(fen.Value,  "f7g7"),
			engine.IsCheckmateAsync(fen.Value,  "f7g7")
		};

		bool[] results = await Task.WhenAll(tasks);

		// Assert: All should succeed and return consistent results
		foreach (bool isMate in results) isMate.Should().BeTrue();

		// All should return the same result (cached)
		results.Distinct().Should().HaveCount(1);
	}

	[Fact]
	public async Task IsCheckmateAsync_WhenMateInOne_ReturnsTrue()
	{
		// Position: Black king on h8, White queen on f7, White king on h6 (white to move).
		// Move f7g7 is checkmate.
		var fen = Fen.Parse("7k/5Q2/7K/8/8/8/8/8 w - - 0 1");

		await using var engine = new MoveClassificationEngine(TestResourcePaths.StockfishPath);
		await engine.StartAsync();

		bool isMate = await engine.IsCheckmateAsync(fen!.Value, "f7g7");

		isMate.Should().BeTrue();
	}

	[Fact]
	public async Task IsCheckmateAsync_WhenNotMate_ReturnsFalse()
	{
		var fen = Fen.Default;

		await using var engine = new MoveClassificationEngine(TestResourcePaths.StockfishPath);
		await engine.StartAsync();

		bool isMate = await engine.IsCheckmateAsync(fen, "e2e4");

		isMate.Should().BeFalse();
	}

	[Fact]
	public async Task IsStalemateAsync_WhenCalledConcurrently_ThreadSafe()
	{
		var fen = Fen.Parse(TestConstants.STALEMATE_FEN);
		fen.Should().NotBeNull();

		await using var engine = new MoveClassificationEngine(TestResourcePaths.StockfishPath);
		await engine.StartAsync();

		// Act: Call IsStalemateAsync concurrently
		var tasks = new[]
		{
			engine.IsStalemateAsync(fen!.Value, "b7b6"), // Stalemate move
			engine.IsStalemateAsync(fen.Value,  "b7b6"),
			engine.IsStalemateAsync(fen.Value,  "b7b6"),
			engine.IsStalemateAsync(fen.Value,  "b7b6"),
			engine.IsStalemateAsync(fen.Value,  "b7b6")
		};

		bool[] results = await Task.WhenAll(tasks);

		// Assert: All should succeed and return consistent results
		foreach (bool isStalemate in results) isStalemate.Should().BeTrue();

		// All should return the same result (cached)
		results.Distinct().Should().HaveCount(1);
	}

	[Fact]
	public async Task IsStalemateAsync_WhenNotStalemate_ReturnsFalse()
	{
		var fen = Fen.Default;

		await using var engine = new MoveClassificationEngine(TestResourcePaths.StockfishPath);
		await engine.StartAsync();

		bool isStalemate = await engine.IsStalemateAsync(fen, "e2e4");

		isStalemate.Should().BeFalse();
	}

	[Fact]
	public async Task IsStalemateAsync_WhenStalemateInOne_ReturnsTrue()
	{
		// Position: Black king on a8, White queen on b7, White king on c7 (white to move).
		// Move b7b6 stalemates Black.
		var fen = Fen.Parse("k7/1QK5/8/8/8/8/8/8 w - - 0 1");

		await using var engine = new MoveClassificationEngine(TestResourcePaths.StockfishPath);
		await engine.StartAsync();

		bool isStalemate = await engine.IsStalemateAsync(fen!.Value, "b7b6");

		isStalemate.Should().BeTrue();
	}
}
