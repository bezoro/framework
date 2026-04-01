using System;

namespace Bezoro.Chess.UCI.API.Common.Enums;

/// <summary>
///     Describes structural move characteristics for UI and gameplay event handling.
/// </summary>
[Flags]
public enum GameMoveKindFlags
{
	/// <summary>No structural flags are set.</summary>
	None = 0,

	/// <summary>A normal move with no special structural behavior.</summary>
	Normal = 1 << 0,

	/// <summary>A pawn advanced two squares.</summary>
	DoublePawnPush = 1 << 1,

	/// <summary>The move captured an opposing piece.</summary>
	Capture = 1 << 2,

	/// <summary>The move captured en passant.</summary>
	EnPassant = 1 << 3,

	/// <summary>The move castled on the kingside.</summary>
	KingsideCastling = 1 << 4,

	/// <summary>The move castled on the queenside.</summary>
	QueensideCastling = 1 << 5,

	/// <summary>The move promoted a pawn.</summary>
	Promotion = 1 << 6
}
