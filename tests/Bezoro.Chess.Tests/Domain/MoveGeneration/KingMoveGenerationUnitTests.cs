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

[TestSubject(typeof(KingMoveGenerator))]
public class KingMoveGenerationUnitTests
{
	[Theory]
	[InlineData(PieceColor.White)]
	[InlineData(PieceColor.Black)]
	internal void MoveGenerator_ForKingOnE1_WithBlockingAndCaptures_ShouldGenerateCorrectMoves(PieceColor color)
	{
		// Arrange
		var fromPosition    = new Position(color == PieceColor.White ? "e1" : "e8");
		var opponentColor   = color.Opposite();
		int opponentPawnRow = color == PieceColor.White ? fromPosition.Row + 1 : fromPosition.Row - 1;

		Board initialBoard = new(BoardFactory.CreateEmptyBitboards());
		initialBoard = initialBoard.SetPieces(
			(new(fromPosition.Row, fromPosition.Col), new(PieceType.King, color)),
			(new(fromPosition.Row, fromPosition.Col - 1), new(PieceType.Pawn, color)),
			(new(fromPosition.Row, fromPosition.Col + 1), new(PieceType.Pawn, color)),
			(new(opponentPawnRow, fromPosition.Col  - 1), new(PieceType.Pawn, opponentColor)),
			(new(opponentPawnRow, fromPosition.Col), new(PieceType.Pawn, opponentColor)),
			(new(opponentPawnRow, fromPosition.Col + 1), new(PieceType.Pawn, opponentColor)));

		var gameState = new GameState
		{
			Board       = initialBoard,
			ActiveColor = color
		};

		// Act
		var moves = MoveGenerator.GeneratePieceMoves(fromPosition, gameState).ToList();

		// Assert
		// King can move to d2, e2, f2 (or d7, e7, f7 for black)
		moves.Should().HaveCount(3);
		moves.Select(m => m.To.ToString()).Should()
			 .Contain(new Position(opponentPawnRow, fromPosition.Col - 1).ToString());

		moves.Select(m => m.To.ToString()).Should().Contain(new Position(opponentPawnRow, fromPosition.Col).ToString());
		moves.Select(m => m.To.ToString()).Should()
			 .Contain(new Position(opponentPawnRow, fromPosition.Col + 1).ToString());
	}

	[Theory]
	[InlineData(PieceColor.White)]
	[InlineData(PieceColor.Black)]
	internal void MoveGenerator_ForKingWithBlockedPath_ShouldNotGenerateCastlingMoves(PieceColor color)
	{
		// Arrange
		bool isWhite      = color == PieceColor.White;
		int  kingRow      = isWhite ? 7 : 0;
		var  fromPosition = new Position(kingRow, 4);

		Board initialBoard = new(BoardFactory.CreateEmptyBitboards());
		initialBoard.SetPiece(new(kingRow, 4), new(PieceType.King, color));
		initialBoard.SetPiece(new(kingRow, 4), new(PieceType.King, color));
		initialBoard.SetPiece(new(kingRow, 0), new(PieceType.Rook, color));
		initialBoard.SetPiece(new(kingRow, 7), new(PieceType.Rook, color));
		// Add blocking pieces  
		initialBoard.SetPiece(new(kingRow, 1), new(PieceType.Knight, color)); // Queenside
		initialBoard.SetPiece(new(kingRow, 6), new(PieceType.Bishop, color)); // Kingside

		var gameState = new GameState
		{
			Board       = initialBoard,
			ActiveColor = color,
			Castling    = isWhite ? CastlingRights.White : CastlingRights.Black
		};

		// Act
		var moves = MoveGenerator.GeneratePieceMoves(fromPosition, gameState).ToList();

		// Assert
		moves.Should().NotContain(m => m.Type == MoveType.Castling);
	}

	[Theory]
	[InlineData(PieceColor.White)]
	[InlineData(PieceColor.Black)]
	internal void MoveGenerator_ForKingWithClearPath_ShouldGenerateCastlingMoves(PieceColor color)
	{
		// Arrange
		bool isWhite      = color == PieceColor.White;
		int  kingRow      = isWhite ? 7 : 0;
		var  fromPosition = new Position(kingRow, 4);

		var initialBoard = new Board(BoardFactory.CreateEmptyBitboards());
		initialBoard = initialBoard.SetPieces(
			(new(kingRow, 4), new(PieceType.King, color)),
			(new(kingRow, 0), new(PieceType.Rook, color)),
			(new(kingRow, 7), new(PieceType.Rook, color))
		);

		var gameState = new GameState
		{
			Board       = initialBoard,
			ActiveColor = color,
			Castling    = isWhite ? CastlingRights.White : CastlingRights.Black
		};

		// Act
		var moves = MoveGenerator.GeneratePieceMoves(fromPosition, gameState).ToList();

		// Assert
		moves.Should().Contain(m => m.Type == MoveType.Castling);
	}

	[Theory]
	[InlineData(PieceColor.White)]
	[InlineData(PieceColor.Black)]
	internal void MoveGenerator_ForLoneKingOnD4_ShouldGenerate8Moves(PieceColor color)
	{
		// Arrange
		var   fromPosition = new Position("d4");
		Board initialBoard = new(BoardFactory.CreateEmptyBitboards());
		initialBoard = initialBoard.SetPiece(
			new(fromPosition.Row, fromPosition.Col),
			new(PieceType.King, color));

		var gameState = new GameState
		{
			Board       = initialBoard,
			ActiveColor = color
		};

		// Act
		var moves = MoveGenerator.GeneratePieceMoves(fromPosition, gameState).ToList();

		// Assert
		moves.Should().HaveCount(8);
		moves.Select(m => m.To.ToString()).Should().Contain("d5");
		moves.Select(m => m.To.ToString()).Should().Contain("c5");
		moves.Select(m => m.To.ToString()).Should().Contain("e5");
		moves.Select(m => m.To.ToString()).Should().Contain("d3");
		moves.Select(m => m.To.ToString()).Should().Contain("c3");
		moves.Select(m => m.To.ToString()).Should().Contain("e3");
		moves.Select(m => m.To.ToString()).Should().Contain("c4");
		moves.Select(m => m.To.ToString()).Should().Contain("e4");
	}

	[Theory]
	[InlineData(PieceColor.White)]
	[InlineData(PieceColor.Black)]
	internal void MoveGenerator_ForStandardStartingKing_ShouldGenerateZeroMoves(PieceColor color)
	{
		// Arrange
		bool isWhite = color == PieceColor.White;
		// In a standard game setup, the king is blocked by its own pieces.
		var fromPosition = new Position(isWhite ? "e1" : "e8");
		var gameState    = BoardSetup.CreateStandardGame() with { ActiveColor = color };

		// Act
		var moves = MoveGenerator.GeneratePieceMoves(fromPosition, gameState).ToList();

		// Assert
		moves.Should().HaveCount(0);
	}
}
