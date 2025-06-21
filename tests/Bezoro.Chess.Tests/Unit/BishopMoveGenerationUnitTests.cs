using Bezoro.Chess.Domain.Extensions;
using Bezoro.Chess.Domain.Functions.Moves;
using Bezoro.Chess.Domain.Helpers;
using Bezoro.Chess.Domain.Shared.Enums;
using Bezoro.Chess.Domain.Types.Records;
using Bezoro.Chess.Domain.Types.Structs;
using FluentAssertions;

namespace Bezoro.Chess.Tests.Unit;

public class BishopMoveGenerationUnitTests
{
	[Theory]
	[InlineData(PieceColor.White)]
	[InlineData(PieceColor.Black)]
	internal void MoveGenerator_ForBishopOnD4WithBlockingAndCaptures_ShouldGenerateCorrectMoves(PieceColor color)
	{
		// Arrange
		var        fromPosition  = new Position("d4");
		PieceColor opponentColor = color.Opposite();

		Board board = new(BoardFactory.CreateEmptyBitboards());
		board = board.SetPiece(fromPosition, new Piece(PieceType.Bishop, color));

		// Friendly piece (blocking)
		board = board.SetPieces(
			(new Position("b6"), new Piece(PieceType.Pawn, color)),
			(new Position("f2"), new Piece(PieceType.Pawn, color)),
			(new Position("f2"), new Piece(PieceType.Pawn, color)), // Friendly, blocking
			(new Position("f6"), new Piece(PieceType.Pawn, opponentColor)),
			(new Position("b2"), new Piece(PieceType.Pawn, opponentColor))
		);

		var gameState = new GameState
		{
			Board       = board,
			ActiveColor = color
		};

		// Act
		List<Move> moves = MoveGenerator.GeneratePieceMoves(fromPosition, gameState).ToList();

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
	internal void MoveGenerator_ForLoneBishopOnD4_ShouldGenerate13Moves(PieceColor color)
	{
		// Arrange
		var fromPosition = new Position("d4");

		Board board = new Board(BoardFactory.CreateEmptyBitboards())
			.SetPiece(fromPosition, new Piece(PieceType.Bishop, color));

		var gameState = new GameState
		{
			Board       = board,
			ActiveColor = color
		};

		// Act
		List<Move> moves = MoveGenerator.GeneratePieceMoves(fromPosition, gameState).ToList();

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
	internal void MoveGenerator_ForStandardStartingBishop_ShouldGenerateZeroMoves(PieceColor color)
	{
		// Arrange
		bool isWhite = color == PieceColor.White;
		// In a standard game setup, the bishops on c1/c8 are blocked by pawns.
		var       fromPosition = new Position(isWhite ? "c1" : "c8");
		GameState gameState    = BoardSetup.CreateStandardGame() with { ActiveColor = color };

		// Act
		List<Move> moves = MoveGenerator.GeneratePieceMoves(fromPosition, gameState).ToList();

		// Assert
		moves.Should().HaveCount(0);
	}
}
