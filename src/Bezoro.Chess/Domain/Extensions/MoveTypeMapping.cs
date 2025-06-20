namespace Bezoro.Chess.API.Types
{
	internal static class MoveTypeMapping
	{
		public static MoveType ToAPI(this Domain.Types.Structs.MoveType p) => p switch
		{
			Domain.Types.Structs.MoveType.Normal               => MoveType.Normal,
			Domain.Types.Structs.MoveType.Capture              => MoveType.Capture,
			Domain.Types.Structs.MoveType.CastleKingside       => MoveType.CastleKingside,
			Domain.Types.Structs.MoveType.CastleQueenside      => MoveType.CastleQueenside,
			Domain.Types.Structs.MoveType.EnPassant            => MoveType.EnPassant,
			Domain.Types.Structs.MoveType.PawnPromotion        => MoveType.QuietPromotion,
			Domain.Types.Structs.MoveType.PawnPromotionCapture => MoveType.CapturePromotion,
			_                                                  => MoveType.None
		};

		public static Domain.Types.Structs.MoveType ToDomain(this MoveType p) => p switch
		{
			MoveType.Normal           => Domain.Types.Structs.MoveType.Normal,
			MoveType.Capture          => Domain.Types.Structs.MoveType.Capture,
			MoveType.CastleKingside   => Domain.Types.Structs.MoveType.CastleKingside,
			MoveType.CastleQueenside  => Domain.Types.Structs.MoveType.CastleQueenside,
			MoveType.EnPassant        => Domain.Types.Structs.MoveType.EnPassant,
			MoveType.QuietPromotion   => Domain.Types.Structs.MoveType.PawnPromotion,
			MoveType.CapturePromotion => Domain.Types.Structs.MoveType.PawnPromotionCapture,
			_                         => Domain.Types.Structs.MoveType.None
		};
	}
}
