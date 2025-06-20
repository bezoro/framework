using Bezoro.Chess.API.Shared.Enums;

namespace Bezoro.Chess.Domain.Extensions
{
	internal static class PromotionTypeMapping
	{
		public static PromotionType ToAPI(this Types.Structs.PromotionType p) => p switch
		{
			Types.Structs.PromotionType.Bishop => PromotionType.Bishop,
			Types.Structs.PromotionType.Knight => PromotionType.Knight,
			Types.Structs.PromotionType.Queen  => PromotionType.Queen,
			Types.Structs.PromotionType.Rook   => PromotionType.Rook,
			_                                  => PromotionType.None
		};

		public static Types.Structs.PromotionType ToDomain(this PromotionType p) => p switch
		{
			PromotionType.Bishop => Types.Structs.PromotionType.Bishop,
			PromotionType.Knight => Types.Structs.PromotionType.Knight,
			PromotionType.Queen  => Types.Structs.PromotionType.Queen,
			PromotionType.Rook   => Types.Structs.PromotionType.Rook,
			_                    => Types.Structs.PromotionType.None
		};
	}
}
