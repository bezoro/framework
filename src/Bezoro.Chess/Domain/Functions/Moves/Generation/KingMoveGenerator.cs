using System.Collections.Generic;
using Bezoro.Chess.Domain.Extensions;
using Bezoro.Chess.Domain.Helpers;
using Bezoro.Chess.Domain.Shared.Enums;
using Bezoro.Chess.Domain.Types.Records;
using Bezoro.Chess.Domain.Types.Structs;

namespace Bezoro.Chess.Domain.Functions.Moves.Generation
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

			Piece movingPiece = gameState.Board.GetPiece(from);

			foreach ((int dRow, int dCol) in directions)
			{
				var to = new Position(from.Row + dRow, from.Col + dCol);

				if (!BoardHelper.IsInsideBoard(to))
				{
					continue;
				}

				Piece pieceAtDestination = gameState.Board.GetPiece(to);

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
			foreach (Move move in GenerateCastlingMoves(from, gameState))
			{
				yield return move;
			}
		}

		private static bool CanCastle(GameState gameState, CastlingSide side)
		{
			PieceColor color         = gameState.ActiveColor;
			PieceColor opponentColor = color.Opposite();

			// The king must be on its home rank to castle.
			int       homeRow = color == PieceColor.White ? 7 : 0;
			Position? kingPos = gameState.FindKingPosition(color);

			// Rule: King must be on its starting square (e1/e8).
			if (kingPos == null || kingPos.Value.Row != homeRow || kingPos.Value.Col != 4)
			{
				return false;
			}

			// Rule: Check if castling rights are present.
			bool hasRights =
				(gameState.Castling &
				 (side == CastlingSide.Kingside
					 ? color == PieceColor.White ? CastlingRights.WhiteKingside : CastlingRights.BlackKingside
					 : color == PieceColor.White ? CastlingRights.WhiteQueenside : CastlingRights.BlackQueenside)) !=
				0;

			if (!hasRights)
			{
				return false;
			}

			var      pathSquares  = new List<Position>();
			var      emptySquares = new List<Position>();
			Position rookPos;

			if (side == CastlingSide.Kingside)
			{
				rookPos = new Position(homeRow, 7); // h-file
				// Path king travels (f and g files)
				pathSquares.Add(new Position(homeRow, 5));
				pathSquares.Add(new Position(homeRow, 6));
				// Squares that must be empty
				emptySquares.Add(new Position(homeRow, 5));
				emptySquares.Add(new Position(homeRow, 6));
			}
			else // Queenside
			{
				rookPos = new Position(homeRow, 0); // a-file
				// Path king travels (d and c files)
				pathSquares.Add(new Position(homeRow, 3));
				pathSquares.Add(new Position(homeRow, 2));
				// Squares that must be empty
				emptySquares.Add(new Position(homeRow, 3));
				emptySquares.Add(new Position(homeRow, 2));
				emptySquares.Add(new Position(homeRow, 1));
			}

			// Rule: The rook must be on its starting square.
			Piece rook = gameState.GetPieceAt(rookPos);
			if (rook.Type != PieceType.Rook || rook.Color != color)
			{
				return false;
			}

			// Rule: All squares between King and Rook must be empty
			if (emptySquares.Exists(pos => gameState.GetPieceAt(pos).Type != PieceType.None))
			{
				return false;
			}

			// Rule: King cannot pass through or land on an attacked square.
			return pathSquares.TrueForAll(pos => !gameState.IsSquareAttackedBy(pos, opponentColor));
		}

		private static IEnumerable<Move> GenerateCastlingMoves(Position from, GameState gameState)
		{
			Piece      movingPiece   = gameState.Board.GetPiece(from);
			PieceColor opponentColor = gameState.ActiveColor.Opposite();

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
