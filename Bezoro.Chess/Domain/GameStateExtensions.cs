using System.Linq;
using Bezoro.Chess.Domain.Board;
using Bezoro.Chess.Domain.Moves;

namespace Bezoro.Chess.Domain
{
	public static class GameStateExtensions
	{
		/// <summary>
		///     Determines if a specific square on the board is under attack by the given color.
		/// </summary>
		public static bool IsSquareAttackedBy(this GameState state, Position square, PieceColor attackerColor)
		{
			// Temporarily set the active color to the attacker's color to generate their moves
			var tempState = state with { ActiveColor = attackerColor };

			// Generate all possible moves for the attacker
			var attackerMoves = MoveGenerator.GenerateMoves(tempState);

			// If any of the generated moves target the square, it's under attack
			return attackerMoves.Any(move => move.To.Equals(square));
		}

		/// <summary>
		///     Finds the position of the king for a given color.
		/// </summary>
		/// <returns>The king's position, or null if not found.</returns>
		public static Position? FindKingPosition(this GameState state, PieceColor kingColor)
		{
			for (var r = 0 ; r < 8 ; r++)
			{
				for (var c = 0 ; c < 8 ; c++)
				{
					var piece = state.PiecePositions[r, c];
					if (piece.Type == PieceType.King && piece.Color == kingColor)
					{
						return new Position(r, c);
					}
				}
			}

			return null;
		}
	}
}
