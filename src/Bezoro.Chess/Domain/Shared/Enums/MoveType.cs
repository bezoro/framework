namespace Bezoro.Chess.Domain.Shared.Enums
{
	/// <summary>
	///     All legal move kinds supported by the engine.
	/// </summary>
	internal enum MoveType : byte
	{
		None,
		Normal, Capture,
		Castling, EnPassant,
		Promotion, PromotionCapture
	}
}
