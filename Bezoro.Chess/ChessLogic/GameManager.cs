using System;
using System.Collections.Generic;
using System.Linq;
using Bezoro.Chess.ChessLogic.Generators;

namespace Bezoro.Chess.ChessLogic
{
	/// <summary>
	///     Manages the current state of a chess game and provides methods to interact with it.
	///     This class is the main entry point for the chess engine.
	/// </summary>
	public class GameManager
	{
		/// <summary>
		///     Creates a new game manager with a standard chess starting position.
		/// </summary>
		public GameManager()
		{
			NewGame();
		}

		/// <summary>
		///     Creates a new game manager with a custom starting position.
		/// </summary>
		public GameManager(GameState initialState)
		{
			CurrentState = initialState;
			_gameStateHistory.Add(initialState);
			_currentStateIndex = 0;
		}

		/// <summary>
		///     The index of the current game state in the history.
		/// </summary>
		private int _currentStateIndex = -1;

		/// <summary>
		///     The history of all game states since the game began.
		///     This allows for undo/redo functionality.
		/// </summary>
		private readonly List<GameState> _gameStateHistory = new();
		/// <summary>
		///     The current state of the game.
		/// </summary>
		public GameState CurrentState { get; private set; }

		/// <summary>
		///     Determines if the current player is in checkmate.
		/// </summary>
		public bool IsCheckmate()
		{
			// If the king is not in check, it can't be checkmate
			if (!IsKingInCheck(CurrentState, CurrentState.ActiveColor))
			{
				return false;
			}

			// If any legal move exists, it's not checkmate
			return !GetLegalMoves().Any();
		}

		/// <summary>
		///     Determines if a king of the specified color is in check.
		/// </summary>
		public bool IsKingInCheck(GameState state, PieceColor kingColor)
		{
			var kingPosition = state.FindKingPosition(kingColor);

			if (!kingPosition.HasValue)
			{
				throw new InvalidOperationException($"No {kingColor} king found on the board");
			}

			var opponentColor = kingColor.Opposite();
			return state.IsSquareAttackedBy(kingPosition.Value, opponentColor);
		}

		/// <summary>
		///     Determines if a move is legal (doesn't leave the king in check).
		/// </summary>
		public bool IsMoveLegal(Move move)
		{
			// Execute the move to see if it would leave the king in check
			var afterMoveState = MoveExecution.MoveExecution.ExecuteMove(CurrentState, move);

			// The move has already switched the active player, so we need to check if the
			// previous player's king is in check in the new state
			return !IsKingInCheck(afterMoveState, CurrentState.ActiveColor);
		}

		/// <summary>
		///     Determines if the current game state is a stalemate.
		/// </summary>
		public bool IsStalemate()
		{
			// If the king is in check, it's not stalemate
			if (IsKingInCheck(CurrentState, CurrentState.ActiveColor))
			{
				return false;
			}

			// If any legal move exists, it's not stalemate
			return !GetLegalMoves().Any();
		}

		/// <summary>
		///     Redoes the last undone move, if possible.
		/// </summary>
		public bool Redo()
		{
			if (_currentStateIndex >= _gameStateHistory.Count - 1)
			{
				return false; // Nothing to redo
			}

			_currentStateIndex++;
			CurrentState = _gameStateHistory[_currentStateIndex];
			return true;
		}

		/// <summary>
		///     Tries to make a move. Returns true if the move was successful.
		/// </summary>
		public bool TryMakeMove(Move move)
		{
			// Verify that the move is legal
			if (!IsMoveLegal(move))
			{
				return false;
			}

			// Execute the move
			var newState = MoveExecution.MoveExecution.ExecuteMove(CurrentState, move);

			// If we're not at the end of the history (i.e., we've done some undos),
			// remove all future states
			if (_currentStateIndex < _gameStateHistory.Count - 1)
			{
				_gameStateHistory.RemoveRange(
					_currentStateIndex      + 1,
					_gameStateHistory.Count - _currentStateIndex - 1);
			}

			// Add the new state to the history
			_gameStateHistory.Add(newState);
			_currentStateIndex++;

			// Update the current state
			CurrentState = newState;

			return true;
		}

		/// <summary>
		///     Undoes the last move, if possible.
		/// </summary>
		public bool Undo()
		{
			if (_currentStateIndex <= 0)
			{
				return false; // Nothing to undo
			}

			_currentStateIndex--;
			CurrentState = _gameStateHistory[_currentStateIndex];
			return true;
		}

		/// <summary>
		///     Returns a list of all legal moves for the active player.
		/// </summary>
		public IEnumerable<Move> GetLegalMoves()
		{
			var allMoves = MoveGenerator.GenerateMoves(CurrentState).ToList();
			return allMoves.Where(IsMoveLegal);
		}

		/// <summary>
		///     Returns a list of legal moves for a specific piece at the given position.
		/// </summary>
		public IEnumerable<Move> GetLegalMovesForPiece(Position position)
		{
			var pieceMoves = MoveGenerator.GeneratePieceMoves(position, CurrentState).ToList();
			return pieceMoves.Where(IsMoveLegal);
		}

		/// <summary>
		///     Resets the game to the standard starting position.
		/// </summary>
		public void NewGame()
		{
			CurrentState = BoardSetup.CreateStandardGame();
			_gameStateHistory.Clear();
			_gameStateHistory.Add(CurrentState);
			_currentStateIndex = 0;
		}
	}
}
