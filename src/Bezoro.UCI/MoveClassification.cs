namespace UCIEngine.Models
{
	/// <summary>
	///     Represents a legal chess move and its specific characteristics,
	///     such as whether it's a capture, castling, or promotion.
	///     This is used to provide detailed feedback in a user interface.
	/// </summary>
	public class MoveClassification
	{
		/// <summary>
		///     Initializes a new instance of the <see cref="MoveClassification" /> class.
		/// </summary>
		/// <param name="move">The move in long algebraic notation.</param>
		public MoveClassification(string move)
		{
			Move = move;
		}

		/// <summary>
		///     True if the move is a quiet, non-special move.
		/// </summary>
		public bool IsNormalMove => !IsCapture && !IsCastling && !IsPromotion;

		/// <summary>
		///     True if the move is a standard capture or an en passant capture.
		/// </summary>
		public bool IsCapture { get; init; }

		/// <summary>
		///     True if the move is a castling move (e.g., "e1g1" or "e8c8").
		/// </summary>
		public bool IsCastling { get; init; }

		/// <summary>
		///     True if the move is an en passant capture.
		/// </summary>
		public bool IsEnPassant { get; init; }

		/// <summary>
		///     True if the move is a pawn promotion.
		/// </summary>
		public bool IsPromotion { get; init; }

		/// <summary>
		///     The move in long algebraic notation (e.g., "e2e4", "e1g1", "a7a8q").
		/// </summary>
		public string Move { get; init; }
	}
}
