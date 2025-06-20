using Bezoro.Chess.API.Shared.Enums;

namespace Bezoro.Chess.Domain.Extensions
{
	internal static class PromotionTypeMapping
	{
		public static PromotionType ToAPI(this Shared.Enums.PromotionType p) => p switch
		{
			Shared.Enums.PromotionType.Bishop => PromotionType.Bishop,
			Shared.Enums.PromotionType.Knight => PromotionType.Knight,
			Shared.Enums.PromotionType.Queen  => PromotionType.Queen,
			Shared.Enums.PromotionType.Rook   => PromotionType.Rook,
			_                                 => PromotionType.None
		};

		public static Shared.Enums.PromotionType ToDomain(this PromotionType p) => p switch
		{
			PromotionType.Bishop => Shared.Enums.PromotionType.Bishop,
			PromotionType.Knight => Shared.Enums.PromotionType.Knight,
			PromotionType.Queen  => Shared.Enums.PromotionType.Queen,
			PromotionType.Rook   => Shared.Enums.PromotionType.Rook,
			_                    => Shared.Enums.PromotionType.None
		};
	}
}
