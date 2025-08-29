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

[TestSubject(typeof(QueenMoveGenerator))]
public class QueenMoveGenerationUnitTests
{
	[Theory]
	[InlineData(PieceColor.White)]
	[InlineData(PieceColor.Black)]
	internal void MoveGenerator_ForLoneQueenOnD4_ShouldGenerate27Moves(PieceColor color)
	{
		// Arrange
		var   fromPosition = new Position("d4");
		Board board        = new(BoardFactory.CreateEmptyBitboards());
		board = board.SetPiece(fromPosition, new(PieceType.Queen, color));
		var gameState = new GameState
		{
			Board       = board,
			ActiveColor = color
		};

		// Act
		var moves = MoveGenerator.GeneratePieceMoves(fromPosition, gameState).ToList();

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
	internal void MoveGenerator_ForQueenOnD4_WithBlockingAndCaptures_ShouldGenerateCorrectMoves(PieceColor color)
	{
		// Arrange
		var fromPosition  = new Position("d4");
		var opponentColor = color.Opposite();

		Board board = new(BoardFactory.CreateEmptyBitboards());
		board = board.SetPiece(fromPosition, new(PieceType.Queen, color));

		// Add pieces to block and be captured
		// Friendly (blocking)
		board = board.SetPiece(new("d6"), new(PieceType.Pawn, color)); // Up
		board = board.SetPiece(new("b4"), new(PieceType.Pawn, color)); // Left
		board = board.SetPiece(new("b6"), new(PieceType.Pawn, color)); // Up-Left 
		board = board.SetPiece(new("f2"), new(PieceType.Pawn, color)); // Down-Right
		// Enemy (capturable)
		board = board.SetPiece(new("d2"), new(PieceType.Pawn, opponentColor)); // Down
		board = board.SetPiece(new("g4"), new(PieceType.Pawn, opponentColor)); // Right
		board = board.SetPiece(new("f6"), new(PieceType.Pawn, opponentColor)); // Up-Right
		board = board.SetPiece(new("b2"), new(PieceType.Pawn, opponentColor)); // Down-Left

		var gameState = new GameState
		{
			Board       = board,
			ActiveColor = color
		};

		// Act
		var moves = MoveGenerator.GeneratePieceMoves(fromPosition, gameState).ToList();

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
	internal void MoveGenerator_ForStandardStartingQueen_ShouldGenerateZeroMoves(PieceColor color)
	{
		// Arrange
		bool isWhite = color == PieceColor.White;
		// In a standard game setup, the queen is blocked by pawns.
		var fromPosition = new Position(isWhite ? "d1" : "d8");
		var gameState    = BoardSetup.CreateStandardGame() with { ActiveColor = color };

		// Act
		var moves = MoveGenerator.GeneratePieceMoves(fromPosition, gameState).ToList();

		// Assert
		moves.Should().HaveCount(0);
	}
}
