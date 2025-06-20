using System.Runtime.CompilerServices;

namespace Bezoro.Chess.Domain.Types.Structs
{
	/// <summary>
	///     Low-level static helpers used by move-generation code.
	///     No external dependencies; <see cref="TrailingZeroCount" /> is implemented
	///     with a De Bruijn multiplication, so <c>System.Numerics.BitOperations</c>
	///     is *not* required.
	/// </summary>
	internal static class BitOps
	{
		// ───────────────────────────────── Pop / Tzcnt ─────────────────────────────────
		// De Bruijn constant and lookup table – see
		// "Using de Bruijn Sequences to Index a Set Bit" (Leiserson et al.).
		private const ulong DeBruijn64 = 0x03f79d71b4cb0a89UL;
		private static readonly int[] Index64 =
		{
			0, 1, 48, 2, 57, 49, 28, 3,
			61, 58, 50, 42, 38, 29, 17, 4,
			62, 55, 59, 36, 53, 51, 43, 22,
			45, 39, 33, 30, 24, 18, 12, 5,
			63, 47, 56, 27, 60, 41, 37, 16,
			54, 35, 52, 21, 44, 32, 23, 11,
			46, 26, 40, 15, 34, 20, 31, 10,
			25, 14, 19, 9, 13, 8, 7, 6
		};
		public static readonly ulong[] KnightAttackMask = BuildKnightAttacks();
		public static readonly ulong[] KingAttackMask   = BuildKingAttacks();

		// ─────────────────────────────── Attack tables ────────────────────────────────

		// ───────────────────────────────── Bit helpers ─────────────────────────────────
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool HasBit(this ulong bb, int square) => (bb & 1UL << square) != 0;

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool HasBit(this ulong bb, ulong mask) => (bb & mask) != 0;

		/// <summary>
		///     Pops and returns the index of the least-significant set bit in <paramref name="bb" />.
		///     Equivalent to x86 TZCNT+BMI "blsi" sequence but portable.
		/// </summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static int PopLsb(ref ulong bb)
		{
			int idx = TrailingZeroCount(bb);
			bb &= bb - 1; // clear LS1B
			return idx;
		}

		/// <summary>Returns the number of trailing zero bits (0-63). Undefined for <paramref name="v" /> == 0.</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static int TrailingZeroCount(ulong v)
		{
			// Isolate LS1B
			ulong lsb = v & unchecked( (ulong)-(long)v );
			// Multiply with De Bruijn and use top 6 bits as index
			var idx = (int)(lsb * DeBruijn64 >> 58);
			return Index64[idx];
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static ulong ClearBit(this ulong bb, int square) => bb & ~(1UL << square);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static ulong SetBit(this ulong bb, int square) => bb | 1UL << square;

		private static ulong[] BuildKingAttacks()
		{
			var arr = new ulong[64];
			for (var sq = 0 ; sq < 64 ; ++sq)
			{
				int   f    = sq & 7, r = sq >> 3;
				ulong mask = 0;

				for (int df = -1 ; df <= 1 ; ++df)
				{
					for (int dr = -1 ; dr <= 1 ; ++dr)
					{
						if (df == 0 && dr == 0)
						{
							continue;
						}

						int nf = f + df, nr = r + dr;
						if (nf is >= 0 and < 8 && nr is >= 0 and < 8)
						{
							mask |= 1UL << nr * 8 + nf;
						}
					}
				}

				arr[sq] = mask;
			}

			return arr;
		}

		private static ulong[] BuildKnightAttacks()
		{
			var arr = new ulong[64];
			for (var sq = 0 ; sq < 64 ; ++sq)
			{
				int   f    = sq & 7, r = sq >> 3;
				ulong mask = 0;

				void Add(int df, int dr)
				{
					int nf = f + df, nr = r + dr;
					if (nf is >= 0 and < 8 && nr is >= 0 and < 8)
					{
						mask |= 1UL << nr * 8 + nf;
					}
				}

				Add(+1, +2);
				Add(+2, +1);
				Add(+2, -1);
				Add(+1, -2);
				Add(-1, -2);
				Add(-2, -1);
				Add(-2, +1);
				Add(-1, +2);

				arr[sq] = mask;
			}

			return arr;
		}
	}

	/// <summary>
	///     Immutable collection of the 12 piece-specific bitboards used by the engine.
	///     Also provides on-the-fly occupancy masks (WhitePieces, BlackPieces, etc.).
	///     Square numbering = little-endian ranks:
	///     0  1 …  7   →  A1 … H1
	///     8  9 … 15   →  A2 … H2
	///     …
	///     56 57 … 63   →  A8 … H8
	/// </summary>
	public readonly struct BoardBitboards
	{
		/// <summary>Return the classical initial position.</summary>
		public static BoardBitboards FromStartPosition() =>
			new(
				0x0000_0000_0000_FF00UL, // WP
				0x0000_0000_0000_0042UL, // WN
				0x0000_0000_0000_0024UL, // WB
				0x0000_0000_0000_0081UL, // WR
				0x0000_0000_0000_0008UL, // WQ
				0x0000_0000_0000_0010UL, // WK
				0x00FF_0000_0000_0000UL, // BP
				0x4200_0000_0000_0000UL, // BN
				0x2400_0000_0000_0000UL, // BB
				0x8100_0000_0000_0000UL, // BR
				0x0800_0000_0000_0000UL, // BQ
				0x1000_0000_0000_0000UL  // BK
			);

		// ── Black ────────────────────────────────────────────────────────────
		public readonly ulong BlackPawns,
							  BlackKnights,
							  BlackBishops,
							  BlackRooks,
							  BlackQueens,
							  BlackKing;
		// ── White ────────────────────────────────────────────────────────────
		public readonly ulong WhitePawns,
							  WhiteKnights,
							  WhiteBishops,
							  WhiteRooks,
							  WhiteQueens,
							  WhiteKing;

		public ulong BlackPieces => BlackPawns   |
									BlackKnights |
									BlackBishops |
									BlackRooks   |
									BlackQueens  |
									BlackKing;
		public ulong Empty => ~Occupied;

		public ulong Occupied => WhitePieces | BlackPieces;

		// ── Derived masks (computed on demand, thus always in sync) ──────────
		public ulong WhitePieces => WhitePawns   |
									WhiteKnights |
									WhiteBishops |
									WhiteRooks   |
									WhiteQueens  |
									WhiteKing;

		public BoardBitboards(
			ulong whitePawns, ulong whiteKnights, ulong whiteBishops,
			ulong whiteRooks, ulong whiteQueens, ulong whiteKing,
			ulong blackPawns, ulong blackKnights, ulong blackBishops,
			ulong blackRooks, ulong blackQueens, ulong blackKing)
		{
			WhitePawns   = whitePawns;
			WhiteKnights = whiteKnights;
			WhiteBishops = whiteBishops;
			WhiteRooks   = whiteRooks;
			WhiteQueens  = whiteQueens;
			WhiteKing    = whiteKing;
			BlackPawns   = blackPawns;
			BlackKnights = blackKnights;
			BlackBishops = blackBishops;
			BlackRooks   = blackRooks;
			BlackQueens  = blackQueens;
			BlackKing    = blackKing;
		}
	}
}
