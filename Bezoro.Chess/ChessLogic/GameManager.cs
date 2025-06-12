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

		public GameOutcome Outcome { get; private set; } = GameOutcome.None;
		/// <summary>
		///     The current state of the game.
		/// </summary>
		public GameState CurrentState { get; private set; }

		public event Action<GameOutcome>? GameEnded;

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
		///     True if the current position is a draw by the fifty-move rule.
		/// </summary>
		public bool IsDrawByFiftyMoveRule() =>
			CurrentState.HalfMoveClock >= 100; // 100 half-moves == 50 full moves

		/// <summary>
		///     Determines if the game is a draw due to insufficient material to checkmate.
		///     Covers K vs K, K+B vs K, K+N vs K, and K+B vs K+B (same color bishops).
		/// </summary>
		public bool IsDrawByInsufficientMaterial()
		{
			var pieces = CurrentState.PiecePositions.Cast<Piece>()
									 .Where(p => p.Type is not PieceType.None and not PieceType.King)
									 .ToList();

			// Any pawns, rooks, or queens? If so, mate is possible.
			if (pieces.Any(p => p.Type is PieceType.Pawn or PieceType.Rook or PieceType.Queen))
			{
				return false;
			}

			// At this point, we only have knights, bishops, or nothing.
			// K vs K, K+N vs K, K+B vs K are all draws.
			if (pieces.Count <= 1)
				return true;

			// If we have exactly two pieces, and they are two bishops on same-colored squares.
			if (pieces.Count != 2 || pieces.Any(p => p.Type != PieceType.Bishop))
				return false;

			var bishopPositions = new List<Position>();
			for (var r = 0 ; r < 8 ; r++)
			{
				for (var c = 0 ; c < 8 ; c++)
				{
					if (CurrentState.PiecePositions[r, c].Type == PieceType.Bishop)
					{
						bishopPositions.Add(new(r, c));
					}
				}
			}

			// Bishops are on the same color square if the sum of their coordinates has the same parity.
			var (pos1, pos2) = (bishopPositions[0], bishopPositions[1]);
			return (pos1.Row + pos1.Col) % 2 == (pos2.Row + pos2.Col) % 2;
		}

		/// <summary>
		///     Determines if the game is a draw by threefold repetition.
		/// </summary>
		public bool IsDrawByThreefoldRepetition() =>
			// The current game state has appeared 3 or more times.
			// GameState is a record, so equality is based on its values (positions, turn, castling, en passant).
			_gameStateHistory.Count(state => state.Equals(CurrentState)) >= 3;

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

		public bool TryMakeMove(Move move)
		{
			if (Outcome.IsFinished() || !IsMoveLegal(move))
				return false;

			HandleTrimGameStateHistory();
			ExecuteAndRecordMove(move);
			HandleGameEndConditions();
			return true;
		}

		/// <summary>
		///     Undoes the last move, if possible.
		/// </summary>
		public bool Undo()
		{
			if (_currentStateIndex <= 0)
				return false; // Nothing to undo

			_currentStateIndex--;
			CurrentState = _gameStateHistory[_currentStateIndex];
			return true;
		}

		/// <summary>
		///     Returns all pseudo-legal moves for a piece, marking each with its legality.
		///     This is ideal for UI that needs to visually distinguish between legal and illegal moves.
		/// </summary>
		public IEnumerable<(Move move, bool isLegal)> GetMovesWithLegalityForPiece(Position position)
		{
			var pieceMoves = MoveGenerator.GeneratePieceMoves(position, CurrentState);
			return pieceMoves.Select(move => (move, IsMoveLegal(move)));
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
			Outcome            = GameOutcome.Ongoing;
		}

		private void ExecuteAndRecordMove(Move move)
		{
			CurrentState = MoveExecution.MoveExecution.ExecuteMove(CurrentState, move);
			_gameStateHistory.Add(CurrentState);
			_currentStateIndex++;
		}

		private void HandleGameEndConditions()
		{
			if (IsCheckmate())
				SetOutcome(CurrentState.ActiveColor == PieceColor.White ? GameOutcome.BlackWin : GameOutcome.WhiteWin);
			else if (IsStalemate())
				SetOutcome(GameOutcome.DrawStalemate);
			else if (IsDrawByThreefoldRepetition())
				SetOutcome(GameOutcome.DrawThreefold);
			else if (IsDrawByInsufficientMaterial())
				SetOutcome(GameOutcome.DrawInsufficientMaterial);
			else if (IsDrawByFiftyMoveRule())
				SetOutcome(GameOutcome.DrawFiftyMoves);
		}

		private void HandleTrimGameStateHistory()
		{
			if (_currentStateIndex < _gameStateHistory.Count - 1)
				_gameStateHistory.RemoveRange(_currentStateIndex + 1, _gameStateHistory.Count - _currentStateIndex - 1);
		}

		private void SetOutcome(GameOutcome outcome)
		{
			if (Outcome.IsFinished())
				return;

			Outcome = outcome;
			GameEnded?.Invoke(outcome);
		}
	}

	public enum GameOutcome : byte
	{
		None,    // Still not begun
		Ongoing, // Game is still in progress
		WhiteWin,
		BlackWin,
		DrawStalemate,
		DrawFiftyMoves,
		DrawThreefold,
		DrawInsufficientMaterial
	}
}
