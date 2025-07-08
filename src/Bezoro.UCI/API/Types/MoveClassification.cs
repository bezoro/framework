namespace Bezoro.UCI.API.Types
{
	/// <summary>
	///     Represents a legal chess move and its specific characteristics,
	///     such as whether it's a capture, castling, or promotion.
	///     This is used to provide detailed feedback in a user interface.
	/// </summary>
	public readonly record struct MoveClassification(
		string Move, bool IsCapture = false, bool IsCastling = false, bool IsEnPassant = false,
		bool IsPromotion = false)
	{
		/// <summary>
		///     True if the move is a quiet, non-special move.
		/// </summary>
		public bool IsNormal => !IsCapture && !IsCastling && !IsPromotion;

		/// <summary>
		///     The starting square of the move in algebraic notation (e.g., "e2").
		/// </summary>
		public string From => Move[..2];

		/// <summary>
		///     The destination square of the move in algebraic notation (e.g., "e4").
		/// </summary>
		public string To => Move.Substring(2, 2);
	}
}
