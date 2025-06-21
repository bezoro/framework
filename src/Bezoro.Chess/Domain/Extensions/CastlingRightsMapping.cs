namespace Bezoro.Chess.API.Shared.Enums
{
	internal static class CastlingRightsMapping
	{
		public static CastlingRights ToAPI(this Domain.Shared.Enums.CastlingRights p) => p switch
		{
			Domain.Shared.Enums.CastlingRights.WhiteKingside  => CastlingRights.WhiteKingside,
			Domain.Shared.Enums.CastlingRights.WhiteQueenside => CastlingRights.WhiteQueenside,
			Domain.Shared.Enums.CastlingRights.BlackKingside  => CastlingRights.BlackKingside,
			Domain.Shared.Enums.CastlingRights.BlackQueenside => CastlingRights.BlackQueenside,
			Domain.Shared.Enums.CastlingRights.White          => CastlingRights.White,
			Domain.Shared.Enums.CastlingRights.Black          => CastlingRights.Black,
			Domain.Shared.Enums.CastlingRights.All            => CastlingRights.All,
			Domain.Shared.Enums.CastlingRights.None           => CastlingRights.None
		};

		public static Domain.Shared.Enums.CastlingRights ToDomain(this CastlingRights p) => p switch
		{
			CastlingRights.WhiteKingside  => Domain.Shared.Enums.CastlingRights.WhiteKingside,
			CastlingRights.WhiteQueenside => Domain.Shared.Enums.CastlingRights.WhiteQueenside,
			CastlingRights.BlackKingside  => Domain.Shared.Enums.CastlingRights.BlackKingside,
			CastlingRights.BlackQueenside => Domain.Shared.Enums.CastlingRights.BlackQueenside,
			CastlingRights.White          => Domain.Shared.Enums.CastlingRights.White,
			CastlingRights.Black          => Domain.Shared.Enums.CastlingRights.Black,
			CastlingRights.All            => Domain.Shared.Enums.CastlingRights.All,
			CastlingRights.None           => Domain.Shared.Enums.CastlingRights.None
		};
	}
}
