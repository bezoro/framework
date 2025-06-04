using System;
using System.Collections.Generic;
using Bezoro.Core.Chess.Interfaces;
using Bezoro.Core.Chess.Utils;

namespace Bezoro.Core.Chess.Pieces
{
	/// <summary>
	///     Generates every pseudo-legal pawn move (white perspective; black is mirrored).
	/// </summary>
	/// <remarks>
	///     The pawn has six distinct *geometrical* move shapes, but at promotion rank
	///     three of them split into four different promotion pieces, yielding twelve
	///     concrete moves in total:
	///     ┌───────────────┬──────────────────────────────────────────────────────┐
	///     │ # │  Move                                     │ MoveKind │ Promotion │
	///     ├───┼───────────────────────────────────────────┼──────-───┼───────────┤
	///     │ 1 │ push forward                       (0,+1) │ Normal   │ Queen     │
	///     │ 2 │ push forward                       (0,+1) │ Normal   │ Rook      │
	///     │ 3 │ push forward                       (0,+1) │ Normal   │ Bishop    │
	///     │ 4 │ push forward                       (0,+1) │ Normal   │ Knight    │
	///     │ 5 │ capture left                      (-1,+1) │ Capture  │ Queen     │
	///     │ 6 │ capture left                      (-1,+1) │ Capture  │ Rook      │
	///     │ 7 │ capture left                      (-1,+1) │ Capture  │ Bishop    │
	///     │ 8 │ capture left                      (-1,+1) │ Capture  │ Knight    │
	///     │ 9 │ capture right                     (+1,+1) │ Capture  │ Queen     │
	///     │10 │ capture right                     (+1,+1) │ Capture  │ Rook      │
	///     │11 │ capture right                     (+1,+1) │ Capture  │ Bishop    │
	///     │12 │ capture right                     (+1,+1) │ Capture  │ Knight    │
	///     └───┴───────────────────────────────────────────┴───-──────┴───────────┘
	///     The remaining three geometrical moves
	///     • double push  (0,+2)               – first move only, never promotes
	///     • en-passant L (-1,+1)              – capture, never promotes
	///     • en-passant R (+1,+1)              – capture, never promotes
	///     are included in the template table below but do not increase the count
	///     of *promotion* moves, hence they are not part of the twelve listed above.
	/// </remarks>
	public sealed class PawnPseudoValidMovesGenerator : IPseudoMoveGenerator
	{
		// Offsets are expressed for a white pawn moving “up” the board (dy = +1).
		// For black pawns the dy component is multiplied by −1.
		private static readonly MoveTemplate[] _templates =
		{
			new(0, +1, MoveKind.Normal),       // single push
			new(0, +2, MoveKind.Normal, true), // double push
			new(-1, +1, MoveKind.Capture),     // capture left
			new(+1, +1, MoveKind.Capture),     // capture right
			new(-1, +1, MoveKind.EnPassant),   // e.p. left
			new(+1, +1, MoveKind.EnPassant)    // e.p. right
		};

	#region Interface Implementations

		public IEnumerable<Move> Generate(IChessBoardModel board, PieceModel piece)
		{
			if (board is null) throw new ArgumentNullException(nameof(board));
			if (piece is not PawnModel pawn)
				throw new ArgumentException("Piece supplied to PawnPseudoMoveGenerator is not a pawn.", nameof(piece));

			var pos = board.GetPosition(pawn);
			if (pos is null)
				return Array.Empty<Move>();

			var file  = pos.Value.Column;
			var rank  = pos.Value.Row;
			var dir   = pawn.Direction; // +1 (white) / −1 (black)
			var moves = new List<Move>(12);

			foreach (var t in _templates)
			{
				if (t.FirstMoveOnly && pawn.HasMoved) continue;

				var toFile = file + t.Dx;
				var toRank = rank + t.Dy * dir; // flip for black

				if (!board.IsInside(toFile, toRank)) continue;

				var from = pos.Value;
				var to   = new BoardPosition(toFile, toRank);

				// Promotion check
				if (IsPromotionRank(pawn.Color, toRank, board))
				{
					foreach (PromotionPieceType promo in Enum.GetValues(typeof(PromotionPieceType)))
					{
						moves.Add(Move.Promotion(from, to, promo));
					}
				}
				else
				{
					moves.Add(new(from, to, t.Kind));
				}
			}

			return moves;
		}

	#endregion

		/*──────────────────────────────────────────────────────────*/

		private static bool IsPromotionRank(PlayerColor color, int rank, IChessBoardModel board) =>
			color == PlayerColor.White ? rank == board.Height - 1 : rank == 0;
		/*──────────────────────────────────────────────────────────*/
		/*  A “matrix” of pawn moves                                */
		/*                                                          */
		/*             (-1,+1)  (0,+1)  (+1,+1)                     */
		/*             capture   push   capture                     */
		/*                                                          */
		/*             (0,+2)   – double push (first move only)     */
		/*                                                          */
		/*  En-passant targets share the same diagonal offsets.     */
		/*──────────────────────────────────────────────────────────*/

		private readonly struct MoveTemplate
		{
			public MoveTemplate(int dx, int dy, MoveKind kind, bool firstMoveOnly = false)
			{
				Dx            = dx;
				Dy            = dy;
				Kind          = kind;
				FirstMoveOnly = firstMoveOnly;
			}

			public readonly bool     FirstMoveOnly;
			public readonly int      Dx;
			public readonly int      Dy;
			public readonly MoveKind Kind;
		}
	}
}
