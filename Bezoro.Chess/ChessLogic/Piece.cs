namespace ChessLogic
{
	public enum PieceColor
	{
		White,
		Black
	}

	public enum PieceType
	{
		None,
		Pawn,
		Knight,
		Bishop,
		Rook,
		Queen,
		King
	}

	/// <summary>
	///     Represents a chess piece. This is a struct for memory efficiency when storing the board state.
	/// </summary>
	public readonly struct Piece
	{
		public Piece(PieceType type, PieceColor color)
		{
			Type  = type;
			Color = color;
		}

		/// <summary>
		///     The color of the piece (White or Black)
		/// </summary>
		public PieceColor Color { get; }
		/// <summary>
		///     The type of piece (Pawn, Knight, etc.)
		/// </summary>
		public PieceType Type { get; }

		/// <summary>
		///     Returns a string representation of the piece, primarily for debugging.
		/// </summary>
		public override string ToString()
		{
			if (Type == PieceType.None) return "Empty";
			return $"{Color} {Type}";
		}
	}
}
