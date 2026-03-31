namespace Bezoro.Chess.UCI.Protocol.API.Types;

/// <summary>
///     Flags describing structural and tactical move characteristics.
/// </summary>
[Flags]
public enum MoveClassificationFlags
{
	/// <summary>No flags are set.</summary>
	None = 0,

	/// <summary>The move is a normal non-special move.</summary>
	Normal = 1 << 0,

	/// <summary>The move captures a piece, including en passant.</summary>
	Capture = 1 << 1,

	/// <summary>The move is an en passant capture.</summary>
	EnPassant = 1 << 2,

	/// <summary>The move promotes a pawn.</summary>
	Promotion = 1 << 3,

	/// <summary>The move castles kingside.</summary>
	KingsideCastling = 1 << 4,

	/// <summary>The move castles queenside.</summary>
	QueensideCastling = 1 << 5,

	/// <summary>The move is a two-square pawn advance.</summary>
	DoublePawnPush = 1 << 6,

	/// <summary>The move gives check.</summary>
	Check = 1 << 7,

	/// <summary>The move gives checkmate.</summary>
	Mate = 1 << 8,

	/// <summary>The move stalemates the opponent.</summary>
	Stalemate = 1 << 9
}
