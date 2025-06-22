using Bezoro.Chess.Domain.Extensions;
using Bezoro.Chess.Domain.Functions.Moves;
using Bezoro.Chess.Domain.Functions.Moves.Generation;
using Bezoro.Chess.Domain.Helpers;
using Bezoro.Chess.Domain.Shared.Enums;
using Bezoro.Chess.Domain.Types.Records;
using Bezoro.Chess.Domain.Types.Structs;
using FluentAssertions;
using JetBrains.Annotations;

namespace Bezoro.Chess.Tests.Unit;

[TestSubject(typeof(PawnMoveGenerator))]
public class PawnMoveGenerationUnitTests
{
	[Theory]
	[InlineData(PieceColor.White, "e4", "d5")]
	[InlineData(PieceColor.Black, "d5", "e4")]
	internal void MoveGenerator_ForPawn_ShouldGenerateCaptureMoves(PieceColor color, string from, string capture)
	{
		// Arrange
		var        fromPosition    = new Position(from);
		var        capturePosition = new Position(capture);
		PieceColor opponentColor   = color.Opposite();

		var board = new Board(BoardFactory.CreateEmptyBitboards());
		board = board.SetPiece(fromPosition, new Piece(PieceType.Pawn,    color))
					 .SetPiece(capturePosition, new Piece(PieceType.Pawn, opponentColor));

		var gameState = new GameState
		{
			Board       = board,
			ActiveColor = color
		};

		// Act
		List<Move> moves = MoveGenerator.GeneratePieceMoves(fromPosition, gameState).ToList();

		// Assert
		moves.Should().Contain(m => m.To == capturePosition);
	}

	[Theory]
	[InlineData(PieceColor.White, "e5", "d6")]
	[InlineData(PieceColor.Black, "d4", "e3")]
	internal void MoveGenerator_ForPawn_ShouldGenerateEnPassantMove(
		PieceColor color, string from, string enPassantTarget)
	{
		// Arrange
		var        fromPosition          = new Position(from);
		var        enPassantTargetSquare = new Position(enPassantTarget);
		PieceColor opponentColor         = color.Opposite();
		var        opponentPawnPosition  = new Position(fromPosition.Row, enPassantTargetSquare.Col);
		var        board                 = new Board(BoardFactory.CreateEmptyBitboards());
		board = board.SetPiece(fromPosition, new Piece(PieceType.Pawn,         color))
					 .SetPiece(opponentPawnPosition, new Piece(PieceType.Pawn, opponentColor));

		var gameState = new GameState
		{
			Board                 = board,
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
	internal void MoveGenerator_ForPawnOnPromotionRank_ShouldGeneratePromotionMoves(
		PieceColor color, string from)
	{
		// Arrange
		var fromPosition = new Position(from);
		var board        = new Board(BoardFactory.CreateEmptyBitboards());
		board = board.SetPiece(fromPosition, new Piece(PieceType.Pawn, color));
		var gameState = new GameState
		{
			Board       = board,
			ActiveColor = color
		};

		// Act
		List<Move> moves = MoveGenerator.GeneratePieceMoves(fromPosition, gameState).ToList();

		// Assert
		moves.Should().Contain(m => m.Type == MoveType.Promotion);
	}

	[Theory]
	[InlineData(PieceColor.White, "e2", new[] { "e3", "e4" })]
	[InlineData(PieceColor.Black, "d7", new[] { "d6", "d5" })]
	internal void MoveGenerator_ForPawnOnStartingRank_ShouldGenerateOneAndTwoSquareMoves(
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
