using System.Collections.Generic;
using Bezoro.Chess.Domain.Board;

namespace Bezoro.Chess.Domain.Moves.Generation
{
	internal static class SlidingPieceMoveGenerator
	{
		public static IEnumerable<Move> GenerateMoves(
			Position from, GameState gameState, (int dRow, int dCol)[] directions)
		{
			foreach (var (dRow, dCol) in directions)
			{
				var newRow = from.Row + dRow;
				var newCol = from.Col + dCol;

				while (BoardHelper.IsInsideBoard(new(newRow, newCol)))
				{
					var to                 = new Position(newRow, newCol);
					var pieceAtDestination = gameState.PiecePositions[to.Row, to.Col];

					if (pieceAtDestination.Type == PieceType.None)
					{
						yield return new(from, to, gameState.ActiveColor);
					}
					else if (pieceAtDestination.Color != gameState.ActiveColor)
					{
						yield return new(
							from, to, gameState.ActiveColor, MoveType.Capture, pieceAtDestination.Type,
							PromotionType.None);

						break;
					}
					else
					{
						// Blocked by our own piece.
						break;
					}

					newRow += dRow;
					newCol += dCol;
				}
			}
		}
	}
}
