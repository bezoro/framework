using System;
using System.Collections.Generic;
using Bezoro.Chess.Abstractions.Interfaces;
using Bezoro.Chess.Board;
using Bezoro.Chess.Common.Enums;
using Bezoro.Chess.Common.Extensions;
using Bezoro.Chess.Game.Models;
using Bezoro.Chess.Moves.Models;
using Bezoro.Chess.Pieces.Models;

namespace Bezoro.Chess.Moves.Services
{
	//     Generates every pseudo-legal pawn move (white perspective; black is mirrored).
	//
	//
	//         The pawn has six geometrical shapes; at the promotion rank three of
	//         them fan out into four promotion pieces, giving 12 concrete moves:
	//         ┌─────────────┬──────────────────────┬──────────┤
	//         │ # │ Geometry│ MoveKind             │ Promotion│
	//         ├───┼─────────┼──────────────────────┼──────────┤
	//         │ 1 │ (0,+1)  │ Normal               │ Queen    │
	//         │ 2 │ (0,+1)  │ Normal               │ Rook     │
	//         │ 3 │ (0,+1)  │ Normal               │ Bishop   │
	//         │ 4 │ (0,+1)  │ Normal               │ Knight   │
	//         │ 5 │(-1,+1)  │ Capture              │ Queen    │
	//         │ 6 │(-1,+1)  │ Capture              │ Rook     │
	//         │ 7 │(-1,+1)  │ Capture              │ Bishop   │
	//         │ 8 │(-1,+1)  │ Capture              │ Knight   │
	//         │ 9 │(+1,+1)  │ Capture              │ Queen    │
	//         │10 │(+1,+1)  │ Capture              │ Rook     │
	//         │11 │(+1,+1)  │ Capture              │ Bishop   │
	//         │12 │(+1,+1)  │ Capture              │ Knight   │
	//         └───┴─────────┴───────────────────────┴──────────┘
	//         The geometries (0,+2) and ±(1,+1) En-Passant never promote, so they
	//         are not counted above.

	/// <summary>
	///     Emits every geometrically legal pawn move.
	///     The white template is mirrored for black by multiplying
	///     <c>dy</c> with <c>pawn.Direction</c> (+1 / −1).
	///     Occupancy, check-legality and en-passant availability are
	///     handled by higher layers.
	/// </summary>
	public sealed class PawnPseudoValidMovesGenerator : IPseudoMoveGenerator
	{
		// Using an ordinary static array avoids span-lifetime issues when
		// we 'yield' from an iterator.
		private static readonly PawnMoveTemplate[] _Templates =
		{
			new(0, +1, MoveKind.Normal),
			new(0, +2, MoveKind.Normal, true),
			new(-1, +1, MoveKind.Capture),
			new(+1, +1, MoveKind.Capture),
			new(-1, +1, MoveKind.EnPassant),
			new(+1, +1, MoveKind.EnPassant)
		};

	#region Interface Implementations

		public IEnumerable<Move> Generate(GameModel game, IChessPieceModel piece)
		{
			if (game is null) throw new ArgumentNullException(nameof(game));

			var board = game.Board;
			var pawn  = EnsurePawnPiece(board, piece);

			var from = board.GetPosition(pawn);
			if (from is null)
				yield break;

			foreach (var mv in GenerateMoves(board, pawn, from.Value))
			{
				yield return mv;
			}
		}

	#endregion

		private static bool IsPromotionRank(PlayerColor color, int rank, IChessBoardModel board) =>
			color == PlayerColor.White ? rank == board.Height - 1 : rank == 0;

		private static IEnumerable<Move> BuildMoves(
			BoardPosition from,
			BoardPosition to,
			PawnModel pawn,
			MoveKind kind,
			IChessBoardModel board)
		{
			if (IsPromotionRank(pawn.Color, to.Row, board))
			{
				foreach (PromotionPieceType promo in Enum.GetValues(typeof(PromotionPieceType)))
				{
					if (promo != PromotionPieceType.None)
						yield return Move.Promotion(from, to, pawn.Color, promo);
				}
			}
			else
				yield return new(from, to, pawn.Color, pawn.GetPieceType(), kind);
		}

		private static IEnumerable<Move> GenerateMoves(
			IChessBoardModel board,
			PawnModel pawn,
			BoardPosition from)
		{
			var dir  = pawn.Direction; // +1 (white) / –1 (black)
			var file = from.Column;
			var rank = from.Row;

			foreach (var tpl in _Templates)
			{
				if (tpl.OnlyOnFirstMove && pawn.HasMoved)
					continue;

				var toFile = file + tpl.Dx;
				var toRank = rank + tpl.Dy * dir;

				if (!board.IsInside(toFile, toRank))
					continue;

				// En-passant never promotes; other kinds may promote
				// later in BuildMoves, so we don’t filter them out here.
				if (tpl.Kind == MoveKind.EnPassant &&
					IsPromotionRank(pawn.Color, toRank, board))
					continue;

				var to = new BoardPosition(toFile, toRank);

				foreach (var move in BuildMoves(from, to, pawn, tpl.Kind, board))
				{
					yield return move;
				}
			}
		}

		private static PawnModel EnsurePawnPiece(IChessBoardModel board, IChessPieceModel piece)
		{
			if (board is null) throw new ArgumentNullException(nameof(board));
			if (piece is null) throw new ArgumentNullException(nameof(piece));
			if (piece is not PawnModel pawn)
				throw new ArgumentException("Generator received a non-pawn piece.", nameof(piece));

			return pawn;
		}

		private readonly struct PawnMoveTemplate
		{
			public PawnMoveTemplate(int dx, int dy, MoveKind kind, bool onlyOnFirstMove = false)
			{
				Dx              = dx;
				Dy              = dy;
				Kind            = kind;
				OnlyOnFirstMove = onlyOnFirstMove;
			}

			public readonly bool OnlyOnFirstMove;

			public readonly int      Dx;
			public readonly int      Dy;
			public readonly MoveKind Kind;
		}
	}
}
