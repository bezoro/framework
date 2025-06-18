using Bezoro.Chess.Application.Features.PlayGame;
using Bezoro.Chess.Domain;
using Bezoro.Chess.Domain.Board;
using Bezoro.Chess.Domain.Moves;

namespace Bezoro.Chess.Tests.Unit;

public class GameManagerUnitTests
{
	[Fact]
	public void Forfeit_WhenBlackToMove_WhiteWins()
	{
		// Arrange
		var gameManager = new GameManager();
		var move        = Move.CreateNormal(new(6, 4), new(4, 4), gameManager.CurrentState.GetPieceAt(new(6, 4)));
		gameManager.TryMakeMove(move);
		Assert.Equal(PieceColor.Black, gameManager.CurrentState.ActiveColor);

		// Act
		gameManager.Forfeit();

		// Assert
		Assert.Equal(GameOutcome.WhiteWin, gameManager.Outcome);
	}

	[Fact]
	public void Forfeit_WhenGameIsAlreadyOver_DoesNothing()
	{
		// Arrange
		var gameManager = new GameManager();
		gameManager.Forfeit();
		GameOutcome firstOutcome = gameManager.Outcome;

		// Act
		gameManager.Forfeit();

		// Assert
		Assert.Equal(firstOutcome, gameManager.Outcome);
	}

	[Fact]
	public void Forfeit_WhenGameIsOngoing_RaisesGameEndedEvent()
	{
		// Arrange
		var gameManager  = new GameManager();
		var eventOutcome = GameOutcome.None;
		gameManager.GameEnded += outcome => { eventOutcome = outcome; };

		// Act
		gameManager.Forfeit();

		// Assert
		Assert.Equal(GameOutcome.BlackWin, eventOutcome);
	}

	[Fact]
	public void Forfeit_WhenWhiteToMove_BlackWins()
	{
		// Arrange
		var gameManager = new GameManager();
		Assert.Equal(PieceColor.White, gameManager.CurrentState.ActiveColor);

		// Act
		gameManager.Forfeit();

		// Assert
		Assert.Equal(GameOutcome.BlackWin, gameManager.Outcome);
	}

	[Fact]
	public void GetLegalMovesForPiece_ForStartingPawn_ShouldReturnTwoMoves()
	{
		// Arrange
		var gameManager  = new GameManager();  // Standard setup
		var pawnPosition = new Position(6, 4); // White e2 pawn

		// Act
		List<Move> legalMoves = gameManager.GetLegalMovesForPiece(pawnPosition).ToList();

		// Assert
		Assert.Equal(2, legalMoves.Count);
		Assert.Contains(legalMoves, move => move.To.Equals(new(5, 4))); // e3
		Assert.Contains(legalMoves, move => move.To.Equals(new(4, 4))); // e4
	}

	[Fact]
	public void GetLegalMovesForPiece_ShouldOnlyReturnMovesThatDoNotLeaveKingInCheck()
	{
		// Arrange
		var board = new Piece[8, 8];
		board[0, 4] = new(PieceType.King, PieceColor.White);
		board[1, 3] = new(PieceType.Rook, PieceColor.White); // Rook that can move, but not horizontally
		board[0, 0] = new(PieceType.Rook, PieceColor.Black); // Attacking rook
		var gameState    = new GameState { PiecePositions = board, ActiveColor = PieceColor.White };
		var gameManager  = new GameManager(gameState);
		var rookPosition = new Position(1, 3);

		// Act
		List<Move> legalMoves = gameManager.GetLegalMovesForPiece(rookPosition).ToList();

		// Assert
		// The rook at (1,3) can generate many moves, but only moving it vertically along the d-file is legal.
		// Any horizontal move would expose the king at (0,4) to the rook at (0,0).
		Assert.NotEmpty(legalMoves);
		Assert.All(legalMoves, move => Assert.Equal(rookPosition.Col, move.To.Col));
	}

	[Fact]
	public void GetLegalMovesForPiece_WhenPieceIsPinned_ShouldReturnNoMoves()
	{
		// Arrange
		var board = new Piece[8, 8];
		board[0, 4] = new(PieceType.King, PieceColor.White);
		board[1, 4] = new(PieceType.Knight, PieceColor.White); // Pinned knight
		board[7, 4] = new(PieceType.Rook, PieceColor.Black);   // Pinning rook
		var gameState      = new GameState { PiecePositions = board, ActiveColor = PieceColor.White };
		var gameManager    = new GameManager(gameState);
		var knightPosition = new Position(1, 4);

		// Act
		IEnumerable<Move> legalMoves = gameManager.GetLegalMovesForPiece(knightPosition);

		// Assert
		Assert.Empty(legalMoves);
	}

	[Fact]
	public void Redo_AfterNewMoveIsMade_ClearsRedoHistory()
	{
		// Arrange
		var gameManager = new GameManager();
		// Move 1: e4
		var   from1  = new Position(6, 4);
		var   to1    = new Position(4, 4);
		Piece piece1 = gameManager.CurrentState.GetPieceAt(from1);
		var   move1  = Move.CreateNormal(from1, to1, piece1);
		gameManager.TryMakeMove(move1);
		// Move 2: e5
		var   from2  = new Position(1, 4);
		var   to2    = new Position(3, 4);
		Piece piece2 = gameManager.CurrentState.GetPieceAt(from2);
		var   move2  = Move.CreateNormal(from2, to2, piece2);
		gameManager.TryMakeMove(move2);

		// Undo move 2
		gameManager.Undo();

		// Move 3 (different from move 2): d4
		var   from3  = new Position(6, 3);
		var   to3    = new Position(4, 3);
		Piece piece3 = gameManager.CurrentState.GetPieceAt(from3);
		var   move3  = Move.CreateNormal(from3, to3, piece3);
		gameManager.TryMakeMove(move3);

		// Act
		bool result = gameManager.Redo();

		// Assert
		Assert.False(result, "Redo should fail because the history was trimmed after the new move.");
	}

	[Fact]
	public void Redo_AfterOneUndo_ReturnsTrueAndRestoresState()
	{
		// Arrange
		var   gameManager = new GameManager();
		var   from        = new Position(6, 4);
		var   to          = new Position(4, 4);
		Piece piece       = gameManager.CurrentState.GetPieceAt(from);
		var   move        = Move.CreateNormal(from, to, piece);
		gameManager.TryMakeMove(move);
		GameState stateAfterMove = gameManager.CurrentState;
		gameManager.Undo();

		// Act
		bool result = gameManager.Redo();

		// Assert
		Assert.True(result);
		Assert.Equal(stateAfterMove, gameManager.CurrentState);
	}

	[Fact]
	public void Redo_WithNoUndoneMoves_ReturnsFalse()
	{
		// Arrange
		var   gameManager = new GameManager();
		var   from        = new Position(6, 4);
		var   to          = new Position(4, 4);
		Piece piece       = gameManager.CurrentState.GetPieceAt(from);
		var   move        = Move.CreateNormal(from, to, piece);
		gameManager.TryMakeMove(move);
		GameState stateAfterMove = gameManager.CurrentState;

		// Act
		bool result = gameManager.Redo();

		// Assert
		Assert.False(result);
		Assert.Equal(stateAfterMove, gameManager.CurrentState);
	}

	[Fact]
	public void Undo_AfterOneMove_ReturnsTrueAndRestoresPreviousState()
	{
		// Arrange
		var       gameManager  = new GameManager();
		GameState initialState = gameManager.CurrentState;

		// A simple valid move, e.g., e4
		var   from  = new Position(6, 4);
		var   to    = new Position(4, 4);
		Piece piece = gameManager.CurrentState.GetPieceAt(from);
		var   move  = Move.CreateNormal(from, to, piece);

		gameManager.TryMakeMove(move);
		GameState stateAfterMove = gameManager.CurrentState;

		// Act
		bool result = gameManager.Undo();

		// Assert
		Assert.True(result);
		Assert.NotEqual(stateAfterMove, gameManager.CurrentState);
		Assert.Equal(initialState, gameManager.CurrentState);
	}

	[Fact]
	public void Undo_OnNewGame_ReturnsFalse()
	{
		// Arrange
		var       gameManager  = new GameManager();
		GameState initialState = gameManager.CurrentState;

		// Act
		bool result = gameManager.Undo();

		// Assert
		Assert.False(result);
		Assert.Equal(initialState, gameManager.CurrentState);
	}

	[Fact]
	public void Undo_PastInitialState_ReturnsFalse()
	{
		// Arrange
		var gameManager = new GameManager();

		var   from1  = new Position(6, 4); // e4
		var   to1    = new Position(4, 4);
		Piece piece1 = gameManager.CurrentState.GetPieceAt(from1);
		var   move1  = Move.CreateNormal(from1, to1, piece1);
		gameManager.TryMakeMove(move1);

		var   from2  = new Position(1, 4); // e5
		var   to2    = new Position(3, 4);
		Piece piece2 = gameManager.CurrentState.GetPieceAt(from2);
		var   move2  = Move.CreateNormal(from2, to2, piece2);
		gameManager.TryMakeMove(move2);

		// Act
		gameManager.Undo();                        // Undo move 2
		gameManager.Undo();                        // Undo move 1
		bool finalUndoResult = gameManager.Undo(); // Attempt to undo past the start

		// Assert
		Assert.False(finalUndoResult);
	}
}
