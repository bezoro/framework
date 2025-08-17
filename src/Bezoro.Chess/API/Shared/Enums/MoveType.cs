namespace Bezoro.Chess.API.Shared.Enums
{
	/// <summary>
	///     Enumerates the types of possible moves in chess.
	/// </summary>
	public enum MoveType : byte
	{
		None,
		Normal, Capture,
		Castling, EnPassant,
		QuietPromotion, CapturePromotion
	}
}
