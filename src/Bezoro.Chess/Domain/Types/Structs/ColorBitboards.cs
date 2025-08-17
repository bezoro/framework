namespace Bezoro.Chess.Domain.Types.Structs
{
	/// <summary>
	///     A compact, immutable container for the six piece-specific bitboards of one color.
	/// </summary>
	internal readonly struct ColorBitboards
	{
		public ColorBitboards(ulong pawns, ulong knights, ulong bishops, ulong rooks, ulong queens, ulong king)
		{
			Pawns   = pawns;
			Knights = knights;
			Bishops = bishops;
			Rooks   = rooks;
			Queens  = queens;
			King    = king;
		}

		public ulong Bishops { get; }
		public ulong King    { get; }
		public ulong Knights { get; }
		public ulong Pawns   { get; }
		public ulong Queens  { get; }
		public ulong Rooks   { get; }
	}
}
