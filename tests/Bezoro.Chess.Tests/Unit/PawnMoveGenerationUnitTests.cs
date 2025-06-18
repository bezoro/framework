using Bezoro.Chess.Domain;
using Bezoro.Chess.Domain.Board;
using Bezoro.Chess.Domain.Moves;
using FluentAssertions;

namespace Bezoro.Chess.Tests.Unit;

public class PawnMoveGenerationUnitTests
{
	[Theory]
	[InlineData(PieceColor.White, "e4", "d5")]
	[InlineData(PieceColor.Black, "d5", "e4")]
	public void MoveGenerator_ForPawn_ShouldGenerateCaptureMoves(
		PieceColor color, string from, string capture)
	{
		// Arrange
		var        fromPosition    = new Position(from);
		var        capturePosition = new Position(capture);
		PieceColor opponentColor   = color.Opposite();

		var initialBoard = new Piece[8, 8];
		initialBoard[fromPosition.Row, fromPosition.Col]       = new(PieceType.Pawn, color);
		initialBoard[capturePosition.Row, capturePosition.Col] = new(PieceType.Pawn, opponentColor);

		var gameState = new GameState
		{
			PiecePositions = initialBoard,
			ActiveColor    = color
		};

		// Act
		List<Move> moves = MoveGenerator.GeneratePieceMoves(fromPosition, gameState).ToList();

		// Assert
		moves.Should().Contain(m => m.To == capturePosition);
	}

	[Theory]
	[InlineData(PieceColor.White, "e5", "d6")]
	[InlineData(PieceColor.Black, "d4", "e3")]
	public void MoveGenerator_ForPawn_ShouldGenerateEnPassantMove(
		PieceColor color, string from, string enPassantTarget)
	{
		// Arrange
		var        fromPosition          = new Position(from);
		var        enPassantTargetSquare = new Position(enPassantTarget);
		PieceColor opponentColor         = color.Opposite();
		var        opponentPawnPosition  = new Position(fromPosition.Row, enPassantTargetSquare.Col);

		var initialBoard = new Piece[8, 8];
		initialBoard[fromPosition.Row, fromPosition.Col]                 = new(PieceType.Pawn, color);
		initialBoard[opponentPawnPosition.Row, opponentPawnPosition.Col] = new(PieceType.Pawn, opponentColor);

		var gameState = new GameState
		{
			PiecePositions        = initialBoard,
			ActiveColor           = color,
			EnPassantTargetSquare = enPassantTargetSquare
		};

		// Act
		List<Move> moves = MoveGenerator.GeneratePieceMoves(fromPosition, gameState).ToList();

		// Assert
		moves.Should().Contain(m => m.Type == MoveType.EnPassant && m.To == enPassantTargetSquare);
	}

	[Theory]
	[InlineData(PieceColor.White, "e7")]
	[InlineData(PieceColor.Black, "d2")]
	public void MoveGenerator_ForPawnOnPromotionRank_ShouldGeneratePromotionMoves(
		PieceColor color, string from)
	{
		// Arrange
		var fromPosition = new Position(from);

		var initialBoard = new Piece[8, 8];
		initialBoard[fromPosition.Row, fromPosition.Col] = new(PieceType.Pawn, color);

		var gameState = new GameState
		{
			PiecePositions = initialBoard,
			ActiveColor    = color
		};

		// Act
		List<Move> moves = MoveGenerator.GeneratePieceMoves(fromPosition, gameState).ToList();

		// Assert
		moves.Should().Contain(m => m.Type == MoveType.PawnPromotion);
	}

	[Theory]
	[InlineData(PieceColor.White, "e2", new[] { "e3", "e4" })]
	[InlineData(PieceColor.Black, "d7", new[] { "d6", "d5" })]
	public void MoveGenerator_ForPawnOnStartingRank_ShouldGenerateOneAndTwoSquareMoves(
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
