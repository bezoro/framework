using Bezoro.Chess.ChessLogic;
using FluentAssertions;

namespace Bezoro.Chess.Tests.Unit;

public class MoveGenerationUnitTests
{
#region Knight

	[Theory]
	[InlineData(PieceColor.White)]
	[InlineData(PieceColor.Black)]
	public void MoveGenerator_ForKnightOnD4_ShouldGenerate8Moves(PieceColor color)
	{
		// Arrange
		var fromPosition = new Position("d4");
		var initialBoard = new Piece[8, 8];
		initialBoard[fromPosition.Row, fromPosition.Col] = new(PieceType.Knight, color);

		var gameState = new GameState
		{
			PiecePositions = initialBoard,
			ActiveColor    = color
		};

		// Act
		var moves = MoveGenerator.GeneratePieceMoves(fromPosition, gameState).ToList();

		// Assert
		moves.Should().HaveCount(8);
	}

	[Theory]
	[InlineData(PieceColor.White, "b1", new[] { "a3", "c3" })]
	[InlineData(PieceColor.White, "g1", new[] { "f3", "h3" })]
	[InlineData(PieceColor.Black, "b8", new[] { "a6", "c6" })]
	[InlineData(PieceColor.Black, "g8", new[] { "f6", "h6" })]
	public void MoveGenerator_ForStandardStartingKnight_ShouldGenerateTwoMoves(
		PieceColor color, string from, string[] expectedMoves)
	{
		// Arrange
		var fromPosition = new Position(from);
		var gameState    = BoardSetup.CreateStandardGame() with { ActiveColor = color };

		// Act
		var moves = MoveGenerator.GeneratePieceMoves(fromPosition, gameState).ToList();

		// Assert
		moves.Should().HaveCount(2);
		moves.Select(m => m.To.ToString()).Should().BeEquivalentTo(expectedMoves);
	}

	[Theory]
	[InlineData(PieceColor.White)]
	[InlineData(PieceColor.Black)]
	public void MoveGenerator_ForKnightOnD4_WithBlockingAndCaptures_ShouldGenerateCorrectMoves(PieceColor color)
	{
		// Arrange
		var fromPosition  = new Position("d4");
		var opponentColor = color.Opposite();

		var initialBoard = new Piece[8, 8];
		initialBoard[fromPosition.Row, fromPosition.Col] = new(PieceType.Knight, color);

		// The 8 potential moves from d4 are: c6, e6, f5, f3, e2, c2, b3, b5.
		// Friendly pieces (blocking)
		initialBoard[new Position("c6").Row, new Position("c6").Col] = new(PieceType.Pawn, color);
		initialBoard[new Position("f3").Row, new Position("f3").Col] = new(PieceType.Pawn, color);
		// Enemy pieces (capturable)
		initialBoard[new Position("e2").Row, new Position("e2").Col] = new(PieceType.Pawn, opponentColor);
		initialBoard[new Position("b5").Row, new Position("b5").Col] = new(PieceType.Pawn, opponentColor);

		var gameState = new GameState
		{
			PiecePositions = initialBoard,
			ActiveColor    = color
		};

		// Act
		var moves = MoveGenerator.GeneratePieceMoves(fromPosition, gameState).ToList();

		// Assert
		// Expected moves: e6, f5, c2, b3 (open) + e2, b5 (captures) = 6 moves
		moves.Should().HaveCount(6);
		moves.Select(m => m.To.ToString()).Should().Contain("e2");    // Capture
		moves.Select(m => m.To.ToString()).Should().Contain("b5");    // Capture
		moves.Select(m => m.To.ToString()).Should().NotContain("c6"); // Blocked
		moves.Select(m => m.To.ToString()).Should().NotContain("f3"); // Blocked
	}

#endregion

#region Pawn

	[Theory]
	[InlineData(PieceColor.White, "e2", new[] { "e3", "e4" })]
	[InlineData(PieceColor.Black, "d7", new[] { "d6", "d5" })]
	public void MoveGenerator_ForPawnOnStartingRank_ShouldGenerateOneAndTwoSquareMoves(
		PieceColor color, string from, string[] expectedMoves)
	{
		// Arrange
		var fromPosition = new Position(from);
		var gameState    = BoardSetup.CreateStandardGame() with { ActiveColor = color };

		// Act
		var moves = MoveGenerator.GeneratePieceMoves(fromPosition, gameState).ToList();

		// Assert
		moves.Should().HaveCount(2);
		moves.Select(m => m.To.ToString()).Should().BeEquivalentTo(expectedMoves);
	}

	[Theory]
	[InlineData(PieceColor.White, "e4", "d5")]
	[InlineData(PieceColor.Black, "d5", "e4")]
	public void MoveGenerator_ForPawn_ShouldGenerateCaptureMoves(
		PieceColor color, string from, string capture)
	{
		// Arrange
		var fromPosition    = new Position(from);
		var capturePosition = new Position(capture);
		var opponentColor   = color.Opposite();

		var initialBoard = new Piece[8, 8];
		initialBoard[fromPosition.Row, fromPosition.Col]       = new(PieceType.Pawn, color);
		initialBoard[capturePosition.Row, capturePosition.Col] = new(PieceType.Pawn, opponentColor);

		var gameState = new GameState
		{
			PiecePositions = initialBoard,
			ActiveColor    = color
		};

		// Act
		var moves = MoveGenerator.GeneratePieceMoves(fromPosition, gameState).ToList();

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
		var fromPosition          = new Position(from);
		var enPassantTargetSquare = new Position(enPassantTarget);
		var opponentColor         = color.Opposite();
		var opponentPawnPosition  = new Position(fromPosition.Row, enPassantTargetSquare.Col);

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
		var moves = MoveGenerator.GeneratePieceMoves(fromPosition, gameState).ToList();

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
		var moves = MoveGenerator.GeneratePieceMoves(fromPosition, gameState).ToList();

		// Assert
		moves.Should().Contain(m => m.Type == MoveType.PawnPromotion);
	}

#endregion

#region Bishop

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

#endregion

#region Rook

	[Theory]
	[InlineData(PieceColor.White)]
	[InlineData(PieceColor.Black)]
	public void MoveGenerator_ForStandardStartingRook_ShouldGenerateZeroMoves(PieceColor color)
	{
		// Arrange
		var isWhite = color == PieceColor.White;
		// In a standard game setup, the rooks on a1/a8 are blocked by pawns.
		var fromPosition = new Position(isWhite ? "a1" : "a8");
		var gameState    = BoardSetup.CreateStandardGame() with { ActiveColor = color };

		// Act
		var moves = MoveGenerator.GeneratePieceMoves(fromPosition, gameState).ToList();

		// Assert
		moves.Should().HaveCount(0);
	}

	[Theory]
	[InlineData(PieceColor.White)]
	[InlineData(PieceColor.Black)]
	public void MoveGenerator_ForLoneRookOnD4_ShouldGenerate14Moves(PieceColor color)
	{
		// Arrange
		var fromPosition = new Position("d4");
		var initialBoard = new Piece[8, 8];
		initialBoard[fromPosition.Row, fromPosition.Col] = new(PieceType.Rook, color);

		var gameState = new GameState
		{
			PiecePositions = initialBoard,
			ActiveColor    = color
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
	public void MoveGenerator_ForRookOnD4_WithBlockingAndCaptures_ShouldGenerateCorrectMoves(PieceColor color)
	{
		// Arrange
		var fromPosition  = new Position("d4");
		var opponentColor = color.Opposite();

		var initialBoard = new Piece[8, 8];
		initialBoard[fromPosition.Row, fromPosition.Col] = new(PieceType.Rook, color);

		// Friendly piece (blocking)
		initialBoard[new Position("d6").Row, new Position("d6").Col] = new(PieceType.Pawn, color);
		// Enemy piece (capturable)
		initialBoard[new Position("d2").Row, new Position("d2").Col] = new(PieceType.Pawn, opponentColor);
		// Friendly piece (blocking)
		initialBoard[new Position("b4").Row, new Position("b4").Col] = new(PieceType.Pawn, color);
		// Enemy piece (capturable)
		initialBoard[new Position("g4").Row, new Position("g4").Col] = new(PieceType.Pawn, opponentColor);

		var gameState = new GameState
		{
			PiecePositions = initialBoard,
			ActiveColor    = color
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

#endregion

#region Queen

	[Theory]
	[InlineData(PieceColor.White)]
	[InlineData(PieceColor.Black)]
	public void MoveGenerator_ForStandardStartingQueen_ShouldGenerateZeroMoves(PieceColor color)
	{
		// Arrange
		var isWhite = color == PieceColor.White;
		// In a standard game setup, the queen is blocked by pawns.
		var fromPosition = new Position(isWhite ? "d1" : "d8");
		var gameState    = BoardSetup.CreateStandardGame() with { ActiveColor = color };

		// Act
		var moves = MoveGenerator.GeneratePieceMoves(fromPosition, gameState).ToList();

		// Assert
		moves.Should().HaveCount(0);
	}

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
	public void MoveGenerator_ForQueenOnD4_WithBlockingAndCaptures_ShouldGenerateCorrectMoves(PieceColor color)
	{
		// Arrange
		var fromPosition  = new Position("d4");
		var opponentColor = color.Opposite();

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

#endregion

#region King

	[Theory]
	[InlineData(PieceColor.White)]
	[InlineData(PieceColor.Black)]
	public void MoveGenerator_ForStandardStartingKing_ShouldGenerateZeroMoves(PieceColor color)
	{
		// Arrange
		var isWhite = color == PieceColor.White;
		// In a standard game setup, the king is blocked by its own pieces.
		var fromPosition = new Position(isWhite ? "e1" : "e8");
		var gameState    = BoardSetup.CreateStandardGame() with { ActiveColor = color };

		// Act
		var moves = MoveGenerator.GeneratePieceMoves(fromPosition, gameState).ToList();

		// Assert
		moves.Should().HaveCount(0);
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
	public void MoveGenerator_ForKingOnE1_WithBlockingAndCaptures_ShouldGenerateCorrectMoves(PieceColor color)
	{
		// Arrange
		var fromPosition    = new Position(color == PieceColor.White ? "e1" : "e8");
		var opponentColor   = color.Opposite();
		var opponentPawnRow = color == PieceColor.White ? fromPosition.Row - 1 : fromPosition.Row + 1;

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
	public void MoveGenerator_ForKing_ShouldGenerateCastlingMoves_WhenPathIsClear(PieceColor color)
	{
		// Arrange
		var isWhite      = color == PieceColor.White;
		var kingRow      = isWhite ? 7 : 0;
		var fromPosition = new Position(kingRow, 4);

		var initialBoard = new Piece[8, 8];
		initialBoard[kingRow, 4] = new(PieceType.King, color);
		initialBoard[kingRow, 0] = new(PieceType.Rook, color);
		initialBoard[kingRow, 7] = new(PieceType.Rook, color);

		var gameState = new GameState
		{
			PiecePositions = initialBoard,
			ActiveColor    = color,
			Castling       = isWhite ? CastlingRights.WhiteBoth : CastlingRights.BlackBoth
		};

		// Act
		var moves = MoveGenerator.GeneratePieceMoves(fromPosition, gameState).ToList();

		// Assert
		moves.Should().Contain(m => m.Type == MoveType.CastleKingside);
		moves.Should().Contain(m => m.Type == MoveType.CastleQueenside);
	}

	[Theory]
	[InlineData(PieceColor.White)]
	[InlineData(PieceColor.Black)]
	public void MoveGenerator_ForKing_ShouldNotGenerateCastlingMoves_WhenPathIsBlocked(PieceColor color)
	{
		// Arrange
		var isWhite      = color == PieceColor.White;
		var kingRow      = isWhite ? 7 : 0;
		var fromPosition = new Position(kingRow, 4);

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
			Castling       = isWhite ? CastlingRights.WhiteBoth : CastlingRights.BlackBoth
		};

		// Act
		var moves = MoveGenerator.GeneratePieceMoves(fromPosition, gameState).ToList();

		// Assert
		moves.Should().NotContain(m => m.Type == MoveType.CastleKingside);
		moves.Should().NotContain(m => m.Type == MoveType.CastleQueenside);
	}

#endregion
}
