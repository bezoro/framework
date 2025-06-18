using Bezoro.Chess.Domain;
using Bezoro.Chess.Domain.Board;
using Bezoro.Chess.Domain.Moves;
using FluentAssertions;

namespace Bezoro.Chess.Tests.Unit;

public class KingMoveGenerationUnitTests
{
	[Theory]
	[InlineData(PieceColor.White)]
	[InlineData(PieceColor.Black)]
	public void MoveGenerator_ForKingOnE1_WithBlockingAndCaptures_ShouldGenerateCorrectMoves(PieceColor color)
	{
		// Arrange
		var        fromPosition    = new Position(color == PieceColor.White ? "e1" : "e8");
		PieceColor opponentColor   = color.Opposite();
		int        opponentPawnRow = color == PieceColor.White ? fromPosition.Row - 1 : fromPosition.Row + 1;

		var initialBoard = new Piece[8, 8];
		initialBoard[fromPosition.Row, fromPosition.Col] = new(PieceType.King, color);

		// Friendly pieces (blocking)
		initialBoard[fromPosition.Row, fromPosition.Col - 1] = new(PieceType.Pawn, color); // d1/d8
		initialBoard[fromPosition.Row, fromPosition.Col + 1] = new(PieceType.Pawn, color); // f1/f8

		// Enemy pieces (capturable)
		initialBoard[opponentPawnRow, fromPosition.Col - 1] = new(PieceType.Pawn, opponentColor); // d2/d7
		initialBoard[opponentPawnRow, fromPosition.Col]     = new(PieceType.Pawn, opponentColor); // e2/e7
		initialBoard[opponentPawnRow, fromPosition.Col + 1] = new(PieceType.Pawn, opponentColor); // f2/f7

		var gameState = new GameState
		{
			PiecePositions = initialBoard,
			ActiveColor    = color
		};

		// Act
		List<Move> moves = MoveGenerator.GeneratePieceMoves(fromPosition, gameState).ToList();

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
	public void MoveGenerator_ForKingWithBlockedPath_ShouldNotGenerateCastlingMoves(PieceColor color)
	{
		// Arrange
		bool isWhite      = color == PieceColor.White;
		int  kingRow      = isWhite ? 7 : 0;
		var  fromPosition = new Position(kingRow, 4);

		var initialBoard = new Piece[8, 8];
		initialBoard[kingRow, 4] = new(PieceType.King, color);
		initialBoard[kingRow, 0] = new(PieceType.Rook, color);
		initialBoard[kingRow, 7] = new(PieceType.Rook, color);
		// Add blocking pieces
		initialBoard[kingRow, 1] = new(PieceType.Knight, color); // Queenside
		initialBoard[kingRow, 6] = new(PieceType.Bishop, color); // Kingside

		var gameState = new GameState
		{
			PiecePositions = initialBoard,
			ActiveColor    = color,
			Castling       = isWhite ? CastlingRights.White : CastlingRights.Black
		};

		// Act
		List<Move> moves = MoveGenerator.GeneratePieceMoves(fromPosition, gameState).ToList();

		// Assert
		moves.Should().NotContain(m => m.Type == MoveType.CastleKingside);
		moves.Should().NotContain(m => m.Type == MoveType.CastleQueenside);
	}

	[Theory]
	[InlineData(PieceColor.White)]
	[InlineData(PieceColor.Black)]
	public void MoveGenerator_ForKingWithClearPath_ShouldGenerateCastlingMoves(PieceColor color)
	{
		// Arrange
		bool isWhite      = color == PieceColor.White;
		int  kingRow      = isWhite ? 7 : 0;
		var  fromPosition = new Position(kingRow, 4);

		var initialBoard = new Piece[8, 8];
		initialBoard[kingRow, 4] = new(PieceType.King, color);
		initialBoard[kingRow, 0] = new(PieceType.Rook, color);
		initialBoard[kingRow, 7] = new(PieceType.Rook, color);

		var gameState = new GameState
		{
			PiecePositions = initialBoard,
			ActiveColor    = color,
			Castling       = isWhite ? CastlingRights.White : CastlingRights.Black
		};

		// Act
		List<Move> moves = MoveGenerator.GeneratePieceMoves(fromPosition, gameState).ToList();

		// Assert
		moves.Should().Contain(m => m.Type == MoveType.CastleKingside);
		moves.Should().Contain(m => m.Type == MoveType.CastleQueenside);
	}

	[Theory]
	[InlineData(PieceColor.White)]
	[InlineData(PieceColor.Black)]
	public void MoveGenerator_ForLoneKingOnD4_ShouldGenerate8Moves(PieceColor color)
	{
		// Arrange
		var fromPosition = new Position("d4");
		var initialBoard = new Piece[8, 8];
		initialBoard[fromPosition.Row, fromPosition.Col] = new(PieceType.King, color);

		var gameState = new GameState
		{
			PiecePositions = initialBoard,
			ActiveColor    = color
		};

		// Act
		List<Move> moves = MoveGenerator.GeneratePieceMoves(fromPosition, gameState).ToList();

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
	public void MoveGenerator_ForStandardStartingKing_ShouldGenerateZeroMoves(PieceColor color)
	{
		// Arrange
		bool isWhite = color == PieceColor.White;
		// In a standard game setup, the king is blocked by its own pieces.
		var       fromPosition = new Position(isWhite ? "e1" : "e8");
		GameState gameState    = BoardSetup.CreateStandardGame() with { ActiveColor = color };

		// Act
		List<Move> moves = MoveGenerator.GeneratePieceMoves(fromPosition, gameState).ToList();

		// Assert
		moves.Should().HaveCount(0);
	}
}
