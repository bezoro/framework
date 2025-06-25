using Bezoro.Chess.Domain.Types.Structs;

namespace Bezoro.Chess.Domain.Shared.Consts
{
	internal static class BitboardConsts
	{
		/// <summary>Empty board with no pieces.</summary>
		public static readonly BoardBitboards EmptyBoard = new(
			new ColorBitboards(0, 0, 0, 0, 0, 0),
			new ColorBitboards(0, 0, 0, 0, 0, 0));

		public static readonly ColorBitboards BlackPieces = new(
			0x00FF_0000_0000_0000UL, // pawns  a7-h7
			0x4200_0000_0000_0000UL, // knights b8, g8
			0x2400_0000_0000_0000UL, // bishops c8, f8
			0x8100_0000_0000_0000UL, // rooks   a8, h8
			0x0800_0000_0000_0000UL, // queen   d8
			0x1000_0000_0000_0000UL  // king    e8
		);
		public static readonly ColorBitboards WhitePieces = new(
			0x0000_0000_0000_FF00UL, // pawns  a2-h2
			0x0000_0000_0000_0042UL, // knights b1, g1
			0x0000_0000_0000_0024UL, // bishops c1, f1
			0x0000_0000_0000_0081UL, // rooks   a1, h1
			0x0000_0000_0000_0008UL, // queen   d1
			0x0000_0000_0000_0010UL  // king    e1
		);
		/// <summary>The classical starting position.</summary>
		public static readonly BoardBitboards StartPosition = new(WhitePieces, BlackPieces);

		/* ─────────────  Correct initial positions (a1 = bit 0) ───────────── */
	}
}
