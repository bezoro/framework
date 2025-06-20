using Bezoro.Chess.Domain.Extensions;
using Bezoro.Chess.Domain.Functions.Moves;
using Bezoro.Chess.Domain.Helpers;
using Bezoro.Chess.Domain.Types.Records;
using Bezoro.Chess.Domain.Types.Structs;
using FluentAssertions;

namespace Bezoro.Chess.Tests.Unit;

public class QueenMoveGenerationUnitTests
{
	[Theory]
	[InlineData(PieceColor.White)]
	[InlineData(PieceColor.Black)]
	public void MoveGenerator_ForLoneQueenOnD4_ShouldGenerate27Moves(PieceColor color)
	{
		// Arrange
		var fromPosition = new Position("d4");
		var initialBoard = new Piece[8, 8];
		initialBoard[fromPosition.Row, fromPosition.Col] = new(PieceType.Queen, color);

		var gameState = new GameState
		{
			PiecePositions = initialBoard,
			ActiveColor    = color
		};

		// Act
		List<Move> moves = MoveGenerator.GeneratePieceMoves(fromPosition, gameState).ToList();

		// Assert
		// 14 rook moves + 13 bishop moves = 27
		moves.Should().HaveCount(27);
		// Rook moves
		moves.Select(m => m.To.ToString()).Should().Contain("d1");
		moves.Select(m => m.To.ToString()).Should().Contain("d8");
		moves.Select(m => m.To.ToString()).Should().Contain("a4");
		moves.Select(m => m.To.ToString()).Should().Contain("h4");
		// Bishop moves
		moves.Select(m => m.To.ToString()).Should().Contain("a1");
		moves.Select(m => m.To.ToString()).Should().Contain("h8");
		moves.Select(m => m.To.ToString()).Should().Contain("a7");
		moves.Select(m => m.To.ToString()).Should().Contain("g1");
	}

	[Theory]
	[InlineData(PieceColor.White)]
	[InlineData(PieceColor.Black)]
	public void MoveGenerator_ForQueenOnD4_WithBlockingAndCaptures_ShouldGenerateCorrectMoves(PieceColor color)
	{
		// Arrange
		var        fromPosition  = new Position("d4");
		PieceColor opponentColor = color.Opposite();

		var initialBoard = new Piece[8, 8];
		initialBoard[fromPosition.Row, fromPosition.Col] = new(PieceType.Queen, color);

		// Add pieces to block and be captured
		// Friendly (blocking)
		initialBoard[new Position("d6").Row, new Position("d6").Col] = new(PieceType.Pawn, color); // Up
		initialBoard[new Position("b4").Row, new Position("b4").Col] = new(PieceType.Pawn, color); // Left
		initialBoard[new Position("b6").Row, new Position("b6").Col] = new(PieceType.Pawn, color); // Up-Left
		initialBoard[new Position("f2").Row, new Position("f2").Col] = new(PieceType.Pawn, color); // Down-Right
		// Enemy (capturable)
		initialBoard[new Position("d2").Row, new Position("d2").Col] = new(PieceType.Pawn, opponentColor); // Down
		initialBoard[new Position("g4").Row, new Position("g4").Col] = new(PieceType.Pawn, opponentColor); // Right
		initialBoard[new Position("f6").Row, new Position("f6").Col] = new(PieceType.Pawn, opponentColor); // Up-Right
		initialBoard[new Position("b2").Row, new Position("b2").Col] = new(PieceType.Pawn, opponentColor); // Down-Left

		var gameState = new GameState
		{
			PiecePositions = initialBoard,
			ActiveColor    = color
		};

		// Act
		List<Move> moves = MoveGenerator.GeneratePieceMoves(fromPosition, gameState).ToList();

		// Assert
		// Expected moves:
		// Up: d5 -> 1
		// Down: d3, d2(C) -> 2
		// Left: c4 -> 1
		// Right: e4, f4, g4(C) -> 3
		// Up-Left: c5 -> 1
		// Up-Right: e5, f6(C) -> 2
		// Down-Left: c3, b2(C) -> 2
		// Down-Right: e3 -> 1
		// Total: 13 moves
		moves.Should().HaveCount(13);
		moves.Select(m => m.To.ToString()).Should().Contain("d2");    // Capture
		moves.Select(m => m.To.ToString()).Should().Contain("g4");    // Capture
		moves.Select(m => m.To.ToString()).Should().Contain("f6");    // Capture
		moves.Select(m => m.To.ToString()).Should().Contain("b2");    // Capture
		moves.Select(m => m.To.ToString()).Should().NotContain("d1"); // Blocked by capture
		moves.Select(m => m.To.ToString()).Should().NotContain("a4"); // Blocked by friendly
	}

	[Theory]
	[InlineData(PieceColor.White)]
	[InlineData(PieceColor.Black)]
	public void MoveGenerator_ForStandardStartingQueen_ShouldGenerateZeroMoves(PieceColor color)
	{
		// Arrange
		bool isWhite = color == PieceColor.White;
		// In a standard game setup, the queen is blocked by pawns.
		var       fromPosition = new Position(isWhite ? "d1" : "d8");
		GameState gameState    = BoardSetup.CreateStandardGame() with { ActiveColor = color };

		// Act
		List<Move> moves = MoveGenerator.GeneratePieceMoves(fromPosition, gameState).ToList();

		// Assert
		moves.Should().HaveCount(0);
	}
}
