using Bezoro.Chess.Domain.Functions.Moves;
using Bezoro.Chess.Domain.Helpers;
using Bezoro.Chess.Domain.Shared.Enums;
using Bezoro.Chess.Domain.Types.Records;
using Bezoro.Chess.Domain.Types.Structs;
using FluentAssertions;
using JetBrains.Annotations;

namespace Bezoro.Chess.Tests.Unit;

[TestSubject(typeof(MoveExecution))]
public class MoveExecutionUnitTests
{
	private GameState _standardGame = BoardSetup.CreateStandardGame();

	[Fact]
	internal void ExecuteMove_BlackKingMoves_RevokesBlackCastlingRights()
	{
		// Arrange
		_standardGame = BoardSetup.CreateStandardGameBlackStarts();
		var   from = new Position(0, 4);
		Piece king = _standardGame.GetPieceAt(from);
		Move  move = Move.Normal(from, new Position(1, 4), king); // Black King e8 to e7

		// Act
		GameState newState = MoveExecution.ExecuteMove(_standardGame, move);

		// Assert
		var expectedRights = CastlingRights.White;
		Assert.Equal(expectedRights, newState.Castling);
	}

	[Fact]
	internal void ExecuteMove_BlackPawnCapture_ShouldResetHalfMoveClock()
	{
		// Arrange
		var board   = new Board(BoardFactory.CreateEmptyBitboards());
		var fromPos = new Position("e5");
		var toPos   = new Position("d4");

		board = board.SetPiece(toPos, new Piece(PieceType.Pawn,   PieceColor.White))
					 .SetPiece(fromPos, new Piece(PieceType.Pawn, PieceColor.Black));

		var gameState = new GameState
		{
			Board         = board,
			ActiveColor   = PieceColor.Black,
			HalfMoveClock = 10 // Arbitrary non-zero value
		};

		Piece movingPiece   = gameState.GetPieceAt(fromPos);
		Piece capturedPiece = gameState.GetPieceAt(toPos);
		Move  move          = Move.Capture(fromPos, toPos, movingPiece, capturedPiece);

		// Act
		GameState newState = MoveExecution.ExecuteMove(gameState, move);

		// Assert
		newState.Board.GetPiece(toPos).Should()
				.Be(new Piece(PieceType.Pawn, PieceColor.Black));

		newState.Board.GetPiece(fromPos).Type.Should().Be(PieceType.None);
		newState.HalfMoveClock.Should().Be(0);
		newState.ActiveColor.Should().Be(PieceColor.White);
		newState.FullMoveNumber.Should().Be(2); // Increments after black's move
	}

	[Fact]
	internal void ExecuteMove_BlackPawnPromotesToQueen_ShouldReplacePawn()
	{
		// Arrange
		var board = new Board(BoardFactory.CreateEmptyBitboards());
		var from  = new Position("a2");
		var to    = new Position("a1");
		board = board.SetPiece(from, new Piece(PieceType.Pawn, PieceColor.Black));

		var   gameState = new GameState { Board = board, ActiveColor = PieceColor.Black };
		Piece pawn      = gameState.GetPieceAt(from);
		Move  move      = Move.Promotion(from, to, pawn, PromotionType.Queen);

		// Act
		gameState = MoveExecution.ExecuteMove(gameState, move);

		// Assert
		// Assuming promotion to Queen by default
		gameState.Board.GetPiece(new Position("a1")).Should()
				 .Be(new Piece(PieceType.Queen, PieceColor.Black));

		gameState.Board.GetPiece(new Position("a2")).Type.Should().Be(PieceType.None);
		gameState.ActiveColor.Should().Be(PieceColor.White);
	}

	[Fact]
	internal void ExecuteMove_BlackQueensideCastle_ShouldMoveKingAndRook()
	{
		// Arrange
		var board   = new Board(BoardFactory.CreateEmptyBitboards());
		var fromPos = new Position("e8");
		var toPos   = new Position("c8");
		board = board.SetPiece(fromPos, new Piece(PieceType.King,            PieceColor.Black))
					 .SetPiece(new Position("a8"), new Piece(PieceType.Rook, PieceColor.Black));

		var gameState = new GameState
		{
			Board       = board,
			ActiveColor = PieceColor.Black,
			Castling    = CastlingRights.BlackQueenside
		};

		Piece king = gameState.GetPieceAt(fromPos);
		Move  move = Move.CastleQueenside(fromPos, toPos, king);

		// Act
		gameState = MoveExecution.ExecuteMove(gameState, move);

		// Assert
		gameState.Board.GetPiece(new Position("c8")).Type.Should().Be(PieceType.King);
		gameState.Board.GetPiece(new Position("d8")).Type.Should().Be(PieceType.Rook);
		gameState.Board.GetPiece(new Position("e8")).Type.Should().Be(PieceType.None);
		gameState.Board.GetPiece(new Position("a8")).Type.Should().Be(PieceType.None);
		gameState.Castling.Should().NotHaveFlag(CastlingRights.Black);
		gameState.ActiveColor.Should().Be(PieceColor.White);
	}

	[Theory]
	[InlineData(0, 7, CastlingRights.BlackQueenside | CastlingRights.White)] // Black Kingside
	[InlineData(0, 0, CastlingRights.BlackKingside  | CastlingRights.White)] // Black Queenside
	internal void ExecuteMove_BlackRookMovesFromHome_RevokesCorrectRights(
		int startRow, int startCol, CastlingRights expectedRights)
	{
		// Arrange
		_standardGame = BoardSetup.CreateStandardGameBlackStarts();
		var   from = new Position(startRow, startCol);
		Piece rook = _standardGame.GetPieceAt(from);
		Move  move = Move.Normal(from, new Position(2, startCol), rook);

		// Act
		GameState newState = MoveExecution.ExecuteMove(_standardGame, move);

		// Assert
		Assert.Equal(expectedRights, newState.Castling);
	}

	[Fact]
	internal void ExecuteMove_NonKingOrRookMove_DoesNotChangeCastlingRights()
	{
		// Arrange
		var   from = new Position(6, 4);
		Piece pawn = _standardGame.GetPieceAt(from);
		Move  move = Move.Normal(from, new Position(4, 4), pawn); // White Pawn e2 to e4

		// Act
		GameState newState = MoveExecution.ExecuteMove(_standardGame, move);

		// Assert
		Assert.Equal(CastlingRights.All, newState.Castling);
	}

	[Fact]
	internal void ExecuteMove_RookMovesFromNonHomeSquare_DoesNotChangeCastlingRights()
	{
		// Arrange
		// Create an initial state where the White Kingside rook has already been moved.
		// This means the corresponding castling right is already revoked.
		Board board   = _standardGame.Board;
		var   fromPos = new Position(5, 3);
		board.SetPiece(new Position(fromPos.Row, fromPos.Col),
			new Piece(PieceType.Rook, PieceColor.White)); // Place rook at d3

		board.SetPiece(new Position(7, 7), new Piece(PieceType.None, PieceColor.None)); // Empty h1

		GameState stateBeforeMove = _standardGame with
		{
			Board = board,
			// Manually set the castling rights to be consistent with the board.
			Castling = CastlingRights.All & ~CastlingRights.WhiteKingside
		};

		Piece rook = stateBeforeMove.GetPieceAt(fromPos);
		Move  move = Move.Normal(fromPos, new Position(4, 3), rook); // White Rook d3 to d4

		// Act
		GameState newState = MoveExecution.ExecuteMove(stateBeforeMove, move);

		// Assert
		// The move should not have changed the castling rights further.
		// They should remain the same as they were in stateBeforeMove.
		Assert.Equal(stateBeforeMove.Castling, newState.Castling);
	}

	[Fact]
	internal void ExecuteMove_WhiteEnPassant_ShouldCapturePawn()
	{
		// Arrange
		var board        = new Board(BoardFactory.CreateEmptyBitboards());
		var startPos     = new Position("e5");
		var enPassantPos = new Position("d6");

		// White pawn ready to capture
		board = board.SetPiece(startPos, new Piece(PieceType.Pawn, PieceColor.White));
		// Black pawn that just moved two squares 
		var capturedPawnPos = new Position("d5");
		board = board.SetPiece(capturedPawnPos, new Piece(PieceType.Pawn, PieceColor.Black));

		var gameState = new GameState
		{
			Board                 = board,
			ActiveColor           = PieceColor.White,
			EnPassantTargetSquare = enPassantPos
		};

		Piece movingPawn   = gameState.GetPieceAt(startPos);
		Piece capturedPawn = gameState.GetPieceAt(capturedPawnPos);
		Move  move         = Move.EnPassant(startPos, enPassantPos, movingPawn, capturedPawn);

		// Act
		GameState newState = MoveExecution.ExecuteMove(gameState, move);

		// Assert
		newState.Board.GetPiece(enPassantPos).Should()
				.Be(new Piece(PieceType.Pawn, PieceColor.White));

		newState.Board.GetPiece(startPos).Type.Should().Be(PieceType.None);
		// Captured pawn should be gone
		newState.Board.GetPiece(capturedPawnPos).Type.Should().Be(PieceType.None);
		newState.ActiveColor.Should().Be(PieceColor.Black);
	}

	[Fact]
	internal void ExecuteMove_WhiteKingMoves_RevokesWhiteCastlingRights()
	{
		// Arrange
		var   from = new Position(7, 4);
		Piece king = _standardGame.GetPieceAt(from);
		Move  move = Move.Normal(from, new Position(6, 4), king); // White King e1 to e2

		// Act
		GameState newState = MoveExecution.ExecuteMove(_standardGame, move);

		// Assert
		var expectedRights = CastlingRights.Black;
		Assert.Equal(expectedRights, newState.Castling);
	}

	[Fact]
	internal void ExecuteMove_WhiteKingsideCastle_ShouldMoveKingAndRook()
	{
		// Arrange
		var board = new Board(BoardFactory.CreateEmptyBitboards());

		var kingStartPos = new Position("e1");
		var kingEndPos   = new Position("g1");

		var rookStartPos = new Position("h1");
		var rookEndPos   = new Position("f1");

		board = board.SetPiece(kingStartPos, new Piece(PieceType.King, PieceColor.White))
					 .SetPiece(rookStartPos, new Piece(PieceType.Rook, PieceColor.White));

		var gameState = new GameState
		{
			Board = board
		};

		Piece king = gameState.GetPieceAt(kingStartPos);
		Move  move = Move.CastleKingside(kingStartPos, kingEndPos, king);

		// Act
		gameState = MoveExecution.ExecuteMove(gameState, move);

		// Assert
		gameState.Board.GetPiece(new Position("g1")).Should().Be(new Piece(PieceType.King, PieceColor.White));
		gameState.Board.GetPiece(new Position("f1")).Should().Be(new Piece(PieceType.Rook, PieceColor.White));

		gameState.Board.GetPiece(new Position("e1")).Type.Should().Be(PieceType.None);
		gameState.Board.GetPiece(new Position("h1")).Type.Should().Be(PieceType.None);
	}

	[Fact]
	internal void ExecuteMove_WhitePawnE2ToE4_ShouldUpdateStateCorrectly()
	{
		// Arrange
		GameState initialState = BoardSetup.CreateStandardGame();
		var       fromPos      = new Position("e2");
		var       toPos        = new Position("e4");
		Piece     pawn         = initialState.GetPieceAt(fromPos);
		Move      move         = Move.Normal(fromPos, toPos, pawn);

		// Act
		GameState newState = MoveExecution.ExecuteMove(initialState, move);

		// Assert
		// 1. Verify the piece moved
		newState.Board.GetPiece(new Position("e4")).Should().Be(new Piece(PieceType.Pawn, PieceColor.White));

		// 2. Verify the original square is empty
		newState.Board.GetPiece(new Position("e2")).Type.Should().Be(PieceType.None);

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
	[InlineData(7, 7, CastlingRights.WhiteQueenside | CastlingRights.Black)] // White Kingside
	[InlineData(7, 0, CastlingRights.WhiteKingside  | CastlingRights.Black)] // White Queenside
	internal void ExecuteMove_WhiteRookMovesFromHome_RevokesCorrectRights(
		int startRow, int startCol, CastlingRights expectedRights)
	{
		// Arrange
		var   from = new Position(startRow, startCol);
		Piece rook = _standardGame.GetPieceAt(from);
		Move  move = Move.Normal(from, new Position(5, startCol), rook);

		// Act
		GameState newState = MoveExecution.ExecuteMove(_standardGame, move);

		// Assert
		Assert.Equal(expectedRights, newState.Castling);
	}
}
