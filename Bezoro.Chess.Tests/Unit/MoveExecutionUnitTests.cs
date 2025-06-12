using ChessLogic;
using FluentAssertions;

namespace Bezoro.Chess.Tests.Unit;

public class MoveExecutionUnitTests
{
	[Fact]
	public void ExecuteMove_BlackPawnCapture_ShouldResetHalfMoveClock()
	{
		// Arrange
		var initialBoard = new Piece[8, 8];
		initialBoard[new Position("d4").Row, new Position("d4").Col] = new(PieceType.Pawn, PieceColor.White);
		initialBoard[new Position("e5").Row, new Position("e5").Col] = new(PieceType.Pawn, PieceColor.Black);

		var initialState = new GameState
		{
			PiecePositions = initialBoard,
			ActiveColor    = PieceColor.Black,
			HalfMoveClock  = 10 // Arbitrary non-zero value
		};

		var move = new Move(new("e5"), new("d4"));

		// Act
		var newState = MoveExecution.ExecuteMove(initialState, move);

		// Assert
		newState.PiecePositions[new Position("d4").Row, new Position("d4").Col].Should()
				.Be(new Piece(PieceType.Pawn, PieceColor.Black));

		newState.PiecePositions[new Position("e5").Row, new Position("e5").Col].Type.Should().Be(PieceType.None);
		newState.HalfMoveClock.Should().Be(0);
		newState.ActiveColor.Should().Be(PieceColor.White);
		newState.FullMoveNumber.Should().Be(2); // Increments after black's move
	}

	[Fact]
	public void ExecuteMove_BlackPawnPromotesToQueen_ShouldReplacePawn()
	{
		// Arrange
		var initialBoard = new Piece[8, 8];
		initialBoard[new Position("a2").Row, new Position("a2").Col] = new(PieceType.Pawn, PieceColor.Black);

		var initialState = new GameState { PiecePositions = initialBoard, ActiveColor = PieceColor.Black };
		var move         = new Move(new("a2"), new("a1"), MoveType.PawnPromotion);

		// Act
		var newState = MoveExecution.ExecuteMove(initialState, move);

		// Assert
		// Assuming promotion to Queen by default
		newState.PiecePositions[new Position("a1").Row, new Position("a1").Col].Should()
				.Be(new Piece(PieceType.Queen, PieceColor.Black));

		newState.PiecePositions[new Position("a2").Row, new Position("a2").Col].Type.Should().Be(PieceType.None);
		newState.ActiveColor.Should().Be(PieceColor.White);
	}

	[Fact]
	public void ExecuteMove_BlackQueensideCastle_ShouldMoveKingAndRook()
	{
		// Arrange
		var initialBoard = new Piece[8, 8];
		initialBoard[new Position("e8").Row, new Position("e8").Col] = new(PieceType.King, PieceColor.Black);
		initialBoard[new Position("a8").Row, new Position("a8").Col] = new(PieceType.Rook, PieceColor.Black);

		var initialState = new GameState
			{ PiecePositions = initialBoard, ActiveColor = PieceColor.Black, Castling = CastlingRights.BlackQueenside };

		var move = new Move(new("e8"), new("c8"), MoveType.CastleQueenside);

		// Act
		var newState = MoveExecution.ExecuteMove(initialState, move);

		// Assert
		newState.PiecePositions[new Position("c8").Row, new Position("c8").Col].Type.Should().Be(PieceType.King);
		newState.PiecePositions[new Position("d8").Row, new Position("d8").Col].Type.Should().Be(PieceType.Rook);
		newState.PiecePositions[new Position("e8").Row, new Position("e8").Col].Type.Should().Be(PieceType.None);
		newState.PiecePositions[new Position("a8").Row, new Position("a8").Col].Type.Should().Be(PieceType.None);
		newState.Castling.Should().NotHaveFlag(CastlingRights.BlackBoth);
		newState.ActiveColor.Should().Be(PieceColor.White);
	}

	[Fact]
	public void ExecuteMove_WhiteEnPassant_ShouldCapturePawn()
	{
		// Arrange
		var initialBoard = new Piece[8, 8];
		// White pawn ready to capture
		initialBoard[new Position("e5").Row, new Position("e5").Col] = new(PieceType.Pawn, PieceColor.White);
		// Black pawn that just moved two squares
		initialBoard[new Position("d5").Row, new Position("d5").Col] = new(PieceType.Pawn, PieceColor.Black);

		var initialState = new GameState
		{
			PiecePositions        = initialBoard,
			ActiveColor           = PieceColor.White,
			EnPassantTargetSquare = new Position("d6")
		};

		var move = new Move(new("e5"), new("d6"), MoveType.EnPassant);

		// Act
		var newState = MoveExecution.ExecuteMove(initialState, move);

		// Assert
		newState.PiecePositions[new Position("d6").Row, new Position("d6").Col].Should()
				.Be(new Piece(PieceType.Pawn, PieceColor.White));

		newState.PiecePositions[new Position("e5").Row, new Position("e5").Col].Type.Should().Be(PieceType.None);
		// Captured pawn should be gone
		newState.PiecePositions[new Position("d5").Row, new Position("d5").Col].Type.Should().Be(PieceType.None);
		newState.ActiveColor.Should().Be(PieceColor.Black);
	}

	[Fact]
	public void ExecuteMove_WhiteKingsideCastle_ShouldMoveKingAndRook()
	{
		// Arrange
		var initialBoard = new Piece[8, 8];
		initialBoard[new Position("e1").Row, new Position("e1").Col] = new(PieceType.King, PieceColor.White);
		initialBoard[new Position("h1").Row, new Position("h1").Col] = new(PieceType.Rook, PieceColor.White);

		var initialState = new GameState
			{ PiecePositions = initialBoard, ActiveColor = PieceColor.White, Castling = CastlingRights.WhiteKingside };

		var move = new Move(new("e1"), new("g1"), MoveType.CastleKingside);

		// Act
		var newState = MoveExecution.ExecuteMove(initialState, move);

		// Assert
		newState.PiecePositions[new Position("g1").Row, new Position("g1").Col].Type.Should().Be(PieceType.King);
		newState.PiecePositions[new Position("f1").Row, new Position("f1").Col].Type.Should().Be(PieceType.Rook);
		newState.PiecePositions[new Position("e1").Row, new Position("e1").Col].Type.Should().Be(PieceType.None);
		newState.PiecePositions[new Position("h1").Row, new Position("h1").Col].Type.Should().Be(PieceType.None);
		newState.Castling.Should().NotHaveFlag(CastlingRights.WhiteBoth);
		newState.ActiveColor.Should().Be(PieceColor.Black);
	}

	[Fact]
	public void ExecuteMove_WhitePawnE2ToE4_ShouldUpdateStateCorrectly()
	{
		// Arrange
		var initialState = BoardSetup.CreateStandardGame();
		var move         = new Move(new("e2"), new("e4"));

		// Act
		var newState = MoveExecution.ExecuteMove(initialState, move);

		// Assert
		// 1. Verify the piece moved
		var pawnAtE4 = newState.PiecePositions[new Position("e4").Row, new Position("e4").Col];
		pawnAtE4.Should().Be(new Piece(PieceType.Pawn, PieceColor.White));

		// 2. Verify the original square is empty
		var pieceAtE2 = newState.PiecePositions[new Position("e2").Row, new Position("e2").Col];
		pieceAtE2.Type.Should().Be(PieceType.None);

		// 3. Verify the active color switched to Black
		newState.ActiveColor.Should().Be(PieceColor.Black);

		// 4. Verify en passant target square is set
		newState.EnPassantTargetSquare.Should().Be(new Position("e3"));

		// 5. Verify half-move clock was reset
		newState.HalfMoveClock.Should().Be(0);

		// 6. Verify full-move number is unchanged (only increments after Black's move)
		newState.FullMoveNumber.Should().Be(1);
	}
}
