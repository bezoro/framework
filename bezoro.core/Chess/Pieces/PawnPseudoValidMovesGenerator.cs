using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Bezoro.Core.Chess.Interfaces;
using Bezoro.Core.Chess.Utils;

namespace Bezoro.Core.Chess.Pieces
{
	/// <summary>
	///     Generates every pseudo-legal pawn move (white perspective; black is mirrored).
	///     ///
	///     <remarks>
	///         The pawn has six geometrical shapes; at the promotion rank three of
	///         them fan out into four promotion pieces, giving 12 concrete moves:
	///         ┌─────────────┬──────────────────────┬──────────┤
	///         │ # │ Geometry│ MoveKind             │ Promotion│
	///         ├───┼─────────┼──────────────────────┼──────────┤
	///         │ 1 │ (0,+1)  │ Normal               │ Queen    │
	///         │ 2 │ (0,+1)  │ Normal               │ Rook     │
	///         │ 3 │ (0,+1)  │ Normal               │ Bishop   │
	///         │ 4 │ (0,+1)  │ Normal               │ Knight   │
	///         │ 5 │(-1,+1)  │ Capture              │ Queen    │
	///         │ 6 │(-1,+1)  │ Capture              │ Rook     │
	///         │ 7 │(-1,+1)  │ Capture              │ Bishop   │
	///         │ 8 │(-1,+1)  │ Capture              │ Knight   │
	///         │ 9 │(+1,+1)  │ Capture              │ Queen    │
	///         │10 │(+1,+1)  │ Capture              │ Rook     │
	///         │11 │(+1,+1)  │ Capture              │ Bishop   │
	///         │12 │(+1,+1)  │ Capture              │ Knight   │
	///         └───┴─────────┴───────────────────────┴──────────┘
	///         The geometries (0,+2) and ±(1,+1) En-Passant never promote, so they
	///         are not counted above.
	///     </remarks>
	/// </summary>
	public sealed class PawnPseudoValidMovesGenerator : IPseudoMoveGenerator
	{
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

		/*───────────────────────────── Public API ─────────────────────────────*/

		public IEnumerable<Move> Generate(IChessBoardModel board, IChessPieceModel piece)
		{
			var pawn = ValidateArgumentsAndCast(board, piece);

			var from = GetPawnPosition(board, pawn);
			if (from is null) yield break;

			foreach (var move in GenerateMoves(board, pawn, from.Value))
			{
				yield return move;
			}
		}

	#endregion

		private static BoardPosition? GetPawnPosition(IChessBoardModel board, PawnModel pawn) =>
			board.GetPosition(pawn);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static bool IsPromotionRank(PlayerColor color, int rank, IChessBoardModel board) =>
			color == PlayerColor.White ? rank == board.Height - 1 : rank == 0;

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static bool IsTemplateApplicable(in MoveTemplate tpl, PawnModel pawn) =>
			!(tpl.FirstMoveOnly && pawn.HasMoved);

		private IEnumerable<Move> BuildMoves(
			IChessBoardModel board,
			PawnModel pawn,
			MoveTemplate tpl,
			BoardPosition from,
			int toFile,
			int toRank)
		{
			var to = new BoardPosition(toFile, toRank);

			if (IsPromotionRank(pawn.Color, toRank, board))
			{
				var type = typeof(PromotionPieceType);
				foreach (PromotionPieceType promo in Enum.GetValues(type))
				{
					yield return Move.Promotion(from, to, pawn.Color, promo);
				}
			}
			else
			{
				yield return new(from, to, pawn.Color, tpl.Kind);
			}
		}

		private IEnumerable<Move> GenerateMoves(
			IChessBoardModel board,
			PawnModel pawn,
			BoardPosition from)
		{
			var dir  = pawn.Direction; // +1 (white) / –1 (black)
			var file = from.Column;
			var rank = from.Row;

			for (var i = 0 ; i < Templates.Length ; i++)
			{
				var tpl = Templates[i];
				if (!IsTemplateApplicable(tpl, pawn)) continue;

				var toFile = file + tpl.Dx;
				var toRank = rank + tpl.Dy * dir;

				if (!board.IsInside(toFile, toRank)) continue;

				foreach (var move in BuildMoves(board, pawn, tpl, from, toFile, toRank))
				{
					yield return move;
				}
			}
		}

		private static PawnModel ValidateArgumentsAndCast(IChessBoardModel board, IChessPieceModel piece)
		{
			if (board is null) throw new ArgumentNullException(nameof(board));
			if (piece is null) throw new ArgumentNullException(nameof(piece));
			if (piece is not PawnModel pawn)
				throw new ArgumentException("Generator received a non-pawn piece.", nameof(piece));

			return pawn;
		}

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
	}
}
