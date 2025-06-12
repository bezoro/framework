using System.Collections.Generic;

namespace Bezoro.Chess.ChessLogic.Generators
{
	internal static class KnightMoveGenerator
	{
		public static IEnumerable<Move> GenerateMoves(Position from, GameState gameState)
		{
			(int dRow, int dCol)[] moves =
			{
				(2, 1), (2, -1), (-2, 1), (-2, -1),
				(1, 2), (1, -2), (-1, 2), (-1, -2)
			};

			foreach (var (dRow, dCol) in moves)
			{
				var toPosition = new Position(from.Row + dRow, from.Col + dCol);

				if (!BoardHelper.IsInsideBoard(toPosition))
				{
					continue;
				}

				var pieceAtDestination = gameState.PiecePositions[toPosition.Row, toPosition.Col];

				if (pieceAtDestination.Type == PieceType.None)
				{
					yield return new(from, toPosition, gameState.ActiveColor);
				}
				else if (pieceAtDestination.Color != gameState.ActiveColor)
				{
					yield return new(from, toPosition, gameState.ActiveColor, MoveType.Capture);
				}
			}
		}
	}
}
