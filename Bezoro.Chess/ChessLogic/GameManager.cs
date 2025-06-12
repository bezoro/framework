using System;
using System.Collections.Generic;
using System.Linq;
using Bezoro.Chess.ChessLogic.Generators;

namespace Bezoro.Chess.ChessLogic
{
	public sealed class GameManager
	{
		/// <summary>
		///     Creates a new game manager with a standard chess starting position.
		/// </summary>
		public GameManager()
		{
			_statusChecker = new(this);
			NewGame();
		}

		public GameManager(GameState initialState)
		{
			_statusChecker = new(this);
			CurrentState   = initialState;
			_gameStateHistory.Add(initialState);
			_currentStateIndex = 0;
		}

		private readonly GameStatusChecker        _statusChecker;
		private          int                      _currentStateIndex = -1;
		private readonly List<GameState>          _gameStateHistory  = new();
		internal         IReadOnlyList<GameState> GameStateHistory => _gameStateHistory;
		public           GameOutcome              Outcome          { get; private set; } = GameOutcome.None;

		public GameState CurrentState { get; private set; }

		public event Action<GameOutcome>? GameEnded;

		public bool IsKingInCheck(GameState state, PieceColor kingColor) =>
			_statusChecker.IsKingInCheck(state, kingColor);

		public bool IsMoveLegal(Move move)
		{
			var afterMoveState = MoveExecution.MoveExecution.ExecuteMove(CurrentState, move);
			return !IsKingInCheck(afterMoveState, CurrentState.ActiveColor);
		}

		public bool Redo()
		{
			if (_currentStateIndex >= _gameStateHistory.Count - 1)
				return false;

			_currentStateIndex++;
			CurrentState = _gameStateHistory[_currentStateIndex];
			_statusChecker.InvalidateCache();
			return true;
		}

		public bool TryMakeMove(Move move)
		{
			if (Outcome.IsFinished() || !IsMoveLegal(move))
				return false;

			HandleTrimGameStateHistory();
			ExecuteAndRecordMove(move);
			_statusChecker.CheckAndSetGameEndOutcome();
			return true;
		}

		public bool Undo()
		{
			if (_currentStateIndex <= 0)
				return false;

			_currentStateIndex--;
			CurrentState = _gameStateHistory[_currentStateIndex];
			_statusChecker.InvalidateCache();
			return true;
		}

		public IEnumerable<(Move move, bool isLegal)> GetMovesWithLegalityForPiece(Position position)
		{
			var pieceMoves = MoveGenerator.GeneratePieceMoves(position, CurrentState);
			return pieceMoves.Select(move => (move, IsMoveLegal(move)));
		}

		public IEnumerable<Move> GetLegalMoves()
		{
			var allMoves = MoveGenerator.GenerateMoves(CurrentState);
			return allMoves.Where(IsMoveLegal);
		}

		public IEnumerable<Move> GetLegalMovesForPiece(Position position)
		{
			var pieceMoves = MoveGenerator.GeneratePieceMoves(position, CurrentState);
			return pieceMoves.Where(IsMoveLegal);
		}

		public void NewGame()
		{
			CurrentState = BoardSetup.CreateStandardGame();
			_gameStateHistory.Clear();
			_gameStateHistory.Add(CurrentState);
			_currentStateIndex = 0;
			Outcome            = GameOutcome.Ongoing;
			_statusChecker.InvalidateCache();
		}

		internal void SetOutcome(GameOutcome outcome)
		{
			if (Outcome.IsFinished())
				return;

			Outcome = outcome;
			GameEnded?.Invoke(outcome);
		}

		private void ExecuteAndRecordMove(Move move)
		{
			CurrentState = MoveExecution.MoveExecution.ExecuteMove(CurrentState, move);
			_gameStateHistory.Add(CurrentState);
			_currentStateIndex++;
			_statusChecker.InvalidateCache();
		}

		private void HandleTrimGameStateHistory()
		{
			if (_currentStateIndex < _gameStateHistory.Count - 1)
				_gameStateHistory.RemoveRange(_currentStateIndex + 1, _gameStateHistory.Count - _currentStateIndex - 1);
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
