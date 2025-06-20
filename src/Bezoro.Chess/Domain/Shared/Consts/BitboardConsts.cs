using Bezoro.Chess.Domain.Types.Structs;

namespace Bezoro.Chess.Domain.Shared.Consts
{
	internal static class BitboardConsts
	{
		/// <summary>
		///     Classical initial position – created once and cached.
		/// </summary>
		public static readonly BoardBitboards StartPosition = new(
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
	}
}
