namespace Bezoro.Chess.Domain.Shared.Consts
{
    /// <summary>
    ///     Relative attack offsets for each piece.<br />
    ///     Tuples are arranged so that, when read left-to-right and top-to-bottom,
    ///     they resemble the real-world board layout
    ///     (negative = up/left, positive = down/right).
    /// </summary>
    internal static class AttackVectors
	{
		// ───────────────────────────────────────────── Bishop ─────────────────────────────────────────────
		// Col    -1   0   1
		// Row  ┌────────────┐
		// -1   │  B   ·   B │
		//  0   │  ·   O   · │
		//  1   │  B   ·   B │
		//      └────────────┘
		public static readonly (int Row, int Col)[] BishopAttackVectors =
		{
			(-1, -1), (-1, 1),
			(1, -1), (1, 1)
		};

		// ───────────────────────────────────────────── King ──────────────────────────────────────────────
		// Col    -1   0   1
		// Row  ┌─────────────┐
		// -1   │  K   K   K  │
		//  0   │  K   O   K  │
		//  1   │  K   K   K  │
		//      └─────────────┘
		public static readonly (int Row, int Col)[] KingAttackVectors =
		{
			(-1, -1), (-1, 0), (-1, 1),
			(0, -1), (0, 1),
			(1, -1), (1, 0), (1, 1)
		};

		// ───────────────────────────────────────────── Knight ────────────────────────────────────────────
		//  Col →  -2  -1   0   1   2
		//  Row  ┌────────────────────┐
		//  -2   │  ·   N   ·   N   · │
		//  -1   │  N   ·   ·   ·   N │
		//   0   │  ·   ·   O   ·   · │
		//   1   │  N   ·   ·   ·   N │
		//   2   │  ·   N   ·   N   · │
		//       └────────────────────┘
		public static readonly (int Row, int Col)[] KnightAttackVectors =
		{
			(-2, -1), (-2, 1),
			(-1, -2), (-1, 2),
			(1, -2), (1, 2),
			(2, -1), (2, 1)
		};

		// ───────────────────────────────────────────── Pawn (white-orientated) ───────────────────────────
		// Col   -1   0   1
		// Row  ┌────────────┐
		// -1   │  P   ·   P │
		//  0   │  ·   O   · │
		//      └────────────┘
		/// <summary>
		///     These are *attack* offsets, not forward-move offsets. For black pawns simply invert the sign of the row
		///     component.
		/// </summary>
		public static readonly (int Row, int Col)[] PawnAttackVectors =
		{
			(-1, -1), // up-left  (white perspective)
			(-1, 1)   // up-right
		};

		// ───────────────────────────────────────────── Queen ─────────────────────────────────────────────
		// (Bishop + Rook directions)
		// Col    -1   0   1
		// Row  ┌─────────────┐
		// -1   │  Q   Q   Q  │
		//  0   │  Q   O   Q  │
		//  1   │  Q   Q   Q  │
		//      └─────────────┘
		public static readonly (int Row, int Col)[] QueenAttackVectors =
		{
			(-1, -1), (-1, 0), (-1, 1),
			(0, -1), (0, 1),
			(1, -1), (1, 0), (1, 1)
		};

		// ───────────────────────────────────────────── Rook ──────────────────────────────────────────────
		// Col   -1   0   1
		// Row  ┌─────────────┐
		// -1   │  ·   R   ·  │
		//  0   │  R   O   R  │
		//  1   │  ·   R   ·  │
		//      └─────────────┘
		public static readonly (int Row, int Col)[] RookAttackVectors =
		{
			(-1, 0), // ↑
			(1, 0),  // ↓
			(0, -1), // ←
			(0, 1)   // →
		};
	}
}
