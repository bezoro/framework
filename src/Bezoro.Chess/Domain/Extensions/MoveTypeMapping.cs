using Bezoro.Chess.API.Shared.Enums;

namespace Bezoro.Chess.Domain.Extensions
{
	internal static class MoveTypeMapping
	{
		public static MoveType ToAPI(this Types.Structs.MoveType p) => p switch
		{
			Types.Structs.MoveType.Normal           => MoveType.Normal,
			Types.Structs.MoveType.Capture          => MoveType.Capture,
			Types.Structs.MoveType.CastleKingside   => MoveType.CastleKingside,
			Types.Structs.MoveType.CastleQueenside  => MoveType.CastleQueenside,
			Types.Structs.MoveType.EnPassant        => MoveType.EnPassant,
			Types.Structs.MoveType.Promotion        => MoveType.QuietPromotion,
			Types.Structs.MoveType.PromotionCapture => MoveType.CapturePromotion,
			_                                       => MoveType.None
		};

		public static Types.Structs.MoveType ToDomain(this MoveType p) => p switch
		{
			MoveType.Normal           => Types.Structs.MoveType.Normal,
			MoveType.Capture          => Types.Structs.MoveType.Capture,
			MoveType.CastleKingside   => Types.Structs.MoveType.CastleKingside,
			MoveType.CastleQueenside  => Types.Structs.MoveType.CastleQueenside,
			MoveType.EnPassant        => Types.Structs.MoveType.EnPassant,
			MoveType.QuietPromotion   => Types.Structs.MoveType.Promotion,
			MoveType.CapturePromotion => Types.Structs.MoveType.PromotionCapture,
			_                         => Types.Structs.MoveType.None
		};
	}
}
