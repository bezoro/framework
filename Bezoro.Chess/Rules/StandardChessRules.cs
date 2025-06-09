using System;
using System.Collections.Generic;
using System.Linq;
using Bezoro.Chess.Abstractions.Interfaces;
using Bezoro.Chess.Board;
using Bezoro.Chess.Common.Enums;
using Bezoro.Chess.Game.Models;
using Bezoro.Chess.Moves.Models;

// Required for GameModel

// Assuming BoardPosition is here
// Using Bezoro.Chess.Board.Models; is not strictly needed here if GameModel.Board provides IChessBoardModel

namespace Bezoro.Chess.Rules
{
	public sealed class StandardChessRules : IGameRules
	{
	#region Interface Implementations

		public IEnumerable<Move> FilterLegalMoves(GameModel game, IEnumerable<Move> pseudoMoves)
		{
			// TODO: Revisit after all movement logic is done
			return pseudoMoves;
			if (game is null)
				throw new ArgumentNullException(nameof(game));

			if (pseudoMoves is null)
				throw new ArgumentNullException(nameof(pseudoMoves));

			return pseudoMoves.Select(
				move =>
				{
					// Ensure the move being checked is for the currently active player in the original game context
					if (CheckIfMoveExposesKing(game, move, game.ActiveColor))
					{
						return new(
							move.From,
							move.To,
							move.MovingSide, // Should align with game.ActiveColor
							move.PieceType,
							move.Kind,
							move.CastleSide,
							move.PromoteTo, true);
					}

					return move;
				});
		}

	#endregion

		private bool CheckIfMoveExposesKing(GameModel originalGame, Move moveToValidate, PlayerColor movingPlayerColor)
		{
			// 1. Create a hypothetical game state by applying 'moveToValidate'.
			var gameAfterMove = SimulateMove(originalGame, moveToValidate);

			var hypotheticalBoard = gameAfterMove.Board;
			if (hypotheticalBoard == null)
			{
				Console.WriteLine("Error: Hypothetical board is null after simulating move.");
				throw new InvalidOperationException(
					"Hypothetical board could not be obtained after simulating a move.");
			}

			// 2. Find the position of the moving player's King in this new hypothetical state.
			//    The 'movingPlayerColor' is the color of the player whose move we are validating.
			BoardPosition?    kingPosition = null;
			IChessPieceModel? kingPiece    = null;

			foreach (var pieceOnBoard in hypotheticalBoard.BoardPieces)
			{
				if (pieceOnBoard.Color == movingPlayerColor && pieceOnBoard.GetPieceType() == ChessPieceType.King)
				{
					kingPiece    = pieceOnBoard;
					kingPosition = hypotheticalBoard.GetPosition(kingPiece);
					break;
				}
			}

			if (kingPiece == null || kingPosition == null)
			{
				Console.WriteLine(
					$"Error: King of color {movingPlayerColor} not found on the hypothetical board after move {moveToValidate}. This indicates a problem with game state or simulation.");

				return true; // Cautious: if king is missing, assume it's an invalid state.
			}

			// 3. Determine the opponent's color.
			//    This is the color of pieces that could be attacking the king.
			//    After 'gameAfterMove.DoMove(moveToValidate)', 'gameAfterMove.ActiveColor' will be the opponent's color.
			//    So, we can use that directly.
			var opponentColor = gameAfterMove.ActiveColor;
			// Or, more explicitly based on the original moving player:
			// PlayerColor opponentColor = (movingPlayerColor == PlayerColor.White) ? PlayerColor.Black : PlayerColor.White;

			// 4. Check if any opponent piece can attack the King's square in the hypothetical state.
			foreach (var opponentPiece in hypotheticalBoard.BoardPieces.Where(p => p.Color == opponentColor))
			{
				// GetPseudoLegalMoves requires a GameModel context. We provide gameAfterMove
				// because that contains the board state on which these pseudo-legal moves should be generated.
				var opponentPseudoMoves = opponentPiece.GetPseudoLegalMoves(gameAfterMove);

				foreach (var opponentAttackMove in opponentPseudoMoves)
				{
					if (opponentAttackMove.To.Equals(kingPosition.Value)) // kingPosition is BoardPosition?, use .Value
					{
						return true; // The King is attacked by this opponent piece.
					}
				}
			}

			return false; // The move is safe for the king.
		}

		/// <summary>
		///     Simulates a move on a copy of the game state.
		///     Creates a new GameModel instance representing the state after the move.
		/// </summary>
		private GameModel SimulateMove(GameModel currentGame, Move move)
		{
			// 1. Get the FEN string of the current game state.
			var currentFen = currentGame.ToFenString();

			// 2. Create a new GameModel instance from this FEN.
			//    This creates an independent copy of the game state.
			//    We need to pass along board dimensions and rules from the original game.
			var hypotheticalGame = new GameModel(
				currentFen,
				currentGame.Board.Width,  // Assuming IChessBoardModel has Width
				currentGame.Board.Height, // Assuming IChessBoardModel has Height
				currentGame.GameRules     // Pass the same ruleset
			);

			// 3. Call DoMove on this new hypotheticalGame instance.
			//    DoMove will update the board and other game state like ActiveColor,
			//    castling rights (if applicable to the move), etc., on the hypotheticalGame.
			//    It's important that 'move.MovingSide' is consistent with 'hypotheticalGame.ActiveColor'
			//    before this call. The FEN creation should ensure ActiveColor is correct.
			//    If move.MovingSide is different, it might indicate an issue or a different context is needed.
			//    For check detection, we're simulating a move for the *current* active player.
			if (hypotheticalGame.ActiveColor != move.MovingSide)
			{
				// This might happen if the 'move' object's MovingSide was set independently
				// and doesn't match the ActiveColor derived from the FEN of 'currentGame'.
				// For a valid simulation of 'currentGame' making 'move', they should align.
				// If they don't, we might need to adjust the hypotheticalGame's ActiveColor
				// before DoMove, or ensure consistency upstream.
				// However, GameModel's constructor from FEN already sets ActiveColor.
				// DoMove itself also flips ActiveColor *after* the move.
				Console.WriteLine(
					$"Warning: ActiveColor of hypothetical game ({hypotheticalGame.ActiveColor}) " +
					$"before DoMove does not match move.MovingSide ({move.MovingSide}). "          +
					"This could lead to issues if DoMove has pre-conditions on ActiveColor matching the piece being moved.");
				// Potentially, one might need to set hypotheticalGame.ActiveColor = move.MovingSide here
				// if DoMove strictly requires it, but typically DoMove assumes the piece being moved
				// belongs to the current ActiveColor.
			}

			hypotheticalGame.DoMove(move);

			// 4. Return the new GameModel instance which now reflects the state *after* the move.
			return hypotheticalGame;
		}
	}
}
