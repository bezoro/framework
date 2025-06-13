using Bezoro.Chess.Domain.Board;

namespace Bezoro.Chess.Domain.Moves.Execution
{
	internal static class PromotionExtensions
	{
		public static PieceType ToPieceType(this PromotionType promotion) =>
			promotion switch
			{
				PromotionType.Queen  => PieceType.Queen,
				PromotionType.Rook   => PieceType.Rook,
				PromotionType.Bishop => PieceType.Bishop,
				PromotionType.Knight => PieceType.Knight,
				_                    => PieceType.Queen // fallback, shouldn't happen
			};
	}
}
