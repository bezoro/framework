using System.Collections.Generic;

namespace Bezoro.Chess.ChessLogic
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
						yield return new(from, to);
					}
					else if (pieceAtDestination.Color != gameState.ActiveColor)
					{
						yield return new(from, to, MoveType.Capture);
						break;
					}
					else
					{
						break;
					}

					newRow += dRow;
					newCol += dCol;
				}
			}
		}
	}
}
