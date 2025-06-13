using System;
using System.Collections.Generic;
using System.Linq;
using Bezoro.Chess.Application.Features.PlayGame;
using Bezoro.Chess.Domain.Board;

namespace Bezoro.Chess.Domain.Rules
{
	/// <summary>
	///     Evaluates the game state for terminal conditions like checkmate, stalemate, and various draws.
	/// </summary>
	internal class GameStatusChecker
	{
		public GameStatusChecker(GameManager gameManager)
		{
			_gameManager = gameManager;
		}

		/// <summary>
		///     Caches the result of expensive end-of-game calculations.
		///     Invalidated whenever the game state changes.
		/// </summary>
		private (bool hasLegalMoves, bool isKingInCheck)? _gameStatusCache;
		private readonly GameManager _gameManager;

		public bool IsCheckmate()
		{
			var (hasLegalMoves, isKingInCheck) = GetGameStatus();
			return !hasLegalMoves && isKingInCheck;
		}

		public bool IsDrawByFiftyMoveRule() =>
			_gameManager.CurrentState.HalfMoveClock >= 100;

		public bool IsDrawByInsufficientMaterial()
		{
			var pieces = _gameManager.CurrentState.PiecePositions.Cast<Piece>()
									 .Where(p => p.Type is not PieceType.None and not PieceType.King)
									 .ToList();

			if (pieces.Any(p => p.Type is PieceType.Pawn or PieceType.Rook or PieceType.Queen))
				return false;

			if (pieces.Count <= 1)
				return true;

			if (pieces.Count != 2 || pieces.Any(p => p.Type != PieceType.Bishop))
				return false;

			var bishopPositions = new List<Position>();
			for (var r = 0 ; r < 8 ; r++)
			{
				for (var c = 0 ; c < 8 ; c++)
				{
					if (_gameManager.CurrentState.PiecePositions[r, c].Type == PieceType.Bishop)
						bishopPositions.Add(new(r, c));
				}
			}

			var (pos1, pos2) = (bishopPositions[0], bishopPositions[1]);
			return (pos1.Row + pos1.Col) % 2 == (pos2.Row + pos2.Col) % 2;
		}

		public bool IsDrawByThreefoldRepetition() =>
			_gameManager.GameStateHistory.Count(state => state.Equals(_gameManager.CurrentState)) >= 3;

		public bool IsKingInCheck(GameState state, PieceColor kingColor)
		{
			var kingPosition = state.FindKingPosition(kingColor);
			if (!kingPosition.HasValue)
				throw new InvalidOperationException($"No {kingColor} king found on the board");

			var opponentColor = kingColor.Opposite();
			return state.IsSquareAttackedBy(kingPosition.Value, opponentColor);
		}

		public bool IsStalemate()
		{
			var (hasLegalMoves, isKingInCheck) = GetGameStatus();
			return !hasLegalMoves && !isKingInCheck;
		}

		/// <summary>
		///     Checks all game-ending conditions and sets the outcome if the game is over.
		/// </summary>
		public void CheckAndSetGameEndOutcome()
		{
			if (IsCheckmate())
				_gameManager.SetOutcome(
					_gameManager.CurrentState.ActiveColor == PieceColor.White
						? GameOutcome.BlackWin
						: GameOutcome.WhiteWin);
			else if (IsStalemate())
				_gameManager.SetOutcome(GameOutcome.DrawStalemate);
			else if (IsDrawByThreefoldRepetition())
				_gameManager.SetOutcome(GameOutcome.DrawThreefold);
			else if (IsDrawByInsufficientMaterial())
				_gameManager.SetOutcome(GameOutcome.DrawInsufficientMaterial);
			else if (IsDrawByFiftyMoveRule())
				_gameManager.SetOutcome(GameOutcome.DrawFiftyMoves);
		}

		public void InvalidateCache() => _gameStatusCache = null;

		private (bool hasLegalMoves, bool isKingInCheck) GetGameStatus()
		{
			_gameStatusCache ??= (
				_gameManager.GetLegalMoves().Any(),
				IsKingInCheck(_gameManager.CurrentState, _gameManager.CurrentState.ActiveColor)
			);

			return _gameStatusCache.Value;
		}
	}
}
