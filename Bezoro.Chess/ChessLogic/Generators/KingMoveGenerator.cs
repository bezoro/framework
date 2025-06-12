using System.Collections.Generic;

namespace Bezoro.Chess.ChessLogic
{
	internal static class KingMoveGenerator
	{
		public static IEnumerable<Move> GenerateMoves(Position from, GameState gameState)
		{
			(int dRow, int dCol)[] directions =
			{
				(-1, -1), (-1, 0), (-1, 1), (0, -1), (0, 1), (1, -1), (1, 0), (1, 1)
			};

			foreach (var (dRow, dCol) in directions)
			{
				var to = new Position(from.Row + dRow, from.Col + dCol);

				if (!BoardHelper.IsInsideBoard(to))
				{
					continue;
				}

				var pieceAtDestination = gameState.PiecePositions[to.Row, to.Col];

				if (pieceAtDestination.Type == PieceType.None)
				{
					yield return new(from, to);
				}
				else if (pieceAtDestination.Color != gameState.ActiveColor)
				{
					yield return new(from, to, MoveType.Capture);
				}
			}

			foreach (var move in GenerateCastlingMoves(from, gameState))
			{
				yield return move;
			}
		}

		private static IEnumerable<Move> GenerateCastlingMoves(Position from, GameState gameState)
		{
			if (gameState.ActiveColor == PieceColor.White)
			{
				if (from.Row != 7 || from.Col != 4) yield break;

				if ((gameState.Castling & CastlingRights.WhiteKingside) != 0              &&
					gameState.PiecePositions[7, 5].Type                 == PieceType.None &&
					gameState.PiecePositions[7, 6].Type                 == PieceType.None)
				{
					yield return new(from, new(7, 6), MoveType.CastleKingside);
				}

				if ((gameState.Castling & CastlingRights.WhiteQueenside) != 0              &&
					gameState.PiecePositions[7, 3].Type                  == PieceType.None &&
					gameState.PiecePositions[7, 2].Type                  == PieceType.None &&
					gameState.PiecePositions[7, 1].Type                  == PieceType.None)
				{
					yield return new(from, new(7, 2), MoveType.CastleQueenside);
				}
			}
			else
			{
				if (from.Row != 0 || from.Col != 4) yield break;

				if ((gameState.Castling & CastlingRights.BlackKingside) != 0              &&
					gameState.PiecePositions[0, 5].Type                 == PieceType.None &&
					gameState.PiecePositions[0, 6].Type                 == PieceType.None)
				{
					yield return new(from, new(0, 6), MoveType.CastleKingside);
				}

				if ((gameState.Castling & CastlingRights.BlackQueenside) != 0              &&
					gameState.PiecePositions[0, 3].Type                  == PieceType.None &&
					gameState.PiecePositions[0, 2].Type                  == PieceType.None &&
					gameState.PiecePositions[0, 1].Type                  == PieceType.None)
				{
					yield return new(from, new(0, 2), MoveType.CastleQueenside);
				}
			}
		}
	}
}
