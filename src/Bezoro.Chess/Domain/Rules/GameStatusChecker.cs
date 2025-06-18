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
		/// <summary>
		///     Caches the result of expensive end-of-game calculations.
		///     Invalidated whenever the game state changes.
		/// </summary>
		private (bool hasLegalMoves, bool isKingInCheck)? _gameStatusCache;
		private readonly GameManager _gameManager;

		public GameStatusChecker(GameManager gameManager)
		{
			_gameManager = gameManager;
		}

		public bool IsCheckmate()
		{
			(bool hasLegalMoves, bool isKingInCheck) = GetGameStatus();
			return !hasLegalMoves && isKingInCheck;
		}

		public bool IsDrawByFiftyMoveRule() =>
			_gameManager.CurrentState.HalfMoveClock >= 100;

		public bool IsDrawByInsufficientMaterial()
		{
			// Get all non-king pieces on the board
			List<Piece> pieces = _gameManager.CurrentState.PiecePositions.Cast<Piece>()
											 .Where(p => p.Type is not PieceType.None and not PieceType.King)
											 .ToList();

			// If there are any pawns, rooks, or queens, a checkmate is possible
			if (pieces.Any(p => p.Type is PieceType.Pawn or PieceType.Rook or PieceType.Queen))
			{
				return false;
			}

			// No pieces other than kings means draw
			if (pieces.Count == 0)
			{
				return true;
			}

			// Group pieces by player color
			List<Piece> whitePieces = pieces.Where(p => p.Color == PieceColor.White).ToList();
			List<Piece> blackPieces = pieces.Where(p => p.Color == PieceColor.Black).ToList();

			// One side has no pieces other than king
			if (whitePieces.Count == 0 || blackPieces.Count == 0)
			{
				List<Piece> remainingPieces = whitePieces.Count > 0 ? whitePieces : blackPieces;

				// King vs King + single knight or bishop is a draw
				if (remainingPieces.Count == 1 &&
					(remainingPieces[0].Type == PieceType.Knight || remainingPieces[0].Type == PieceType.Bishop))
				{
					return true;
				}

				// King vs King + 2 Knights can't force checkmate (according to FIDE rules)
				if (remainingPieces.Count == 2 && remainingPieces.All(p => p.Type == PieceType.Knight))
				{
					return true;
				}
			}

			// Check for bishops of the same color scenario
			var bishops = new List<(Position Position, PieceColor PlayerColor)>();
			for (var r = 0 ; r < 8 ; r++)
			{
				for (var c = 0 ; c < 8 ; c++)
				{
					Piece piece = _gameManager.CurrentState.PiecePositions[r, c];
					if (piece.Type == PieceType.Bishop)
					{
						bishops.Add((new(r, c), piece.Color));
					}
				}
			}

			// Special case: if all bishops are on same-colored squares
			if (bishops.Count > 0 && pieces.All(p => p.Type == PieceType.Bishop || p.Type == PieceType.King))
			{
				// Check if all bishops move on same-colored squares
				var allOnSameColoredSquares = true;
				int squareColor             = (bishops[0].Position.Row + bishops[0].Position.Col) % 2;

				for (var i = 1 ; i < bishops.Count ; i++)
				{
					if ((bishops[i].Position.Row + bishops[i].Position.Col) % 2 != squareColor)
					{
						allOnSameColoredSquares = false;
						break;
					}
				}

				if (allOnSameColoredSquares)
				{
					return true;
				}
			}

			// All other cases are not draws by insufficient material
			return false;
		}

		public bool IsDrawByThreefoldRepetition() =>
			_gameManager.GameStateHistory.Count(state => state.Equals(_gameManager.CurrentState)) >= 3;

		public bool IsKingInCheck(GameState state, PieceColor kingColor)
		{
			Position? kingPosition = state.FindKingPosition(kingColor);
			if (!kingPosition.HasValue)
			{
				throw new InvalidOperationException($"No {kingColor} king found on the board");
			}

			PieceColor opponentColor = kingColor.Opposite();
			return state.IsSquareAttackedBy(kingPosition.Value, opponentColor);
		}

		public bool IsStalemate()
		{
			(bool hasLegalMoves, bool isKingInCheck) = GetGameStatus();
			return !hasLegalMoves && !isKingInCheck;
		}

		/// <summary>
		///     Checks all game-ending conditions and sets the outcome if the game is over.
		/// </summary>
		public void CheckAndSetGameEndOutcome()
		{
			if (IsCheckmate())
			{
				_gameManager.SetOutcome(
					_gameManager.CurrentState.ActiveColor == PieceColor.White
						? GameOutcome.BlackWin
						: GameOutcome.WhiteWin);
			}
			else if (IsStalemate())
			{
				_gameManager.SetOutcome(GameOutcome.DrawStalemate);
			}
			else if (IsDrawByThreefoldRepetition())
			{
				_gameManager.SetOutcome(GameOutcome.DrawThreefold);
			}
			else if (IsDrawByInsufficientMaterial())
			{
				_gameManager.SetOutcome(GameOutcome.DrawInsufficientMaterial);
			}
			else if (IsDrawByFiftyMoveRule())
			{
				_gameManager.SetOutcome(GameOutcome.DrawFiftyMoves);
			}
		}

		public void InvalidateCache() => _gameStatusCache = null;

		private (bool hasLegalMoves, bool isKingInCheck) GetGameStatus()
		{
			// If cache is null or invalidated, compute and store the game status
			if (!_gameStatusCache.HasValue)
			{
				_gameStatusCache = (
					_gameManager.GetLegalMoves().Any(),
					IsKingInCheck(_gameManager.CurrentState, _gameManager.CurrentState.ActiveColor)
				);
			}

			return _gameStatusCache.Value;
		}
	}
}
