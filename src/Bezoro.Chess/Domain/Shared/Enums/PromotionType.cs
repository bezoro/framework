namespace Bezoro.Chess.Domain.Shared.Enums
{
	internal enum PromotionType : byte
	{
		None,
		Queen = PieceType.Queen, Rook = PieceType.Rook, Bishop = PieceType.Bishop, Knight = PieceType.Knight
	}
}
