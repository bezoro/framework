using Bezoro.Chess.API.Shared.Enums;

namespace Bezoro.Chess.Domain.Extensions
{
	internal static class CastlingRightsMapping
	{
		public static CastlingRights ToAPI(this Shared.Enums.CastlingRights p) => p switch
		{
			Shared.Enums.CastlingRights.WhiteKingside  => CastlingRights.WhiteKingside,
			Shared.Enums.CastlingRights.WhiteQueenside => CastlingRights.WhiteQueenside,
			Shared.Enums.CastlingRights.BlackKingside  => CastlingRights.BlackKingside,
			Shared.Enums.CastlingRights.BlackQueenside => CastlingRights.BlackQueenside,
			Shared.Enums.CastlingRights.White          => CastlingRights.White,
			Shared.Enums.CastlingRights.Black          => CastlingRights.Black,
			Shared.Enums.CastlingRights.All            => CastlingRights.All,
			Shared.Enums.CastlingRights.None           => CastlingRights.None
		};

		public static Shared.Enums.CastlingRights ToDomain(this CastlingRights p) => p switch
		{
			CastlingRights.WhiteKingside  => Shared.Enums.CastlingRights.WhiteKingside,
			CastlingRights.WhiteQueenside => Shared.Enums.CastlingRights.WhiteQueenside,
			CastlingRights.BlackKingside  => Shared.Enums.CastlingRights.BlackKingside,
			CastlingRights.BlackQueenside => Shared.Enums.CastlingRights.BlackQueenside,
			CastlingRights.White          => Shared.Enums.CastlingRights.White,
			CastlingRights.Black          => Shared.Enums.CastlingRights.Black,
			CastlingRights.All            => Shared.Enums.CastlingRights.All,
			CastlingRights.None           => Shared.Enums.CastlingRights.None
		};
	}
}
