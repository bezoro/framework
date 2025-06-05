using System;
using System.Collections.Generic;
using Bezoro.Core.Chess.Board.Validation;
using Bezoro.Core.Chess.Interfaces;

namespace Bezoro.Core.Chess.Pieces
{
	/// <summary>
	///     Generates every pseudo-legal move for a single king:
	///     • 8 adjacent squares (captures plus quiet moves)
	///     • castling (k/q-side) – the right is now delegated to
	///     <see cref="KingModel.CanCastle" />.
	/// </summary>
	public sealed class KingPseudoMoveGenerator : IPseudoMoveGenerator
	{
		private static readonly (int df, int dr)[] _KING_STEPS =
		{
			(-1, -1), (-1, 0), (-1, 1),
			(0, -1), (0, 1),
			(1, -1), (1, 0), (1, 1)
		};

	#region Interface Implementations

		public IEnumerable<Move> Generate(GameModel game, IChessPieceModel piece)
		{
			if (game is null) throw new ArgumentNullException(nameof(game));
			if (piece is not KingModel king)
				throw new ArgumentException("Piece supplied to KingPseudoMoveGenerator is not a king.", nameof(piece));

			var board = game.Board;
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

				if (!board.IsInside(tf, tr))
					continue;

				var target = board.Squares[tf, tr].GetPiece();
				if (target is null)
				{
					moves.Add(new(currentPos.Value, new(tf, tr), king.Color, king.GetPieceType()));
				}
				else if (target.Color != king.Color)
				{
					moves.Add(new(currentPos.Value, new(tf, tr), king.Color, king.GetPieceType(), MoveKind.Capture));
				}
			}

			//------------------------------------------------------------------
			// 3. Castling – delegate the rights check to KingModel
			//------------------------------------------------------------------
			if (king.HasMoved)
				return moves; // a moved king may no longer castle

			var homeRank = king.Color == PlayerColor.White ? 0 : board.Height - 1;

			// Describe both castle options once and loop over them
			var castleOptions = new[]
			{
				new
				{
					Side       = CastleSide.King,
					RookFile   = 7, // h-file
					KingTarget = 6  // g-file
				},
				new
				{
					Side       = CastleSide.Queen,
					RookFile   = 0, // a-file
					KingTarget = 2  // c-file
				}
			};

			foreach (var opt in castleOptions)
			{
				var kingStart = new BoardPosition(4,              homeRank); // e-file
				var rookStart = new BoardPosition(opt.RookFile,   homeRank);
				var kingDest  = new BoardPosition(opt.KingTarget, homeRank);

				TryAddCastle(game, king, moves, opt.Side, kingStart, rookStart, kingDest);
			}

			return moves;
		}

	#endregion

		private static void TryAddCastle(
			GameModel game,
			KingModel king,
			ICollection<Move> moves,
			CastleSide side,
			BoardPosition kingStart,
			BoardPosition rookStart,
			BoardPosition kingDestination)
		{
			var board = game.Board;
			// 1. Global rights + king / rook unmoved
			if (!king.CanCastle(game.CastlingRights, side))
				return;

			// 2. Every square between the king and rook must be empty.
			//    We can ask the board itself for that straight path.
			if (board is BoardModel concreteBoard)
			{
				foreach (var sq in concreteBoard.GetStraightPath(kingStart, rookStart))
				{
					if (sq.GetPiece() is not null)
						return;
				}
			}
			else
			{
				// Fallback: manual scan if we are given a different board implementation.
				var step = Math.Sign(rookStart.Column - kingStart.Column);
				for (var f = kingStart.Column + step ; f != rookStart.Column ; f += step)
				{
					if (board.Squares[f, kingStart.Rank].GetPiece() is not null)
						return;
				}
			}

			// 3. (Optional) check for check along the path – still deferred.

			moves.Add(new(kingStart, kingDestination, king.Color, king.GetPieceType(), MoveKind.Castle));
		}
	}
}
