namespace Bezoro.Chess.UCI.API.Common.Enums;

/// <summary>
///     Chess piece type.
/// </summary>
public enum PieceType : byte
{
	/// <summary>No piece (empty square).</summary>
	Empty,

	/// <summary>A pawn.</summary>
	Pawn,

	/// <summary>A knight.</summary>
	Knight,

	/// <summary>A bishop.</summary>
	Bishop,

	/// <summary>A rook.</summary>
	Rook,

	/// <summary>A queen.</summary>
	Queen,

	/// <summary>A king.</summary>
	King
}
