using Bezoro.UCI.API.Types;
using FluentAssertions;
using JetBrains.Annotations;

namespace Bezoro.UCI.Tests;

[TestSubject(typeof(MoveClassificationEngine))]
public class MoveClassificationEngineTests
{
	public const string STOCKFISH_PATH = "Engine/stockfish/stockfish-windows-x86-64-avx2.exe";

	[Fact]
	public async Task ClassifyAsync_WhenCalled_ReturnsClassifiedMovesStream()
	{
		await using var engine = new MoveClassificationEngine(STOCKFISH_PATH);
		await engine.StartAsync();

		var moveStream = engine.ClassifyAsync(Fen.Default, BoardState.FromFen(Fen.Default)!.Value);

		moveStream.Should().NotBeNull();
		await foreach (var move in moveStream)
		{
			move.Analysis.Should().NotBeNull();
			move.Move.Should().NotBeNull();
			move.Score.Should().NotBeNull();
		}
	}

	[Fact]
	public async Task ClassifyAsync_WhenMateInOne_MoveIsFlaggedAsMateAndCheck()
	{
		// Position: Black king on h8, White queen on f7, White king on h6 (white to move).
		// Move f7g7 is checkmate.
		var fen   = Fen.Parse("7k/5Q2/7K/8/8/8/8/8 w - - 0 1");
		var board = BoardState.FromFen(fen!.Value)!.Value;

		await using var engine = new MoveClassificationEngine(STOCKFISH_PATH);
		await engine.StartAsync();

		var stream = engine.ClassifyAsync(fen.Value, board);

		var found = false;
		await foreach (var item in stream)
		{
			if (item.Analysis.IsCheck && item.Analysis.IsMate)
				found = true;
		}

		found.Should().BeTrue();
	}

	[Fact]
	public async Task ClassifyAsync_WhenStalemateInOne_MoveIsFlaggedAsStalemate()
	{
		// Position: Black king on a8, White queen on b7, White king on c7 (white to move).
		// Move b7b6 stalemates Black.
		var fen   = Fen.Parse("k7/1QK5/8/8/8/8/8/8 w - - 0 1");
		var board = BoardState.FromFen(fen!.Value)!.Value;

		await using var engine = new MoveClassificationEngine(STOCKFISH_PATH);
		await engine.StartAsync();

		var stream = engine.ClassifyAsync(fen.Value, board);

		var found = false;
		await foreach (var item in stream)
		{
			if (item.Analysis.IsStalemate)
				found = true;
		}

		found.Should().BeTrue("expected move b7b6 to be legal and classified for the given FEN");
	}
}
