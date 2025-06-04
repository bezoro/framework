using System;
using System.Collections.Generic;
using Bezoro.Core.Chess.Interfaces;
using Bezoro.Core.Chess.Utils;

namespace Bezoro.Core.Chess
{
	/// <summary>
	///     Immutable representation of a board position that can be cloned and
	///     queried without touching the live <see cref="BoardModel" /> instance.
	/// </summary>
	public sealed class BoardSnapshot
	{
		/*──────────────────────────────────────────────────────────*/
		/*  Construction / cloning                                  */
		/*──────────────────────────────────────────────────────────*/

		internal BoardSnapshot(IChessPieceModel?[,] pieces)
		{
			if (pieces == null) throw new ArgumentNullException(nameof(pieces));

			Width  = pieces.GetLength(0);
			Height = pieces.GetLength(1);

			_pieces = new IChessPieceModel?[Width, Height];
			Array.Copy(pieces, _pieces, pieces.Length);
		}

		/*──────────────────────────────────────────────────────────*/
		/*  Indexer                                                 */
		/*──────────────────────────────────────────────────────────*/

		public IChessPieceModel? this[int file, int rank]
		{
			get
			{
				if (!_pieces.IsInside(file, rank))
				{
					throw new ArgumentOutOfRangeException(
						$"Coordinates ({file},{rank}) outside board bounds.");
				}

				return _pieces[file, rank];
			}
			private set
			{
				if (!_pieces.IsInside(file, rank))
				{
					throw new ArgumentOutOfRangeException(
						$"Coordinates ({file},{rank}) outside board bounds.");
				}

				_pieces[file, rank] = value;
			}
		}
		private readonly IChessPieceModel?[,] _pieces;
		public           int                  Height { get; }

		public int Width { get; }

		public BoardPosition? FindKing(PlayerColor side)
		{
			foreach (var (piece, pos) in EnumeratePieces())
			{
				if (GetPieceType(piece) == ChessPieceType.King && piece.Color == side)
					return pos;
			}

			return null;
		}

		public BoardSnapshot ApplyMove(BoardPosition from, BoardPosition to)
		{
			if (from == null) throw new ArgumentNullException(nameof(from));
			if (to   == null) throw new ArgumentNullException(nameof(to));

			var movingPiece = this[from.File, from.Rank];
			if (movingPiece is null)
			{
				throw new InvalidOperationException(
					$"No piece at {from}.");
			}

			var next = Clone();

			next[to.File, to.Rank]     = movingPiece; // capture / overwrite
			next[from.File, from.Rank] = null;

			return next;
		}

		public BoardSnapshot Clone()
		{
			var copy = new IChessPieceModel?[Width, Height];
			Array.Copy(_pieces, copy, _pieces.Length);
			return new(copy);
		}

		/*──────────────────────────────────────────────────────────*/
		/*  Attack detection                                        */
		/*──────────────────────────────────────────────────────────*/

		public bool IsSquareAttacked(BoardPosition target, PlayerColor by)
		{
			var (tf, tr) = (target.File, target.Rank);

			/* -------- Knights ----------------------------------------- */
			foreach (var (df, dr) in DirectionVectors.Knight)
			{
				if (_pieces.TryGetPiece(tf + df, tr + dr, out var pN)
					&& pN!.Color         == by
					&& pN.GetPieceType() == ChessPieceType.Knight)
					return true;
			}

			/* -------- Pawns (capture vectors only) -------------------- */
			var pawnDir = by == PlayerColor.White ? +1 : -1;
			foreach (var df in new[] { -1, +1 })
			{
				if (TryGetPiece(tf + df, tr + pawnDir, out var pP)
					&& pP!.Color         == by
					&& pP.GetPieceType() == ChessPieceType.Pawn)
					return true;
			}

			/* -------- King (adjacent) --------------------------------- */
			foreach (var (df, dr) in DirectionVectors.King)
			{
				if (TryGetPiece(tf + df, tr + dr, out var pK)
					&& pK!.Color         == by
					&& pK.GetPieceType() == ChessPieceType.King)
					return true;
			}

			/* -------- Sliders: rook / bishop / queen ------------------ */

			// Orthogonal rays – rook or queen
			foreach (var (df, dr) in DirectionVectors.Orthogonal)
			{
				if (RayHitsSlider(tf, tr, df, dr, by, false))
					return true;
			}

			// Diagonal rays – bishop or queen
			foreach (var (df, dr) in DirectionVectors.Diagonal)
			{
				if (RayHitsSlider(tf, tr, df, dr, by, true))
					return true;
			}

			return false;
		}

		/*──────────────────────────────────────────────────────────*/
		/*  Basic enumeration utilities                             */
		/*──────────────────────────────────────────────────────────*/

		public IEnumerable<(IChessPieceModel piece, BoardPosition pos)> EnumeratePieces()
		{
			for (var f = 0 ; f < Width ; ++f)
			{
				for (var r = 0 ; r < Height ; ++r)
				{
					if (_pieces[f, r] is { } p)
						yield return (p, new(f, r));
				}
			}
		}

		private bool RayHitsSlider(
			int fStart,
			int rStart,
			int df,
			int dr,
			PlayerColor attacker,
			bool diag)
		{
			var f = fStart + df;
			var r = rStart + dr;

			foreach (var (nf, nr) in _pieces.Ray(fStart, rStart, df, dr))
			{
				var piece = _pieces[nf, nr];

				// Empty square – keep going.
				if (piece is null)
					continue;

				// We hit a piece; see if it is an attacking slider.
				if (piece.Color == attacker)
				{
					var type = piece.GetPieceType();
					if (type == ChessPieceType.Queen) return true;
					if (diag  && type == ChessPieceType.Bishop) return true;
					if (!diag && type == ChessPieceType.Rook) return true;
				}

				// Either friendly blocker or wrong piece type – stop the ray.
				break;
			}

			return false;
		}

		/*──────────────────────────────────────────────────────────*/
		/*  Helpers                                                 */
		/*──────────────────────────────────────────────────────────*/

		private bool TryGetPiece(int f, int r, out IChessPieceModel? piece) =>
			_pieces.TryGetPiece(f, r, out piece);

		private static ChessPieceType GetPieceType(IChessPieceModel piece) =>
			piece.GetPieceType();
	}
}
