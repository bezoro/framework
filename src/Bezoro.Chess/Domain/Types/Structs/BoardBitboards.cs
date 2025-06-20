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

		public ulong BlackPieces => BlackPawns | BlackKnights | BlackBishops | BlackRooks | BlackQueens | BlackKing;
		public ulong Empty       => ~Occupied;
		public ulong Occupied    => WhitePieces | BlackPieces;
		public ulong WhitePieces => WhitePawns  | WhiteKnights | WhiteBishops | WhiteRooks | WhiteQueens | WhiteKing;

		/// <summary>Return the classical initial position.</summary>
		public static BoardBitboards FromStartPosition() =>
			BitboardConsts.StartPosition;
	}
}
