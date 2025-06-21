using System;
using Bezoro.Chess.Domain.Shared.Enums;
using Bezoro.Chess.Domain.Types.Structs;

namespace Bezoro.Chess.Domain.Types.Records
{
	/// <summary>
	///     Immutable value object that wraps the 12 underlying bitboards and offers
	///     high-level helpers.
	/// </summary>
	internal sealed record Board(BoardBitboards Bitboards)
	{
		private static readonly Piece BB = new(PieceType.Bishop, PieceColor.Black);
		private static readonly Piece BK = new(PieceType.King, PieceColor.Black);
		private static readonly Piece BN = new(PieceType.Knight, PieceColor.Black);

		private static readonly Piece BP = new(PieceType.Pawn, PieceColor.Black);
		private static readonly Piece BQ = new(PieceType.Queen, PieceColor.Black);
		private static readonly Piece BR = new(PieceType.Rook, PieceColor.Black);
		private static readonly Piece WB = new(PieceType.Bishop, PieceColor.White);
		private static readonly Piece WK = new(PieceType.King, PieceColor.White);
		private static readonly Piece WN = new(PieceType.Knight, PieceColor.White);
		/* ───────────────── Cached immutable piece instances ───────────────── */
		private static readonly Piece WP = new(PieceType.Pawn, PieceColor.White);
		private static readonly Piece WQ = new(PieceType.Queen, PieceColor.White);
		private static readonly Piece WR = new(PieceType.Rook, PieceColor.White);

		/* Constructs a new BoardBitboards from 12 raw ulong values */
		private static BoardBitboards Create(
			ulong wP, ulong wN, ulong wB, ulong wR, ulong wQ, ulong wK,
			ulong bP, ulong bN, ulong bB, ulong bR, ulong bQ, ulong bK)
		{
			var white = new ColorBitboards(wP, wN, wB, wR, wQ, wK);
			var black = new ColorBitboards(bP, bN, bB, bR, bQ, bK);
			return new BoardBitboards(white, black);
		}

		public Board RemovePiece(Position square) => SetPiece(square, default);

		public Board SetPiece(Position square, Piece piece)
		{
			EnsureOnBoard(square);

			Piece boardPiece = GetPiece(square);
			if (boardPiece.Equals(piece))
			{
				return this;
			}

			int   idx = square.Row * 8 + square.Col;
			ulong msk = 1UL << idx;

			// 1. Clear the bit everywhere
			BoardBitboards cleared = ClearBit(Bitboards, msk);

			bool needsWrite = (cleared.Occupied & msk) == 0;
			if (!needsWrite)
			{
				return this;
			}

			// 2. If we’re adding a piece, set the bit in the correct board
			BoardBitboards updated = piece.IsValid
				? SetBit(cleared, msk, piece)
				: cleared;

			return new Board(updated);
		}

		/// <summary>
		///     Applies several (square, piece) changes in one sweep and returns a new board.
		///     Pass <c>default</c> (or an invalid piece) to clear a square.
		/// </summary>
		public Board SetPieces(params (Position square, Piece piece)[] placements)
		{
			// Fast path: nothing to do
			if (placements is null || placements.Length == 0)
			{
				return this;
			}

			BoardBitboards bb = Bitboards; // work on a local copy

			foreach ((Position square, Piece piece) in placements)
			{
				EnsureOnBoard(square);

				int   idx = square.Row * 8 + square.Col;
				ulong msk = 1UL << idx;

				bb = piece.IsValid
					? SetBit(bb, msk, piece)
					: ClearBit(bb, msk);
			}

			// If nothing really changed, just return the current instance
			if (bb.Equals(Bitboards))
			{
				return this;
			}

			return new Board(bb);
		}

		public Piece GetPiece(Position square)
		{
			EnsureOnBoard(square);

			int   idx = square.Row * 8 + square.Col;
			ulong msk = 1UL << idx;

			if ((Bitboards.WhitePawns & msk) != 0)
			{
				return WP;
			}

			if ((Bitboards.WhiteKnights & msk) != 0)
			{
				return WN;
			}

			if ((Bitboards.WhiteBishops & msk) != 0)
			{
				return WB;
			}

			if ((Bitboards.WhiteRooks & msk) != 0)
			{
				return WR;
			}

			if ((Bitboards.WhiteQueens & msk) != 0)
			{
				return WQ;
			}

			if ((Bitboards.WhiteKing & msk) != 0)
			{
				return WK;
			}

			if ((Bitboards.BlackPawns & msk) != 0)
			{
				return BP;
			}

			if ((Bitboards.BlackKnights & msk) != 0)
			{
				return BN;
			}

			if ((Bitboards.BlackBishops & msk) != 0)
			{
				return BB;
			}

			if ((Bitboards.BlackRooks & msk) != 0)
			{
				return BR;
			}

			if ((Bitboards.BlackQueens & msk) != 0)
			{
				return BQ;
			}

			if ((Bitboards.BlackKing & msk) != 0)
			{
				return BK;
			}

			return default;
		}

		private static BoardBitboards ClearBit(in BoardBitboards bb, ulong msk)
			=> Create(
				bb.WhitePawns & ~msk, bb.WhiteKnights & ~msk, bb.WhiteBishops & ~msk,
				bb.WhiteRooks & ~msk, bb.WhiteQueens  & ~msk, bb.WhiteKing    & ~msk,
				bb.BlackPawns & ~msk, bb.BlackKnights & ~msk, bb.BlackBishops & ~msk,
				bb.BlackRooks & ~msk, bb.BlackQueens  & ~msk, bb.BlackKing    & ~msk);

		private static BoardBitboards SetBit(in BoardBitboards bb, ulong msk, in Piece p)
		{
			// Copy current values into locals we can mutate
			ulong wP = bb.WhitePawns,
				  wN = bb.WhiteKnights,
				  wB = bb.WhiteBishops,
				  wR = bb.WhiteRooks,
				  wQ = bb.WhiteQueens,
				  wK = bb.WhiteKing,
				  bP = bb.BlackPawns,
				  bN = bb.BlackKnights,
				  bB = bb.BlackBishops,
				  bR = bb.BlackRooks,
				  bQ = bb.BlackQueens,
				  bK = bb.BlackKing;

			if (p.Color == PieceColor.White)
			{
				switch (p.Type)
				{
					case PieceType.Pawn:   wP |= msk; break;
					case PieceType.Knight: wN |= msk; break;
					case PieceType.Bishop: wB |= msk; break;
					case PieceType.Rook:   wR |= msk; break;
					case PieceType.Queen:  wQ |= msk; break;
					case PieceType.King:   wK |= msk; break;
				}
			}
			else
			{
				switch (p.Type)
				{
					case PieceType.Pawn:   bP |= msk; break;
					case PieceType.Knight: bN |= msk; break;
					case PieceType.Bishop: bB |= msk; break;
					case PieceType.Rook:   bR |= msk; break;
					case PieceType.Queen:  bQ |= msk; break;
					case PieceType.King:   bK |= msk; break;
				}
			}

			return Create(wP, wN, wB, wR, wQ, wK, bP, bN, bB, bR, bQ, bK);
		}

		private static void EnsureOnBoard(in Position pos)
		{
			if ((uint)pos.Row >= 8 || (uint)pos.Col >= 8)
			{
				throw new ArgumentOutOfRangeException(nameof(pos), $"Invalid square: {pos}");
			}
		}
	}
}
