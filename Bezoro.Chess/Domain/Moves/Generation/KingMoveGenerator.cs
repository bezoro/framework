using System.Collections.Generic;
using Bezoro.Chess.Domain.Board;

namespace Bezoro.Chess.Domain.Moves.Generation
{
	internal static class KingMoveGenerator
	{
		public static IEnumerable<Move> GenerateMoves(Position from, GameState gameState)
		{
			(int dRow, int dCol)[] directions =
			{
				(-1, -1), (-1, 0), (-1, 1), (0, -1), (0, 1), (1, -1), (1, 0), (1, 1)
			};

			var movingPiece = gameState.PiecePositions[from.Row, from.Col];

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
					yield return Move.CreateNormal(from, to, movingPiece);
				}
				else if (pieceAtDestination.Color != gameState.ActiveColor)
				{
					yield return Move.CreateCapture(from, to, movingPiece, pieceAtDestination);
				}
			}

			foreach (var move in GenerateCastlingMoves(from, gameState))
			{
				yield return move;
			}
		}

		private static IEnumerable<Move> GenerateCastlingMoves(Position from, GameState gameState)
		{
			var movingPiece = gameState.PiecePositions[from.Row, from.Col];

			if (gameState.ActiveColor == PieceColor.White)
			{
				if (from.Row != 7 || from.Col != 4) yield break;

				// Kingside Castling
				if ((gameState.Castling & CastlingRights.WhiteKingside) != 0              &&
					gameState.PiecePositions[7, 5].Type                 == PieceType.None &&
					gameState.PiecePositions[7, 6].Type                 == PieceType.None)
				{
					yield return Move.CreateCastleKingside(from, new(7, 6), movingPiece);
				}

				// Queenside Castling
				if ((gameState.Castling & CastlingRights.WhiteQueenside) != 0              &&
					gameState.PiecePositions[7, 3].Type                  == PieceType.None &&
					gameState.PiecePositions[7, 2].Type                  == PieceType.None &&
					gameState.PiecePositions[7, 1].Type                  == PieceType.None)
				{
					yield return Move.CreateCastleQueenside(from, new(7, 2), movingPiece);
				}
			}
			else // Black
			{
				if (from.Row != 0 || from.Col != 4) yield break;

				// Kingside Castling
				if ((gameState.Castling & CastlingRights.BlackKingside) != 0              &&
					gameState.PiecePositions[0, 5].Type                 == PieceType.None &&
					gameState.PiecePositions[0, 6].Type                 == PieceType.None)
				{
					yield return Move.CreateCastleKingside(from, new(0, 6), movingPiece);
				}

				// Queenside Castling
				if ((gameState.Castling & CastlingRights.BlackQueenside) != 0              &&
					gameState.PiecePositions[0, 3].Type                  == PieceType.None &&
					gameState.PiecePositions[0, 2].Type                  == PieceType.None &&
					gameState.PiecePositions[0, 1].Type                  == PieceType.None)
				{
					yield return Move.CreateCastleQueenside(from, new(0, 2), movingPiece);
				}
			}
		}
	}
}
