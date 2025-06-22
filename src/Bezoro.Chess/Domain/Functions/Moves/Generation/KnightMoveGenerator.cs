using System.Collections.Generic;
using Bezoro.Chess.Domain.Helpers;
using Bezoro.Chess.Domain.Shared.Enums;
using Bezoro.Chess.Domain.Types.Records;
using Bezoro.Chess.Domain.Types.Structs;

namespace Bezoro.Chess.Domain.Functions.Moves.Generation
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

			Piece movingPiece = gameState.Board.GetPiece(from);

			foreach ((int dRow, int dCol) in moves)
			{
				var toPosition = new Position(from.Row + dRow, from.Col + dCol);

				if (!BoardHelper.IsInsideBoard(toPosition))
				{
					continue;
				}

				Piece pieceAtDestination = gameState.Board.GetPiece(toPosition);

				if (pieceAtDestination.Type == PieceType.None)
				{
					yield return Move.Normal(from, toPosition, movingPiece);
				}
				else if (pieceAtDestination.Color != gameState.ActiveColor)
				{
					yield return Move.Capture(from, toPosition, movingPiece, pieceAtDestination);
				}
			}
		}
	}
}
