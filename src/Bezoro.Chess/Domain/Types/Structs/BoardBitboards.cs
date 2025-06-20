using Bezoro.Chess.Domain.Shared.Consts;

namespace Bezoro.Chess.Domain.Types.Structs
{
	/// <summary>
	///     Immutable collection of the 12 piece-specific bitboards used by the engine.
	///     Also provides on-the-fly occupancy masks (WhitePieces, BlackPieces, etc.).
	///     Square numbering = little-endian ranks:
	///     0  1 …  7   →  A1 … H1
	///     8  9 … 15   →  A2 … H2
	///     …
	///     56 57 … 63   →  A8 … H8
	/// </summary>
	internal readonly struct BoardBitboards
	{
		// ── Black ────────────────────────────────────────────────────────────
		public readonly ulong BlackPawns, BlackKnights, BlackBishops, BlackRooks, BlackQueens, BlackKing;
		// ── White ────────────────────────────────────────────────────────────
		public readonly ulong WhitePawns, WhiteKnights, WhiteBishops, WhiteRooks, WhiteQueens, WhiteKing;

		/// <summary>Main, public constructor – uses colour-specific value objects.</summary>
		public BoardBitboards(ColorBitboards white, ColorBitboards black)

		{
			WhitePawns   = white.Pawns;
			WhiteKnights = white.Knights;
			WhiteBishops = white.Bishops;
			WhiteRooks   = white.Rooks;
			WhiteQueens  = white.Queens;
			WhiteKing    = white.King;

			BlackPawns   = black.Pawns;
			BlackKnights = black.Knights;
			BlackBishops = black.Bishops;
			BlackRooks   = black.Rooks;
			BlackQueens  = black.Queens;
			BlackKing    = black.King;
		}

		public ulong BlackPieces => BlackPawns | BlackKnights | BlackBishops | BlackRooks | BlackQueens | BlackKing;
		public ulong Empty       => ~Occupied;
		public ulong Occupied    => WhitePieces | BlackPieces;
		public ulong WhitePieces => WhitePawns  | WhiteKnights | WhiteBishops | WhiteRooks | WhiteQueens | WhiteKing;

		/// <summary>Return the classical initial position.</summary>
		public static BoardBitboards FromStartPosition() =>
			BitboardConsts.StartPosition;
	}
}
