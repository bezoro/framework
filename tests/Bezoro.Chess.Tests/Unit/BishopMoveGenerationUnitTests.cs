using Bezoro.Chess.Domain;
using Bezoro.Chess.Domain.Board;
using Bezoro.Chess.Domain.Moves;
using FluentAssertions;

namespace Bezoro.Chess.Tests.Unit;

public class BishopMoveGenerationUnitTests
{
	[Theory]
	[InlineData(PieceColor.White)]
	[InlineData(PieceColor.Black)]
	public void MoveGenerator_ForBishopOnD4WithBlockingAndCaptures_ShouldGenerateCorrectMoves(PieceColor color)
	{
		// Arrange
		var fromPosition  = new Position("d4");
		var opponentColor = color.Opposite();

		var initialBoard = new Piece[8, 8];
		initialBoard[fromPosition.Row, fromPosition.Col] = new(PieceType.Bishop, color);

		// Friendly piece (blocking)
		initialBoard[new Position("b6").Row, new Position("b6").Col] = new(PieceType.Pawn, color);
		// Enemy piece (capturable)
		initialBoard[new Position("f6").Row, new Position("f6").Col] = new(PieceType.Pawn, opponentColor);
		// Enemy piece (capturable)
		initialBoard[new Position("b2").Row, new Position("b2").Col] = new(PieceType.Pawn, opponentColor);
		// Friendly piece (blocking)
		initialBoard[new Position("f2").Row, new Position("f2").Col] = new(PieceType.Pawn, color);

		var gameState = new GameState
		{
			PiecePositions = initialBoard,
			ActiveColor    = color
		};

		// Act
		var moves = MoveGenerator.GeneratePieceMoves(fromPosition, gameState).ToList();

		// Assert
		// Expected moves:
		// Up-left: c5 (b6 is blocked) -> 1 move
		// Up-right: e5, f6 (capture) -> 2 moves
		// Down-left: c3, b2 (capture) -> 2 moves
		// Down-right: e3 (f2 is blocked) -> 1 move
		// Total = 6 moves
		moves.Should().HaveCount(6);

		moves.Select(m => m.To.ToString()).Should().Contain("c5");
		moves.Select(m => m.To.ToString()).Should().Contain("e5");
		moves.Select(m => m.To.ToString()).Should().Contain("f6"); // Capture
		moves.Select(m => m.To.ToString()).Should().Contain("c3");
		moves.Select(m => m.To.ToString()).Should().Contain("b2"); // Capture
		moves.Select(m => m.To.ToString()).Should().Contain("e3");
	}

	[Theory]
	[InlineData(PieceColor.White)]
	[InlineData(PieceColor.Black)]
	public void MoveGenerator_ForLoneBishopOnD4_ShouldGenerate13Moves(PieceColor color)
	{
		// Arrange
		var fromPosition = new Position("d4");
		var initialBoard = new Piece[8, 8];
		initialBoard[fromPosition.Row, fromPosition.Col] = new(PieceType.Bishop, color);

		var gameState = new GameState
		{
			PiecePositions = initialBoard,
			ActiveColor    = color
		};

		// Act
		var moves = MoveGenerator.GeneratePieceMoves(fromPosition, gameState).ToList();

		// Assert
		moves.Should().HaveCount(13);
		moves.Select(m => m.To.ToString()).Should().Contain("a1");
		moves.Select(m => m.To.ToString()).Should().Contain("h8");
		moves.Select(m => m.To.ToString()).Should().Contain("a7");
		moves.Select(m => m.To.ToString()).Should().Contain("g1");
	}

	[Theory]
	[InlineData(PieceColor.White)]
	[InlineData(PieceColor.Black)]
	public void MoveGenerator_ForStandardStartingBishop_ShouldGenerateZeroMoves(PieceColor color)
	{
		// Arrange
		var isWhite = color == PieceColor.White;
		// In a standard game setup, the bishops on c1/c8 are blocked by pawns.
		var fromPosition = new Position(isWhite ? "c1" : "c8");
		var gameState    = BoardSetup.CreateStandardGame() with { ActiveColor = color };

		// Act
		var moves = MoveGenerator.GeneratePieceMoves(fromPosition, gameState).ToList();

		// Assert
		moves.Should().HaveCount(0);
	}
}
