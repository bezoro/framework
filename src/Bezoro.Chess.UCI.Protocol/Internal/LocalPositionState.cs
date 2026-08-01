namespace Bezoro.Chess.UCI.Protocol.Internal;

internal readonly struct LocalPositionState
{
	internal LocalPositionState(
		char[] board,
		char   activeColor,
		string castlingRights,
		string enPassantTarget,
		int    halfmoveClock,
		int    fullmoveNumber)
	{
		Board           = board;
		ActiveColor     = activeColor;
		CastlingRights  = castlingRights;
		EnPassantTarget = enPassantTarget;
		HalfmoveClock   = halfmoveClock;
		FullmoveNumber  = fullmoveNumber;
	}

	internal char[] Board { get; }

	internal char ActiveColor { get; }

	internal string CastlingRights { get; }

	internal string EnPassantTarget { get; }

	internal int HalfmoveClock { get; }

	internal int FullmoveNumber { get; }
}
