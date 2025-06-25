using Bezoro.Chess.API.Shared.Enums;

namespace Bezoro.Chess.Domain.Extensions
{
	internal static class MoveTypeMapping
	{
		public static MoveType ToAPI(this Shared.Enums.MoveType p) => p switch
		{
			Shared.Enums.MoveType.Normal           => MoveType.Normal,
			Shared.Enums.MoveType.Capture          => MoveType.Capture,
			Shared.Enums.MoveType.Castling         => MoveType.Castling,
			Shared.Enums.MoveType.EnPassant        => MoveType.EnPassant,
			Shared.Enums.MoveType.Promotion        => MoveType.QuietPromotion,
			Shared.Enums.MoveType.PromotionCapture => MoveType.CapturePromotion,
			_                                      => MoveType.None
		};

		public static Shared.Enums.MoveType ToDomain(this MoveType p) => p switch
		{
			MoveType.Normal           => Shared.Enums.MoveType.Normal,
			MoveType.Capture          => Shared.Enums.MoveType.Capture,
			MoveType.Castling         => Shared.Enums.MoveType.Castling,
			MoveType.EnPassant        => Shared.Enums.MoveType.EnPassant,
			MoveType.QuietPromotion   => Shared.Enums.MoveType.Promotion,
			MoveType.CapturePromotion => Shared.Enums.MoveType.PromotionCapture,
			_                         => Shared.Enums.MoveType.None
		};
	}
}
