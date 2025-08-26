using Bezoro.UCI.API.Types;
using Bezoro.UCI.Domain;
using FluentAssertions;
using JetBrains.Annotations;

namespace Bezoro.UCI.Tests.Integration;

[TestSubject(typeof(MoveClassificationEngine))]
[Trait("Category", "Integration")]
public class MoveClassificationEngineIntegrationTests
{
	public const string STOCKFISH_PATH = "Engine/stockfish/stockfish-windows-x86-64-avx2.exe";

	[Fact]
	public async Task Classify_FullTurn_WhiteThenBlack_WorksForBothSides()
	{
		var start = Fen.Default;

		await using var engine = new MoveClassificationEngine(STOCKFISH_PATH);
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

		await using var engine = new MoveClassificationEngine(STOCKFISH_PATH);
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

		await using var engine = new MoveClassificationEngine(STOCKFISH_PATH);
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

		moveCount.Should().Be(20);
	}

	[Fact]
	public async Task ClassifyAsync_WhenMateInOne_MoveIsFlaggedAsMateAndCheck()
	{
		// Position: Black king on h8, White queen on f7, White king on h6 (white to move).
		// Move f7g7 is checkmate.
		var fen = Fen.Parse("7k/5Q2/7K/8/8/8/8/8 w - - 0 1");

		await using var engine = new MoveClassificationEngine(STOCKFISH_PATH);
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

		await using var engine = new MoveClassificationEngine(STOCKFISH_PATH);
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
	public async Task ClassifyMoveAsync_WhenIllegalMove_Throws()
	{
		// Starting position: "e2e5" is illegal.
		var fen = Fen.Default;

		await using var engine = new MoveClassificationEngine(STOCKFISH_PATH);
		await engine.StartAsync();

		await Assert.ThrowsAsync<ArgumentException>(() => engine.ClassifyMoveAsync(fen, "e2e5"));
	}

	[Fact]
	public async Task ClassifyMoveAsync_WhenLegalMoveFromStart_ReturnsResult()
	{
		// Starting position: "e2e4" is legal.
		var fen = Fen.Default;

		await using var engine = new MoveClassificationEngine(STOCKFISH_PATH);
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

		await using var engine = new MoveClassificationEngine(STOCKFISH_PATH);
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

		await using var engine = new MoveClassificationEngine(STOCKFISH_PATH);
		await engine.StartAsync();

		var result = await engine.ClassifyMoveAsync(fen.Value, "b7b6");

		result.HasValue.Should().BeTrue();
		var move = result.Value;
		move.Notation.Should().Be("b7b6");
		move.Analysis.IsStalemate.Should().BeTrue();
	}

	[Fact]
	public async Task IsCheckmateAsync_WhenMateInOne_ReturnsTrue()
	{
		// Position: Black king on h8, White queen on f7, White king on h6 (white to move).
		// Move f7g7 is checkmate.
		var fen = Fen.Parse("7k/5Q2/7K/8/8/8/8/8 w - - 0 1");

		await using var engine = new MoveClassificationEngine(STOCKFISH_PATH);
		await engine.StartAsync();

		bool isMate = await engine.IsCheckmateAsync(fen!.Value, "f7g7");

		isMate.Should().BeTrue();
	}

	[Fact]
	public async Task IsCheckmateAsync_WhenNotMate_ReturnsFalse()
	{
		var fen = Fen.Default;

		await using var engine = new MoveClassificationEngine(STOCKFISH_PATH);
		await engine.StartAsync();

		bool isMate = await engine.IsCheckmateAsync(fen, "e2e4");

		isMate.Should().BeFalse();
	}

	[Fact]
	public async Task IsStalemateAsync_WhenNotStalemate_ReturnsFalse()
	{
		var fen = Fen.Default;

		await using var engine = new MoveClassificationEngine(STOCKFISH_PATH);
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

		await using var engine = new MoveClassificationEngine(STOCKFISH_PATH);
		await engine.StartAsync();

		bool isStalemate = await engine.IsStalemateAsync(fen!.Value, "b7b6");

		isStalemate.Should().BeTrue();
	}
}
