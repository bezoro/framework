using System;

namespace Bezoro.Chess.Common.Enums
{
	public enum CardinalDirection
	{
		None = -1,
		North, South, East, West,
		NorthEast, NorthWest, SouthEast, SouthWest
	}

	/// <summary>
	///     Identifies which side (king- or queen-side) a player wishes to castle on.
	///     A value of <see cref="None" /> should be supplied only when the caller
	///     does not yet know which side is intended.
	/// </summary>
	public enum CastleSide
	{
		None  = 0,
		King  = 1,
		Queen = 2
	}

	/// <summary>
	///     Represents the castling rights available to players.
	///     Can be combined using bitwise operations as it's a flags enum.
	/// </summary>
	[Flags]
	public enum CastlingRights
	{
		/// <summary>
		///     No castling rights are available. Useful for representing a state where
		///     no player can castle, or as an initial value before specific rights are assigned.
		/// </summary>
		None = 0,
		/// <summary>
		///     White has the right to castle king-side.
		/// </summary>
		WhiteKingSide = 1,
		/// <summary>
		///     White has the right to castle queen-side.
		/// </summary>
		WhiteQueenSide = 2,
		/// <summary>
		///     Black has the right to castle king-side.
		/// </summary>
		BlackKingSide = 4,
		/// <summary>
		///     Black has the right to castle queen-side.
		/// </summary>
		BlackQueenSide = 8,
		/// <summary>
		///     All castling rights are available. Useful for initializing the game state
		///     or for checks where all rights are expected.
		/// </summary>
		All = WhiteKingSide | WhiteQueenSide | BlackKingSide | BlackQueenSide
	}

	/// <summary>
	///     Defines the types of chess pieces.
	/// </summary>
	public enum ChessPieceType
	{
		/// <summary>
		///     Represents an empty square or an uninitialized/invalid piece type.
		/// </summary>
		None = -1,
		Pawn, Knight, Bishop, Rook, Queen, King
	}

	/// <summary>
	///     Represents all 64 squares on a chessboard, with A1 = 0.
	///     The visual layout in the definition lists ranks from 8 down to 1.
	/// </summary>
	public enum FileRank
	{
		/// <summary>
		///     Represents an invalid or off-board square. Useful for error states
		///     or when a piece is not on any valid square.
		/// </summary>
		None = -1,
		// Rank 1 (bottom of the board)
		A1 = 0, B1 = 1, C1 = 2, D1 = 3, E1 = 4, F1 = 5, G1 = 6, H1 = 7,
		// Rank 2
		A2 = 8, B2 = 9, C2 = 10, D2 = 11, E2 = 12, F2 = 13, G2 = 14, H2 = 15,
		// Rank 3
		A3 = 16, B3 = 17, C3 = 18, D3 = 19, E3 = 20, F3 = 21, G3 = 22, H3 = 23,
		// Rank 4
		A4 = 24, B4 = 25, C4 = 26, D4 = 27, E4 = 28, F4 = 29, G4 = 30, H4 = 31,
		// Rank 5
		A5 = 32, B5 = 33, C5 = 34, D5 = 35, E5 = 36, F5 = 37, G5 = 38, H5 = 39,
		// Rank 6
		A6 = 40, B6 = 41, C6 = 42, D6 = 43, E6 = 44, F6 = 45, G6 = 46, H6 = 47,
		// Rank 7
		A7 = 48, B7 = 49, C7 = 50, D7 = 51, E7 = 52, F7 = 53, G7 = 54, H7 = 55,
		// Rank 8 (top of the board)
		A8 = 56, B8 = 57, C8 = 58, D8 = 59, E8 = 60, F8 = 61, G8 = 62, H8 = 63
	}

	/// <summary>
	///     Defines the high-level progression status of a chess game.
	/// </summary>
	public enum GameStatus
	{
		/// <summary> The game's status is uninitialized or unknown. </summary>
		None = -1,
		/// <summary> The game has been set up but has not yet started. </summary>
		NotStarted,
		/// <summary> The game is currently being played. </summary>
		InProgress,
		/// <summary> The game has concluded. The specific outcome is detailed by a GameOutcome object. </summary>
		Finished
	}

	/// <summary>
	///     Enumerates the high-level categories a move can belong to.
	/// </summary>
	public enum MoveKind
	{
		Normal,
		Capture,
		EnPassant,
		PromotionQuiet,
		PromotionCapture,
		Castle
	}

	/// <summary>
	///     Defines the color of a chess player or piece.
	/// </summary>
	public enum PlayerColor
	{
		/// <summary>
		///     Represents no specific color, an empty square's "owner" (if applicable),
		///     an observer, or an uninitialized state.
		/// </summary>
		None = -1,
		White, Black
	}

	/// <summary>
	///     Defines the types of pieces a pawn can be promoted to.
	/// </summary>
	public enum PromotionPieceType
	{
		/// <summary>
		///     No promotion is currently applicable, or this value is used as a default/uninitialized state.
		/// </summary>
		None = -1,
		Queen, Rook, Bishop, Knight
	}
}
