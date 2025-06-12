using System;

namespace Bezoro.Chess.Common.Enums
{
	/// <summary>Board files (columns) A–H.</summary>
	public enum BoardFile : sbyte
	{
		None = -1,
		A, B, C, D, E, F, G, H
	}

	/// <summary>Board ranks (rows) 1–8.</summary>
	public enum BoardRank : sbyte
	{
		None = -1,
		Rank1, Rank2, Rank3, Rank4, Rank5, Rank6, Rank7, Rank8
	}

	/// <summary>Compass directions (combinable).</summary>
	[Flags]
	public enum CardinalDirection : byte
	{
		None      = 0,
		North     = 1 << 0,
		South     = 1 << 1,
		East      = 1 << 2,
		West      = 1 << 3,
		NorthEast = North | East,
		NorthWest = North | West,
		SouthEast = South | East,
		SouthWest = South | West
	}

	[Flags]
	public enum CastleSide : byte
	{
		None  = 0,
		King  = 1,
		Queen = 2
	}

	[Flags]
	public enum CastlingRights : byte
	{
		None           = 0,
		WhiteKingSide  = 1 << 0,
		WhiteQueenSide = 1 << 1,
		BlackKingSide  = 1 << 2,
		BlackQueenSide = 1 << 3,
		All            = WhiteKingSide | WhiteQueenSide | BlackKingSide | BlackQueenSide
	}

	/// <summary>Standard chess piece ordering 0–5.</summary>
	public enum ChessPieceType : sbyte
	{
		None = -1,
		Pawn = 0, Knight, Bishop, Rook, Queen, King
	}

	public enum GameEndReason : sbyte
	{
		None = -1,
		Checkmate,
		Resignation,
		Timeout,
		Stalemate,
		Agreement,
		InsufficientMaterial,
		FiftyMoveRule,
		Repetition,
		Abandoned
	}

	public enum GameOutcome : sbyte
	{
		None = -1,
		WhiteWin,
		BlackWin,
		Draw,
		Abandoned
	}

	public enum GameStatus : sbyte
	{
		None = -1,
		NotStarted,
		InProgress,
		Finished
	}

	public enum MoveKind : sbyte
	{
		None = -1,
		Normal,
		Capture,
		EnPassant,
		PromotionQuiet,
		PromotionCapture,
		Castle
	}

	[Flags]
	public enum PGNMoveAnnotation : byte
	{
		None        = 0,
		Check       = 1 << 0, // +
		Checkmate   = 1 << 1, // #
		Good        = 1 << 2, // !
		Brilliant   = 1 << 3, // !!
		Mistake     = 1 << 4, // ?
		Blunder     = 1 << 5, // ??
		Interesting = 1 << 6, // !?
		Dubious     = 1 << 7  // ?!
	}

	/// <summary>Player color (black or white)</summary>
	public enum PlayerColor : sbyte
	{
		None = -1,
		White,
		Black
	}

	/// <summary>Promotion targets (values match <see cref="ChessPieceType" />).</summary>
	public enum PromotionPieceType : sbyte
	{
		None   = -1,
		Knight = ChessPieceType.Knight,
		Bishop = ChessPieceType.Bishop,
		Rook   = ChessPieceType.Rook,
		Queen  = ChessPieceType.Queen
	}

	/// <summary>Square index 0-63 (A1→H8).</summary>
	public enum Square : sbyte
	{
		None = -1,
		A1   = 0, B1, C1, D1, E1, F1, G1, H1,
		A2, B2, C2, D2, E2, F2, G2, H2,
		A3, B3, C3, D3, E3, F3, G3, H3,
		A4, B4, C4, D4, E4, F4, G4, H4,
		A5, B5, C5, D5, E5, F5, G5, H5,
		A6, B6, C6, D6, E6, F6, G6, H6,
		A7, B7, C7, D7, E7, F7, G7, H7,
		A8, B8, C8, D8, E8, F8, G8, H8
	}

	/// <summary>Square color (light or dark).</summary>
	public enum SquareColor : byte
	{
		None = 0,
		Light,
		Dark
	}
}

/// <summary>Which textual notation to emit or parse.</summary>
public enum NotationStyle : byte
{
	Coordinate, // e2e4
	SAN,        // e4, Nf3, O-O
	LAN,        // Pe2-e4
	Uci         // e2e4 (but always lowercase, no symbols)
}

/// <summary>Special SAN prefixes or separators.</summary>
[Flags]
public enum SanModifiers : byte
{
	None      = 0,
	Capture   = 1 << 0, // “x”
	Equals    = 1 << 1, // “=”
	Check     = 1 << 2, // “+”
	Checkmate = 1 << 3  // “#”
	// More can be added if you really need “ep” or “-”.
}
