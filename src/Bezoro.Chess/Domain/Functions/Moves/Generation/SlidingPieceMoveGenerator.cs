using System.Collections.Generic;
using Bezoro.Chess.Domain.Helpers;
using Bezoro.Chess.Domain.Shared.Enums;
using Bezoro.Chess.Domain.Types.Records;
using Bezoro.Chess.Domain.Types.Structs;

namespace Bezoro.Chess.Domain.Functions.Moves.Generation
{
	internal static class SlidingPieceMoveGenerator
	{
		public static IEnumerable<Move> GenerateMoves(
			Position from, GameState gameState, (int dRow, int dCol)[] directions)
		{
			Piece movingPiece = gameState.PiecePositions[from.Row, from.Col];

			foreach ((int dRow, int dCol) in directions)
			{
				int newRow = from.Row + dRow;
				int newCol = from.Col + dCol;

				while (BoardHelper.IsInsideBoard(new Position(newRow, newCol)))
				{
					var   to                 = new Position(newRow, newCol);
					Piece pieceAtDestination = gameState.PiecePositions[to.Row, to.Col];

					if (pieceAtDestination.Type == PieceType.None)
					{
						yield return Move.CreateNormal(from, to, movingPiece);
					}
					else if (pieceAtDestination.Color != gameState.ActiveColor)
					{
						yield return Move.CreateCapture(from, to, movingPiece, pieceAtDestination);

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
