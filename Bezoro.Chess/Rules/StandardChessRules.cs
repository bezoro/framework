using System;
using System.Collections.Generic;
using System.Linq;
using Bezoro.Chess.Abstractions.Interfaces;
using Bezoro.Chess.Board;
using Bezoro.Chess.Common.Enums;
using Bezoro.Chess.Game.Models;
using Bezoro.Chess.Moves.Models;

namespace Bezoro.Chess.Rules
{
	public sealed class StandardChessRules : IGameRules
	{
	#region Interface Implementations

		public IEnumerable<Move> FilterLegalMoves(GameModel game, IChessPieceModel piece, IEnumerable<Move> pseudoMoves)
		{
			// If no moves provided, return empty collection
			if (pseudoMoves == null || !pseudoMoves.Any())
				return Enumerable.Empty<Move>();

			// Create a collection to store moves with check information
			var evaluatedMoves = new List<Move>();

			// Examine each move to determine if it would leave the king in check
			foreach (var move in pseudoMoves)
			{
				// For each pseudo-legal move, check if it would leave the king in check
				var leavesKingInCheck = CheckIfMoveExposesKing(game, move, piece.Color);

				// Create a new move object with the check information
				var evaluatedMove = new Move(
					move.From,
					move.To,
					move.MovingSide,
					move.PieceType,
					move.Kind,
					move.PromoteTo,
					leavesKingInCheck);

				evaluatedMoves.Add(evaluatedMove);
			}

			return evaluatedMoves;
		}

	#endregion

		private bool CheckIfMoveExposesKing(GameModel game, Move move, PlayerColor movingPlayerColor)
		{
			// Create a snapshot of the current board state
			var currentSnapshot = new BoardSnapshot(game.Board.Squares);

			// Apply the move to the snapshot to see resulting position
			BoardSnapshot nextBoardState;
			try
			{
				nextBoardState = currentSnapshot.ApplyMove(move);
			}
			catch (Exception)
			{
				// If move cannot be applied, conservatively assume it would leave king in check
				return true;
			}

			// Find the king's position after the move
			var kingPosition = nextBoardState.FindKing(movingPlayerColor);

			// If king is not found (shouldn't happen in normal chess), consider it in check
			if (kingPosition == null)
			{
				return true;
			}

			// Determine opponent's color
			var opponentColor = movingPlayerColor == PlayerColor.White ? PlayerColor.Black : PlayerColor.White;

			// Check if the king's position is under attack after the move
			return nextBoardState.IsSquareAttacked(kingPosition.Value, opponentColor);
		}
	}
}
