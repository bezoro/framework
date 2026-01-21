using Bezoro.Chess.Domain.Functions.Moves;
using Bezoro.Chess.Domain.Helpers;
using Bezoro.Chess.Domain.Shared.Enums;
using Bezoro.Chess.Domain.Types.Records;
using Bezoro.Chess.Domain.Types.Structs;
using FluentAssertions;
using JetBrains.Annotations;

namespace Bezoro.Chess.Tests.Domain.Unit;

[TestSubject(typeof(MoveExecution))]
public class MoveExecutionUnitTests
{
	private GameState _standardGame = BoardSetup.CreateStandardGame();

	[Fact]
	internal void ExecuteMove_BlackKingMoves_RevokesBlackCastlingRights()
	{
		// Arrange
		_standardGame = BoardSetup.CreateStandardGameBlackStarts();
		var from = new Position(0, 4);
		var king = _standardGame.GetPieceAt(from);
		var move = Move.Normal(from, new(1, 4), king); // Black King e8 to e7

		// Act
		var newState = MoveExecution.ExecuteMove(_standardGame, move);

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

		board = board.SetPiece(toPos, new(PieceType.Pawn, PieceColor.White))
					 .SetPiece(fromPos, new(PieceType.Pawn, PieceColor.Black));

		var gameState = new GameState
		{
			Board         = board,
			ActiveColor   = PieceColor.Black,
			HalfMoveClock = 10 // Arbitrary non-zero value
		};

		var movingPiece   = gameState.GetPieceAt(fromPos);
		var capturedPiece = gameState.GetPieceAt(toPos);
		var move          = Move.Capture(fromPos, toPos, movingPiece, capturedPiece);

		// Act
		var newState = MoveExecution.ExecuteMove(gameState, move);

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
		board = board.SetPiece(from, new(PieceType.Pawn, PieceColor.Black));

		var gameState = new GameState { Board = board, ActiveColor = PieceColor.Black };
		var pawn      = gameState.GetPieceAt(from);
		var move      = Move.Promotion(from, to, pawn, PromotionType.Queen);

		// Act
		gameState = MoveExecution.ExecuteMove(gameState, move);

		// Assert
		// Assuming promotion to Queen by default
		gameState.Board.GetPiece(new("a1")).Should()
				 .Be(new Piece(PieceType.Queen, PieceColor.Black));

		gameState.Board.GetPiece(new("a2")).Type.Should().Be(PieceType.None);
		gameState.ActiveColor.Should().Be(PieceColor.White);
	}

	[Theory]
	[InlineData(0, 7, CastlingRights.BlackQueenside | CastlingRights.White)] // Black Kingside
	[InlineData(0, 0, CastlingRights.BlackKingside  | CastlingRights.White)] // Black Queenside
	internal void ExecuteMove_BlackRookMovesFromHome_RevokesCorrectRights(
		int            startRow,
		int            startCol,
		CastlingRights expectedRights)
	{
		// Arrange
		_standardGame = BoardSetup.CreateStandardGameBlackStarts();
		var from = new Position(startRow, startCol);
		var rook = _standardGame.GetPieceAt(from);
		var move = Move.Normal(from, new(2, startCol), rook);

		// Act
		var newState = MoveExecution.ExecuteMove(_standardGame, move);

		// Assert
		Assert.Equal(expectedRights, newState.Castling);
	}

	[Fact]
	internal void ExecuteMove_NonKingOrRookMove_DoesNotChangeCastlingRights()
	{
		// Arrange
		var from = new Position(6, 4);
		var pawn = _standardGame.GetPieceAt(from);
		var move = Move.Normal(from, new(4, 4), pawn); // White Pawn e2 to e4

		// Act
		var newState = MoveExecution.ExecuteMove(_standardGame, move);

		// Assert
		Assert.Equal(CastlingRights.All, newState.Castling);
	}

	[Fact]
	internal void ExecuteMove_RookMovesFromNonHomeSquare_DoesNotChangeCastlingRights()
	{
		// Arrange
		// Create an initial state where the White Kingside rook has already been moved.
		// This means the corresponding castling right is already revoked.
		var board   = _standardGame.Board;
		var fromPos = new Position(5, 3);
		board.SetPiece(
			new(fromPos.Row, fromPos.Col),
			new(PieceType.Rook, PieceColor.White)); // Place rook at d3

		board.SetPiece(new(7, 7), new(PieceType.None, PieceColor.None)); // Empty h1

		var stateBeforeMove = _standardGame with
		{
			Board = board,
			// Manually set the castling rights to be consistent with the board.
			Castling = CastlingRights.All & ~CastlingRights.WhiteKingside
		};

		var rook = stateBeforeMove.GetPieceAt(fromPos);
		var move = Move.Normal(fromPos, new(4, 3), rook); // White Rook d3 to d4

		// Act
		var newState = MoveExecution.ExecuteMove(stateBeforeMove, move);

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
		board = board.SetPiece(startPos, new(PieceType.Pawn, PieceColor.White));
		// Black pawn that just moved two squares 
		var capturedPawnPos = new Position("d5");
		board = board.SetPiece(capturedPawnPos, new(PieceType.Pawn, PieceColor.Black));

		var gameState = new GameState
		{
			Board                 = board,
			ActiveColor           = PieceColor.White,
			EnPassantTargetSquare = enPassantPos
		};

		var movingPawn   = gameState.GetPieceAt(startPos);
		var capturedPawn = gameState.GetPieceAt(capturedPawnPos);
		var move         = Move.EnPassant(startPos, enPassantPos, movingPawn, capturedPawn);

		// Act
		var newState = MoveExecution.ExecuteMove(gameState, move);

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
		var from = new Position(7, 4);
		var king = _standardGame.GetPieceAt(from);
		var move = Move.Normal(from, new(6, 4), king); // White King e1 to e2

		// Act
		var newState = MoveExecution.ExecuteMove(_standardGame, move);

		// Assert
		var expectedRights = CastlingRights.Black;
		Assert.Equal(expectedRights, newState.Castling);
	}

	[Fact]
	internal void ExecuteMove_WhitePawnE2ToE4_ShouldUpdateStateCorrectly()
	{
		// Arrange
		var initialState = BoardSetup.CreateStandardGame();
		var fromPos      = new Position("e2");
		var toPos        = new Position("e4");
		var pawn         = initialState.GetPieceAt(fromPos);
		var move         = Move.Normal(fromPos, toPos, pawn);

		// Act
		var newState = MoveExecution.ExecuteMove(initialState, move);

		// Assert
		// 1. Verify the piece moved
		newState.Board.GetPiece(new("e4")).Should().Be(new Piece(PieceType.Pawn, PieceColor.White));

		// 2. Verify the original square is empty
		newState.Board.GetPiece(new("e2")).Type.Should().Be(PieceType.None);

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
		int            startRow,
		int            startCol,
		CastlingRights expectedRights)
	{
		// Arrange
		var from = new Position(startRow, startCol);
		var rook = _standardGame.GetPieceAt(from);
		var move = Move.Normal(from, new(5, startCol), rook);

		// Act
		var newState = MoveExecution.ExecuteMove(_standardGame, move);

		// Assert
		Assert.Equal(expectedRights, newState.Castling);
	}
}
