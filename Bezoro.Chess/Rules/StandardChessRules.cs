using System;
using System.Collections.Generic;
using System.Linq;
using Bezoro.Chess.Abstractions.Interfaces;
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
					if (CheckIfMoveExposesKing())
					{
						return Move.Create(
							move.From,
							move.To,
							move.MovingSide,
							move.PieceType,
							move.Kind,
							move.PromoteTo,
							move.CastleSide,
							true);
					}

					return move;
				});
		}

	#endregion

		private bool CheckIfMoveExposesKing() =>
			throw new NotImplementedException();
	}
}
