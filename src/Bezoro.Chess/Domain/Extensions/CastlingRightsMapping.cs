using Bezoro.Chess.API.Shared.Enums;

namespace Bezoro.Chess.Domain.Extensions
{
	internal static class CastlingRightsMapping
	{
		public static CastlingRights ToAPI(this Shared.Enums.CastlingRights p) =>
			(CastlingRights)p;

		public static Shared.Enums.CastlingRights ToDomain(this CastlingRights p) =>
			(Shared.Enums.CastlingRights)p;
	}
}
