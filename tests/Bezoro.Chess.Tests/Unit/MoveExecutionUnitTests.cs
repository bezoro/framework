using Bezoro.Chess.Domain;
using Bezoro.Chess.Domain.Board;
using Bezoro.Chess.Domain.Moves;
using FluentAssertions;

namespace Bezoro.Chess.Tests.Unit;

public class MoveExecutionUnitTests
{
	private GameState _standardGame = BoardSetup.CreateStandardGame();

	[Fact]
	public void ExecuteMove_BlackKingMoves_RevokesBlackCastlingRights()
	{
		// Arrange
		_standardGame = BoardSetup.CreateStandardGameBlackStarts();
		var   from = new Position(0, 4);
		Piece king = _standardGame.GetPieceAt(from);
		var   move = Move.CreateNormal(from, new(1, 4), king); // Black King e8 to e7

		// Act
		GameState newState = MoveExecution.ExecuteMove(_standardGame, move);

		// Assert
		var expectedRights = CastlingRights.WhiteBoth;
		Assert.Equal(expectedRights, newState.Castling);
	}

	[Fact]
	public void ExecuteMove_BlackPawnCapture_ShouldResetHalfMoveClock()
	{
		// Arrange
		var initialBoard = new Piece[8, 8];
		var fromPos      = new Position("e5");
		var toPos        = new Position("d4");

		initialBoard[toPos.Row, toPos.Col]     = new(PieceType.Pawn, PieceColor.White);
		initialBoard[fromPos.Row, fromPos.Col] = new(PieceType.Pawn, PieceColor.Black);

		var initialState = new GameState
		{
			PiecePositions = initialBoard,
			ActiveColor    = PieceColor.Black,
			HalfMoveClock  = 10 // Arbitrary non-zero value
		};

		Piece movingPiece   = initialState.GetPieceAt(fromPos);
		Piece capturedPiece = initialState.GetPieceAt(toPos);
		var   move          = Move.CreateCapture(fromPos, toPos, movingPiece, capturedPiece);

		// Act
		GameState newState = MoveExecution.ExecuteMove(initialState, move);

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
		var fromPos      = new Position("a2");
		var toPos        = new Position("a1");
		initialBoard[fromPos.Row, fromPos.Col] = new(PieceType.Pawn, PieceColor.Black);

		var   initialState = new GameState { PiecePositions = initialBoard, ActiveColor = PieceColor.Black };
		Piece pawn         = initialState.GetPieceAt(fromPos);
		var   move         = Move.CreateQuietPromotion(fromPos, toPos, pawn, PieceType.Queen);

		// Act
		GameState newState = MoveExecution.ExecuteMove(initialState, move);

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
		var fromPos      = new Position("e8");
		var toPos        = new Position("c8");
		initialBoard[fromPos.Row, fromPos.Col]                       = new(PieceType.King, PieceColor.Black);
		initialBoard[new Position("a8").Row, new Position("a8").Col] = new(PieceType.Rook, PieceColor.Black);

		var initialState = new GameState
		{
			PiecePositions = initialBoard, ActiveColor = PieceColor.Black, Castling = CastlingRights.BlackQueenside
		};

		Piece king = initialState.GetPieceAt(fromPos);
		var   move = Move.CreateCastleQueenside(fromPos, toPos, king);

		// Act
		GameState newState = MoveExecution.ExecuteMove(initialState, move);

		// Assert
		newState.PiecePositions[new Position("c8").Row, new Position("c8").Col].Type.Should().Be(PieceType.King);
		newState.PiecePositions[new Position("d8").Row, new Position("d8").Col].Type.Should().Be(PieceType.Rook);
		newState.PiecePositions[new Position("e8").Row, new Position("e8").Col].Type.Should().Be(PieceType.None);
		newState.PiecePositions[new Position("a8").Row, new Position("a8").Col].Type.Should().Be(PieceType.None);
		newState.Castling.Should().NotHaveFlag(CastlingRights.BlackBoth);
		newState.ActiveColor.Should().Be(PieceColor.White);
	}

	[Theory]
	[InlineData(0, 7, CastlingRights.BlackQueenside | CastlingRights.WhiteBoth)] // Black Kingside
	[InlineData(0, 0, CastlingRights.BlackKingside  | CastlingRights.WhiteBoth)] // Black Queenside
	public void ExecuteMove_BlackRookMovesFromHome_RevokesCorrectRights(
		int startRow, int startCol, CastlingRights expectedRights)
	{
		// Arrange
		_standardGame = BoardSetup.CreateStandardGameBlackStarts();
		var   from = new Position(startRow, startCol);
		Piece rook = _standardGame.GetPieceAt(from);
		var   move = Move.CreateNormal(from, new(2, startCol), rook);

		// Act
		GameState newState = MoveExecution.ExecuteMove(_standardGame, move);

		// Assert
		Assert.Equal(expectedRights, newState.Castling);
	}

	[Fact]
	public void ExecuteMove_NonKingOrRookMove_DoesNotChangeCastlingRights()
	{
		// Arrange
		var   from = new Position(6, 4);
		Piece pawn = _standardGame.GetPieceAt(from);
		var   move = Move.CreateNormal(from, new(4, 4), pawn); // White Pawn e2 to e4

		// Act
		GameState newState = MoveExecution.ExecuteMove(_standardGame, move);

		// Assert
		Assert.Equal(CastlingRights.All, newState.Castling);
	}

	[Fact]
	public void ExecuteMove_RookMovesFromNonHomeSquare_DoesNotChangeCastlingRights()
	{
		// Arrange
		// Create an initial state where the White Kingside rook has already been moved.
		// This means the corresponding castling right is already revoked.
		var piecePositions = (Piece[,])_standardGame.PiecePositions.Clone();
		var fromPos        = new Position(5, 3);
		piecePositions[fromPos.Row, fromPos.Col] = new(PieceType.Rook, PieceColor.White); // Place rook at d3
		piecePositions[7, 7]                     = new(PieceType.None, PieceColor.None);  // Empty h1

		GameState stateBeforeMove = _standardGame with
		{
			PiecePositions = piecePositions,
			// Manually set the castling rights to be consistent with the board.
			Castling = CastlingRights.All & ~CastlingRights.WhiteKingside
		};

		Piece rook = stateBeforeMove.GetPieceAt(fromPos);
		var   move = Move.CreateNormal(fromPos, new(4, 3), rook); // White Rook d3 to d4

		// Act
		GameState newState = MoveExecution.ExecuteMove(stateBeforeMove, move);

		// Assert
		// The move should not have changed the castling rights further.
		// They should remain the same as they were in stateBeforeMove.
		Assert.Equal(stateBeforeMove.Castling, newState.Castling);
	}

	[Fact]
	public void ExecuteMove_WhiteEnPassant_ShouldCapturePawn()
	{
		// Arrange
		var initialBoard = new Piece[8, 8];
		var fromPos      = new Position("e5");
		var toPos        = new Position("d6");

		// White pawn ready to capture
		initialBoard[fromPos.Row, fromPos.Col] = new(PieceType.Pawn, PieceColor.White);
		// Black pawn that just moved two squares
		var capturedPawnPos = new Position("d5");
		initialBoard[capturedPawnPos.Row, capturedPawnPos.Col] = new(PieceType.Pawn, PieceColor.Black);

		var initialState = new GameState
		{
			PiecePositions        = initialBoard,
			ActiveColor           = PieceColor.White,
			EnPassantTargetSquare = new Position("d6")
		};

		Piece movingPawn   = initialState.GetPieceAt(fromPos);
		Piece capturedPawn = initialState.GetPieceAt(capturedPawnPos);
		var   move         = Move.CreateEnPassant(fromPos, toPos, movingPawn, capturedPawn);

		// Act
		GameState newState = MoveExecution.ExecuteMove(initialState, move);

		// Assert
		newState.PiecePositions[new Position("d6").Row, new Position("d6").Col].Should()
				.Be(new Piece(PieceType.Pawn, PieceColor.White));

		newState.PiecePositions[new Position("e5").Row, new Position("e5").Col].Type.Should().Be(PieceType.None);
		// Captured pawn should be gone
		newState.PiecePositions[new Position("d5").Row, new Position("d5").Col].Type.Should().Be(PieceType.None);
		newState.ActiveColor.Should().Be(PieceColor.Black);
	}

	[Fact]
	public void ExecuteMove_WhiteKingMoves_RevokesWhiteCastlingRights()
	{
		// Arrange
		var   from = new Position(7, 4);
		Piece king = _standardGame.GetPieceAt(from);
		var   move = Move.CreateNormal(from, new(6, 4), king); // White King e1 to e2

		// Act
		GameState newState = MoveExecution.ExecuteMove(_standardGame, move);

		// Assert
		var expectedRights = CastlingRights.BlackBoth;
		Assert.Equal(expectedRights, newState.Castling);
	}

	[Fact]
	public void ExecuteMove_WhiteKingsideCastle_ShouldMoveKingAndRook()
	{
		// Arrange
		var initialBoard = new Piece[8, 8];
		var fromPos      = new Position("e1");
		var toPos        = new Position("g1");
		initialBoard[fromPos.Row, fromPos.Col]                       = new(PieceType.King, PieceColor.White);
		initialBoard[new Position("h1").Row, new Position("h1").Col] = new(PieceType.Rook, PieceColor.White);

		var initialState = new GameState
		{
			PiecePositions = initialBoard, ActiveColor = PieceColor.White, Castling = CastlingRights.WhiteKingside
		};

		Piece king = initialState.GetPieceAt(fromPos);
		var   move = Move.CreateCastleKingside(fromPos, toPos, king);

		// Act
		GameState newState = MoveExecution.ExecuteMove(initialState, move);

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
		GameState initialState = BoardSetup.CreateStandardGame();
		var       fromPos      = new Position("e2");
		var       toPos        = new Position("e4");
		Piece     pawn         = initialState.GetPieceAt(fromPos);
		var       move         = Move.CreateNormal(fromPos, toPos, pawn);

		// Act
		GameState newState = MoveExecution.ExecuteMove(initialState, move);

		// Assert
		// 1. Verify the piece moved
		Piece pawnAtE4 = newState.PiecePositions[new Position("e4").Row, new Position("e4").Col];
		pawnAtE4.Should().Be(new Piece(PieceType.Pawn, PieceColor.White));

		// 2. Verify the original square is empty
		Piece pieceAtE2 = newState.PiecePositions[new Position("e2").Row, new Position("e2").Col];
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

	[Theory]
	[InlineData(7, 7, CastlingRights.WhiteQueenside | CastlingRights.BlackBoth)] // White Kingside
	[InlineData(7, 0, CastlingRights.WhiteKingside  | CastlingRights.BlackBoth)] // White Queenside
	public void ExecuteMove_WhiteRookMovesFromHome_RevokesCorrectRights(
		int startRow, int startCol, CastlingRights expectedRights)
	{
		// Arrange
		var   from = new Position(startRow, startCol);
		Piece rook = _standardGame.GetPieceAt(from);
		var   move = Move.CreateNormal(from, new(5, startCol), rook);

		// Act
		GameState newState = MoveExecution.ExecuteMove(_standardGame, move);

		// Assert
		Assert.Equal(expectedRights, newState.Castling);
	}
}
