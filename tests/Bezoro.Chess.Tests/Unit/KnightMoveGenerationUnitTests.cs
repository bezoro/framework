using Bezoro.Chess.Domain.Extensions;
using Bezoro.Chess.Domain.Functions.Moves;
using Bezoro.Chess.Domain.Helpers;
using Bezoro.Chess.Domain.Shared.Enums;
using Bezoro.Chess.Domain.Types.Records;
using Bezoro.Chess.Domain.Types.Structs;
using FluentAssertions;

namespace Bezoro.Chess.Tests.Unit;

public class KnightMoveGenerationUnitTests
{
	[Theory]
	[InlineData(PieceColor.White)]
	[InlineData(PieceColor.Black)]
	internal void MoveGenerator_ForKnightOnD4_ShouldGenerate8Moves(PieceColor color)
	{
		// Arrange
		var fromPosition = new Position("d4");
		var initialBoard = new Piece[8, 8];
		initialBoard[fromPosition.Row, fromPosition.Col] = new Piece(PieceType.Knight, color);

		var gameState = new GameState
		{
			PiecePositions = initialBoard,
			ActiveColor    = color
		};

		// Act
		List<Move> moves = MoveGenerator.GeneratePieceMoves(fromPosition, gameState).ToList();

		// Assert
		moves.Should().HaveCount(8);
	}

	[Theory]
	[InlineData(PieceColor.White)]
	[InlineData(PieceColor.Black)]
	internal void MoveGenerator_ForKnightOnD4_WithBlockingAndCaptures_ShouldGenerateCorrectMoves(PieceColor color)
	{
		// Arrange
		var        fromPosition  = new Position("d4");
		PieceColor opponentColor = color.Opposite();

		var initialBoard = new Piece[8, 8];
		initialBoard[fromPosition.Row, fromPosition.Col] = new Piece(PieceType.Knight, color);

		// The 8 potential moves from d4 are: c6, e6, f5, f3, e2, c2, b3, b5.
		// Friendly pieces (blocking)
		initialBoard[new Position("c6").Row, new Position("c6").Col] = new Piece(PieceType.Pawn, color);
		initialBoard[new Position("f3").Row, new Position("f3").Col] = new Piece(PieceType.Pawn, color);
		// Enemy pieces (capturable)
		initialBoard[new Position("e2").Row, new Position("e2").Col] = new Piece(PieceType.Pawn, opponentColor);
		initialBoard[new Position("b5").Row, new Position("b5").Col] = new Piece(PieceType.Pawn, opponentColor);

		var gameState = new GameState
		{
			PiecePositions = initialBoard,
			ActiveColor    = color
		};

		// Act
		List<Move> moves = MoveGenerator.GeneratePieceMoves(fromPosition, gameState).ToList();

		// Assert
		// Expected moves: e6, f5, c2, b3 (open) + e2, b5 (captures) = 6 moves
		moves.Should().HaveCount(6);
		moves.Select(m => m.To.ToString()).Should().Contain("e2");    // Capture
		moves.Select(m => m.To.ToString()).Should().Contain("b5");    // Capture
		moves.Select(m => m.To.ToString()).Should().NotContain("c6"); // Blocked
		moves.Select(m => m.To.ToString()).Should().NotContain("f3"); // Blocked
	}

	[Theory]
	[InlineData(PieceColor.White, "b1", new[] { "a3", "c3" })]
	[InlineData(PieceColor.White, "g1", new[] { "f3", "h3" })]
	[InlineData(PieceColor.Black, "b8", new[] { "a6", "c6" })]
	[InlineData(PieceColor.Black, "g8", new[] { "f6", "h6" })]
	internal void MoveGenerator_ForStandardStartingKnight_ShouldGenerateTwoMoves(
		PieceColor color, string from, string[] expectedMoves)
	{
		// Arrange
		var       fromPosition = new Position(from);
		GameState gameState    = BoardSetup.CreateStandardGame() with { ActiveColor = color };

		// Act
		List<Move> moves = MoveGenerator.GeneratePieceMoves(fromPosition, gameState).ToList();

		// Assert
		moves.Should().HaveCount(2);
		moves.Select(m => m.To.ToString()).Should().BeEquivalentTo(expectedMoves);
	}
}
