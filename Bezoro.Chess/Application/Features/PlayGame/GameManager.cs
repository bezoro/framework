using System;
using System.Collections.Generic;
using System.Linq;
using Bezoro.Chess.Domain;
using Bezoro.Chess.Domain.Board;
using Bezoro.Chess.Domain.Moves;
using Bezoro.Chess.Domain.Rules;

namespace Bezoro.Chess.Application.Features.PlayGame
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

		// Tracks captured pieces per colour.
		private readonly Dictionary<PieceColor, List<Piece>> _capturedPieces = new()
		{
			{ PieceColor.White, new() },
			{ PieceColor.Black, new() }
		};

		private readonly GameStatusChecker _statusChecker;
		private          int               _currentStateIndex = -1;
		private readonly List<GameState>   _gameStateHistory  = new();
		private readonly List<Move>        _moveHistory       = new();
		public IReadOnlyDictionary<PieceColor, IReadOnlyList<Piece>> CapturedPieces =>
			_capturedPieces.ToDictionary(k => k.Key, k => (IReadOnlyList<Piece>)k.Value);
		public   IReadOnlyList<Move>      MoveHistory      => _moveHistory;
		internal IReadOnlyList<GameState> GameStateHistory => _gameStateHistory;
		public   GameOutcome              Outcome          { get; private set; } = GameOutcome.None;

		public GameState CurrentState { get; private set; }

		public event Action<GameOutcome>? GameEnded;
		public event Action<GameOutcome>? GameStarted;

		public bool IsKingInCheck(GameState state, PieceColor kingColor) =>
			_statusChecker.IsKingInCheck(state, kingColor);

		public bool IsMoveLegal(Move move)
		{
			var afterMoveState = MoveExecution.ExecuteMove(CurrentState, move);
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

			// Reset state history
			_gameStateHistory.Clear();
			_gameStateHistory.Add(CurrentState);
			_currentStateIndex = 0;

			// Reset new members
			_moveHistory.Clear();
			_capturedPieces[PieceColor.White].Clear();
			_capturedPieces[PieceColor.Black].Clear();

			Outcome = GameOutcome.Ongoing;
			_statusChecker.InvalidateCache();
			GameStarted?.Invoke(Outcome);
		}

		internal void SetOutcome(GameOutcome outcome)
		{
			if (Outcome.IsFinished())
				return;

			Outcome = outcome;

			if (outcome.IsFinished())
				GameEnded?.Invoke(outcome);
		}

		private void ExecuteAndRecordMove(Move move)
		{
			// 1. make the move on the immutable board
			CurrentState = MoveExecution.ExecuteMove(CurrentState, move);

			// 2. store the capture – but only if there really was one
			if (move.CapturedPiece.Color != PieceColor.None)
			{
				_capturedPieces[move.CapturedPiece.Color].Add(move.CapturedPiece);
			}

			// 3. keep the timelines in sync
			_moveHistory.Add(move);
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
