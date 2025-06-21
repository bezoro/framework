using System;

namespace Bezoro.Chess.API.Shared.Enums
{
	[Flags]
	public enum CastlingRights : byte
	{
		None           = 0,
		WhiteKingside  = 1 << 0,
		WhiteQueenside = 1 << 1,
		BlackKingside  = 1 << 2,
		BlackQueenside = 1 << 3,
		White          = WhiteKingside | WhiteQueenside,
		Black          = BlackKingside | BlackQueenside,
		All            = White         | Black
	}
}
