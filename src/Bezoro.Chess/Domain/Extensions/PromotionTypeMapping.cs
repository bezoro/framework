namespace Bezoro.Chess.API.Types
{
	internal static class PromotionTypeMapping
	{
		public static PromotionType ToAPI(this Domain.Types.Structs.PromotionType p) => p switch
		{
			Domain.Types.Structs.PromotionType.Bishop => PromotionType.Bishop,
			Domain.Types.Structs.PromotionType.Knight => PromotionType.Knight,
			Domain.Types.Structs.PromotionType.Queen  => PromotionType.Queen,
			Domain.Types.Structs.PromotionType.Rook   => PromotionType.Rook,
			_                                         => PromotionType.None
		};

		public static Domain.Types.Structs.PromotionType ToDomain(this PromotionType p) => p switch
		{
			PromotionType.Bishop => Domain.Types.Structs.PromotionType.Bishop,
			PromotionType.Knight => Domain.Types.Structs.PromotionType.Knight,
			PromotionType.Queen  => Domain.Types.Structs.PromotionType.Queen,
			PromotionType.Rook   => Domain.Types.Structs.PromotionType.Rook,
			_                    => Domain.Types.Structs.PromotionType.None
		};
	}
}
