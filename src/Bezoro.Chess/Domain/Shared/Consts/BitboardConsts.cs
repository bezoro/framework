using Bezoro.Chess.Domain.Types.Structs;

namespace Bezoro.Chess.Domain.Shared.Consts
{
	internal static class BitboardConsts
	{
		/// <summary>
		///     Pre-defined bitboard constants for initial black piece positions.
		///     Arranged in order: pawns, knights, bishops, rooks, queen, king.
		///     Example: 0xFF_FF represents black pawns in their starting position (rank 7).
		/// </summary>
		public static readonly ColorBitboards BlackPieces = new(
			0xFF_FF_00_00_00_00_00_00,
			0x42_00_00_00_00_00_00_00,
			0x24_00_00_00_00_00_00_00,
			0x81_00_00_00_00_00_00_00,
			0x08_00_00_00_00_00_00_00,
			0x10_00_00_00_00_00_00_00);
		/// <summary>
		///     Pre-defined bitboard constants for initial white piece positions.
		///     Arranged in order: pawns, knights, bishops, rooks, queen, king.
		///     Example: 0xFF_FF represents white pawns in their starting position (rank 2).
		/// </summary>
		public static readonly ColorBitboards WhitePieces = new(
			0x00_00_00_00_00_00_FF_FF,
			0x00_00_00_00_00_00_00_42,
			0x00_00_00_00_00_00_00_24,
			0x00_00_00_00_00_00_00_81,
			0x00_00_00_00_00_00_00_08,
			0x00_00_00_00_00_00_00_10);
		/// <summary>
		///     Classical initial position – created once and cached.
		/// </summary>
		public static readonly BoardBitboards StartPosition = new(WhitePieces, BlackPieces);
	}
}
