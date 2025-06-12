using System;

namespace ChessLogic
{
	[Flags]
	public enum CastlingRights
	{
		None           = 0,
		WhiteKingside  = 1 << 0,
		WhiteQueenside = 1 << 1,
		BlackKingside  = 1 << 2,
		BlackQueenside = 1 << 3,
		WhiteBoth      = WhiteKingside | WhiteQueenside,
		BlackBoth      = BlackKingside | BlackQueenside,
		All            = WhiteBoth     | BlackBoth
	}

	public record GameState
	{
		/// <summary>
		///     A bitmask representing the current castling availability.
		/// </summary>
		public CastlingRights Castling { get; init; } = CastlingRights.All;

		/// <summary>
		///     The number of full moves in the game. Starts at 1 and increments after Black moves.
		/// </summary>
		public int FullMoveNumber { get; init; } = 1;

		/// <summary>
		///     The number of half-moves since the last capture or pawn advance.
		///     Used for the fifty-move rule.
		/// </summary>
		public int HalfMoveClock { get; init; } = 0;
		/// <summary>
		///     The 8x8 grid of pieces. This IS the board.
		///     It's the single source of truth for all piece positions.
		/// </summary>
		public Piece[,] PiecePositions { get; init; } = new Piece[8, 8];

		/// <summary>
		///     The color of the player whose turn it is to move.
		/// </summary>
		public PieceColor ActiveColor { get; init; } = PieceColor.White;

		/// <summary>
		///     If a pawn has just made a two-square move, this is the position "behind" the pawn.
		///     This is needed for en passant capture validation.
		/// </summary>
		public Position? EnPassantTargetSquare { get; init; }
	}
}
