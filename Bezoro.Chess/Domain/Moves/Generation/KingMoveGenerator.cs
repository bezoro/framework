using System.Collections.Generic;
using Bezoro.Chess.Domain.Board;

namespace Bezoro.Chess.Domain.Moves.Generation
{
	internal static class KingMoveGenerator
	{
		public static IEnumerable<Move> GenerateMoves(Position from, GameState gameState)
		{
			// Standard King Moves (1 square in any direction)
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

			// Castling Moves
			foreach (var move in GenerateCastlingMoves(from, gameState))
			{
				yield return move;
			}
		}

		private static bool CanCastle(GameState gameState, CastlingSide side)
		{
			var color         = gameState.ActiveColor;
			var kingPos       = gameState.FindKingPosition(color);
			var opponentColor = color.Opposite();

			if (kingPos == null) return false;

			var row = kingPos.Value.Row;
			var hasRights =
				(gameState.Castling &
				 (side == CastlingSide.Kingside
					 ? color == PieceColor.White ? CastlingRights.WhiteKingside : CastlingRights.BlackKingside
					 : color == PieceColor.White ? CastlingRights.WhiteQueenside : CastlingRights.BlackQueenside)) !=
				0;

			if (!hasRights) return false;

			var pathSquares  = new List<Position>();
			var emptySquares = new List<Position>();

			if (side == CastlingSide.Kingside)
			{
				pathSquares.Add(new(row, 5)); // f1/f8
				emptySquares.Add(new(row, 5));
				emptySquares.Add(new(row, 6)); // g1/g8
			}
			else // Queenside
			{
				pathSquares.Add(new(row, 3)); // d1/d8
				pathSquares.Add(new(row, 2)); // c1/c8
				emptySquares.Add(new(row, 3));
				emptySquares.Add(new(row, 2));
				emptySquares.Add(new(row, 1)); // b1/b8
			}

			// Rule: All squares between King and Rook must be empty
			if (emptySquares.Exists(pos => gameState.PiecePositions[pos.Row, pos.Col].Type != PieceType.None))
			{
				return false;
			}

			// Rule: King cannot pass through an attacked square.
			return pathSquares.TrueForAll(pos => !gameState.IsSquareAttackedBy(pos, opponentColor));
		}

		private static IEnumerable<Move> GenerateCastlingMoves(Position from, GameState gameState)
		{
			var movingPiece   = gameState.PiecePositions[from.Row, from.Col];
			var opponentColor = gameState.ActiveColor.Opposite();

			// Rule: King cannot be in check to castle.
			if (gameState.IsSquareAttackedBy(from, opponentColor))
			{
				yield break;
			}

			// Check Kingside Castling
			if (CanCastle(gameState, CastlingSide.Kingside))
			{
				var to = new Position(from.Row, from.Col + 2);
				yield return Move.CreateCastleKingside(from, to, movingPiece);
			}

			// Check Queenside Castling
			if (CanCastle(gameState, CastlingSide.Queenside))
			{
				var to = new Position(from.Row, from.Col - 2);
				yield return Move.CreateCastleQueenside(from, to, movingPiece);
			}
		}
	}

	internal enum CastlingSide
	{
		Kingside,
		Queenside
	}
}
