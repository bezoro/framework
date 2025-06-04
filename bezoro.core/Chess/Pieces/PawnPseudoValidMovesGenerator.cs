using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Bezoro.Core.Chess.Interfaces;
using Bezoro.Core.Chess.Utils;

namespace Bezoro.Core.Chess.Pieces
{
	/// <summary>
	///     Generates every pseudo-legal pawn move (white perspective; black is mirrored).
	///     See the big ASCII table below for the 12 promotion cases.
	/// </summary>
	public sealed class PawnPseudoValidMovesGenerator : IPseudoMoveGenerator
	{
		// `ReadOnlySpan` avoids allocating a managed array; the JIT embeds the
		// data directly in the readonly data section.
		private static ReadOnlySpan<MoveTemplate> Templates => new[]
		{
			new MoveTemplate(0,  +1, MoveKind.Normal),       // single push
			new MoveTemplate(0,  +2, MoveKind.Normal, true), // double push
			new MoveTemplate(-1, +1, MoveKind.Capture),      // capture left
			new MoveTemplate(+1, +1, MoveKind.Capture),      // capture right
			new MoveTemplate(-1, +1, MoveKind.EnPassant),    // e.p. left
			new MoveTemplate(+1, +1, MoveKind.EnPassant)     // e.p. right
		};

	#region Interface Implementations

		/*──────────────────────────────────────────────────────────*/
		/*  IPseudoMoveGenerator                                    */
		/*──────────────────────────────────────────────────────────*/

		public IEnumerable<Move> Generate(IChessBoardModel board, PieceModel piece)
		{
			if (board is null) throw new ArgumentNullException(nameof(board));
			if (piece is not PawnModel pawn)
				throw new ArgumentException("Generator was given a non-pawn piece.", nameof(piece));

			var pos = board.GetPosition(pawn);
			if (pos is null) yield break;

			var file = pos.Value.Column;
			var rank = pos.Value.Row;
			var dir  = pawn.Direction; // +1 (white) / –1 (black)

			for (var i = 0 ; i < Templates.Length ; i++)
			{
				var t = Templates[i];
				if (t.FirstMoveOnly && pawn.HasMoved) continue;

				var toFile = file + t.Dx;
				var toRank = rank + t.Dy * dir;

				if (!board.IsInside(toFile, toRank)) continue;

				var from = pos.Value;
				var to   = new BoardPosition(toFile, toRank);

				if (IsPromotionRank(pawn.Color, toRank, board))
				{
					foreach (PromotionPieceType promo in Enum.GetValues(typeof(PromotionPieceType)))
					{
						yield return Move.Promotion(from, to, pawn.Color, promo);
					}
				}
				else
				{
					yield return new(from, to, pawn.Color, t.Kind);
				}
			}
		}

	#endregion

		/*──────────────────────────────────────────────────────────*/

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static bool IsPromotionRank(PlayerColor color, int rank, IChessBoardModel board) =>
			color == PlayerColor.White ? rank == board.Height - 1 : rank == 0;
		/*──────────────────────────────────────────────────────────*/
		/*  “Matrix” of pawn moves (white); black flips dy with dir */
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

			public readonly bool FirstMoveOnly;

			public readonly int      Dx;
			public readonly int      Dy;
			public readonly MoveKind Kind;
		}

		/*──────────────────────────────────────────────────────────*/
		/*  The 12 promotion moves table (documentation only)       */
		/*──────────────────────────────────────────────────────────*/
		/// <remarks>
		/// The pawn has six geometrical shapes; at the promotion rank three of
		/// them fan out into four promotion pieces, giving 12 concrete moves:
		/// ┌─────────────┬───────────────────────┬──────────┬───────────┐
		/// │ # │ Geometry│ MoveKind             │ Promotion│
		/// ├───┼─────────┼───────────────────────┼──────────┤
		/// │ 1 │ (0,+1)  │ Normal               │ Queen    │
		/// │ 2 │ (0,+1)  │ Normal               │ Rook     │
		/// │ 3 │ (0,+1)  │ Normal               │ Bishop   │
		/// │ 4 │ (0,+1)  │ Normal               │ Knight   │
		/// │ 5 │(-1,+1)  │ Capture              │ Queen    │
		/// │ 6 │(-1,+1)  │ Capture              │ Rook     │
		/// │ 7 │(-1,+1)  │ Capture              │ Bishop   │
		/// │ 8 │(-1,+1)  │ Capture              │ Knight   │
		/// │ 9 │(+1,+1)  │ Capture              │ Queen    │
		/// │10 │(+1,+1)  │ Capture              │ Rook     │
		/// │11 │(+1,+1)  │ Capture              │ Bishop   │
		/// │12 │(+1,+1)  │ Capture              │ Knight   │
		/// └───┴─────────┴───────────────────────┴──────────┘
		/// The geometries (0,+2) and ±(1,+1) En-Passant never promote, so they
		/// are not counted above.
		/// </remarks>
	}
}
