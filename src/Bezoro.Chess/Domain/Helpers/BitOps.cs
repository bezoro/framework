using System.Runtime.CompilerServices;

namespace Bezoro.Chess.Domain.Helpers
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
}
