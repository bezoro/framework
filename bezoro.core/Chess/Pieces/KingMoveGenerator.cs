using System;
using System.Collections.Generic;

namespace Bezoro.Core.Chess
{
	/// <summary>
	///     Generates every pseudo-legal move for a single king, including:
	///     • all 8 adjacent squares (captures + quiet moves)
	///     • castling (king/queen-side) when the corresponding rights exist
	/// </summary>
	public sealed class KingMoveGenerator : IMoveGenerator
	{
		private static readonly (int df, int dr)[] _KING_STEPS =
		{
			(-1, -1), (-1, 0), (-1, 1),
			(0, -1), (0, 1),
			(1, -1), (1, 0), (1, 1)
		};

	#region Interface Implementations

		public IEnumerable<Move> Generate(IChessBoardModel board, PieceModel piece)
		{
			if (board is null) throw new ArgumentNullException(nameof(board));
			if (piece is not KingModel king)
				throw new ArgumentException("Piece supplied to KingMoveGenerator is not a king.", nameof(piece));

			var moves = new List<Move>();

			//------------------------------------------------------------------
			// 1. Locate the king on the board
			//------------------------------------------------------------------
			var currentPos = board.GetPosition(king);
			if (currentPos is null)
				return moves;

			int file = currentPos.Value.File;
			var rank = currentPos.Value.Rank;

			//------------------------------------------------------------------
			// 2. Normal king moves (8 surrounding squares)
			//------------------------------------------------------------------
			foreach (var (df, dr) in _KING_STEPS)
			{
				var tf = file + df;
				var tr = rank + dr;

				if (!IsInside(board, tf, tr)) continue;

				var targetPiece = board.Squares[tf, tr].GetPiece();
				if (targetPiece is null)
				{
					moves.Add(new(currentPos.Value, new(tf, tr)));
				}
				else if (targetPiece.Color != king.Color)
				{
					moves.Add(new(currentPos.Value, new(tf, tr), MoveKind.Capture));
				}
			}

			//------------------------------------------------------------------
			// 3. Castling (rights, empty squares, king & rook unmoved)
			//
			// NOTE: Attack checks are intentionally left out here because the
			// engine does not expose such functionality yet. They can be added
			// later by filtering the move list at a higher level.
			//------------------------------------------------------------------
			if (king.HasMoved)
				return moves;

			if (king.Color == PlayerColor.White)
			{
				AddCastling(
					board, moves,
					CastlingRights.WhiteKingSide,
					new(4, 0),                // king start
					new[] { (5, 0), (6, 0) }, // king path squares that must be empty
					new(6, 0));               // king destination

				AddCastling(
					board, moves,
					CastlingRights.WhiteQueenSide,
					new(4, 0),
					new[] { (3, 0), (2, 0), (1, 0) },
					new(2, 0));
			}
			else // black
			{
				var backRank = board.Height - 1;

				AddCastling(
					board, moves,
					CastlingRights.BlackKingSide,
					new(4, backRank),
					new[] { (5, backRank), (6, backRank) },
					new(6, backRank));

				AddCastling(
					board, moves,
					CastlingRights.BlackQueenSide,
					new(4, backRank),
					new[] { (3, backRank), (2, backRank), (1, backRank) },
					new(2, backRank));
			}

			return moves;
		}

	#endregion

	#region Helper Methods

		private static bool IsInside(IChessBoardModel board, int file, int rank) =>
			file >= 0 && file < board.Width && rank >= 0 && rank < board.Height;

		private static void AddCastling(
			IChessBoardModel board,
			ICollection<Move> moves,
			CastlingRights requiredRight,
			BoardPosition kingStart,
			IEnumerable<(int f, int r)> emptySquares,
			BoardPosition kingDestination)
		{
			if (!board.CastlingRights.HasFlag(requiredRight)) return;

			foreach (var (f, r) in emptySquares)
			{
				if (!IsInside(board, f, r) || board.Squares[f, r].GetPiece() is not null)
					return;
			}

			moves.Add(new(kingStart, kingDestination, MoveKind.Castle));
		}

	#endregion
	}
}
