namespace Bezoro.Chess.ChessLogic
{
	/// <summary>
	///     Well-known FEN strings that are reused in tests and demo code.
	/// </summary>
	public static class FenStrings
	{
		/// <summary>
		///     An entirely empty board (useful for isolated piece-movement tests).
		/// </summary>
		public const string EmptyBoard =
			"8/8/8/8/8/8/8/8 w - - 0 1";

		/// <summary>
		///     A simple KPK (King &amp; Pawn vs King) end-game position, white to move.
		/// </summary>
		public const string KpkExample =
			"8/8/8/2k5/8/5K2/4P3/8 w - - 0 1";
		/// <summary>
		///     The standard start position of an orthodox chess game.
		/// </summary>
		public const string StandardStart =
			"rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1";
	}
}
