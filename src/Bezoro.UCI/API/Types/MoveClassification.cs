namespace Bezoro.UCI.API.Types
{
	/// <summary>
	///     Represents a legal chess move and its specific characteristics,
	///     such as whether it's a capture, castling, or promotion.
	///     This is used to provide detailed feedback in a user interface.
	/// </summary>
	public readonly struct MoveClassification
	{
		/// <summary>
		///     Initializes a new instance of the <see cref="MoveClassification" /> class.
		/// </summary>
		/// <param name="move">The move in long algebraic notation.</param>
		public MoveClassification(string move)
		{
			Move        = move;
			IsCapture   = false;
			IsCastling  = false;
			IsEnPassant = false;
			IsPromotion = false;
		}

		/// <summary>
		///     True if the move is a quiet, non-special move.
		/// </summary>
		public bool IsNormal => !IsCapture && !IsCastling && !IsPromotion;
		/// <summary>
		///     The starting square of the move in algebraic notation (e.g., "e2").
		/// </summary>
		public string From => Move[..2];
		/// <summary>
		///     The move in long algebraic notation (e.g., "e2e4", "e1g1", "a7a8q").
		/// </summary>
		public string Move { get; }
		/// <summary>
		///     The destination square of the move in algebraic notation (e.g., "e4").
		/// </summary>
		public string To => Move.Substring(2, 2);
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

		public override string ToString() =>
			Move;
	}
}
