namespace Bezoro.Chess.API.Types
{
	/// <summary>
	///     Enumerates the types of possible moves in chess.
	/// </summary>
	public enum MoveType : byte
	{
		None,
		Normal, Capture,
		CastleKingside, CastleQueenside,
		EnPassant, QuietPromotion, CapturePromotion
	}
}
