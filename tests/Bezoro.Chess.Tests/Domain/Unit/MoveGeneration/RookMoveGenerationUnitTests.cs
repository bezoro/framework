using Bezoro.Chess.Domain.Extensions;
using Bezoro.Chess.Domain.Functions.Moves;
using Bezoro.Chess.Domain.Functions.Moves.Generation;
using Bezoro.Chess.Domain.Helpers;
using Bezoro.Chess.Domain.Shared.Enums;
using Bezoro.Chess.Domain.Types.Records;
using Bezoro.Chess.Domain.Types.Structs;
using FluentAssertions;
using JetBrains.Annotations;

namespace Bezoro.Chess.Tests.Domain.Unit.MoveGeneration;

[TestSubject(typeof(RookMoveGenerator))]
public class RookMoveGenerationUnitTests
{
	[Theory]
	[InlineData(PieceColor.White)]
	[InlineData(PieceColor.Black)]
	internal void MoveGenerator_ForLoneRookOnD4_ShouldGenerate14Moves(PieceColor color)
	{
		// Arrange
		var   fromPosition = new Position("d4");
		Board board        = new(BoardFactory.CreateEmptyBitboards());
		board = board.SetPiece(new(fromPosition.Row, fromPosition.Col), new(PieceType.Rook, color));
		var gameState = new GameState
		{
			Board       = board,
			ActiveColor = color
		};

		// Act
		var moves = MoveGenerator.GeneratePieceMoves(fromPosition, gameState).ToList();

		// Assert
		moves.Should().HaveCount(14);
		moves.Select(m => m.To.ToString()).Should().Contain("d1"); // Vertical
		moves.Select(m => m.To.ToString()).Should().Contain("d8"); // Vertical
		moves.Select(m => m.To.ToString()).Should().Contain("a4"); // Horizontal
		moves.Select(m => m.To.ToString()).Should().Contain("h4"); // Horizontal
	}

	[Theory]
	[InlineData(PieceColor.White)]
	[InlineData(PieceColor.Black)]
	internal void MoveGenerator_ForRookOnD4_WithBlockingAndCaptures_ShouldGenerateCorrectMoves(PieceColor color)
	{
		// Arrange
		var fromPosition  = new Position("d4");
		var opponentColor = color.Opposite();

		Board board = new(BoardFactory.CreateEmptyBitboards());
		board = board.SetPiece(fromPosition, new(PieceType.Rook, color));

		// Friendly piece (blocking)
		board = board.SetPiece(new("d6"), new(PieceType.Pawn, color))
					 .SetPiece(new("b4"), new(PieceType.Pawn, color))
					 .SetPiece(new("d2"), new(PieceType.Pawn, opponentColor))
					 .SetPiece(new("g4"), new(PieceType.Pawn, opponentColor));

		var gameState = new GameState
		{
			Board       = board,
			ActiveColor = color
		};

		// Act
		var moves = MoveGenerator.GeneratePieceMoves(fromPosition, gameState).ToList();

		// Assert
		// Expected moves:
		// Up:    d5 (d6 is blocked) -> 1 move
		// Down:  d3, d2 (capture) -> 2 moves
		// Left:  c4 (b4 is blocked) -> 1 move
		// Right: e4, f4, g4 (capture) -> 3 moves
		// Total = 7 moves
		moves.Should().HaveCount(7);
		moves.Select(m => m.To.ToString()).Should().Contain("d5");
		moves.Select(m => m.To.ToString()).Should().Contain("d2"); // Capture
		moves.Select(m => m.To.ToString()).Should().Contain("c4");
		moves.Select(m => m.To.ToString()).Should().Contain("g4"); // Capture
	}

	[Theory]
	[InlineData(PieceColor.White)]
	[InlineData(PieceColor.Black)]
	internal void MoveGenerator_ForStandardStartingRook_ShouldGenerateZeroMoves(PieceColor color)
	{
		// Arrange
		bool isWhite = color == PieceColor.White;
		// In a standard game setup, the rooks on a1/a8 are blocked by pawns.
		var fromPosition = new Position(isWhite ? "a1" : "a8");
		var gameState    = BoardSetup.CreateStandardGame() with { ActiveColor = color };

		// Act
		var moves = MoveGenerator.GeneratePieceMoves(fromPosition, gameState).ToList();

		// Assert
		moves.Should().HaveCount(0);
	}
}
