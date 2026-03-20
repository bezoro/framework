namespace Bezoro.Chess.UCI.API.Common.Enums;

/// <summary>
///     Chess piece type.
/// </summary>
public enum PieceType : byte
{
	/// <summary>No piece (empty square).</summary>
	Empty,
	Pawn, Knight, Bishop, Rook, Queen, King
}
