using Bezoro.Chess.Board;
using Bezoro.Chess.Common.Enums;

namespace Bezoro.Chess.Common.Data
{
	public readonly struct CastlingData
	{
		public CastlingData(
			PlayerColor color,
			CastleSide side,
			BoardPosition kingStartPosition,
			BoardPosition kingEndPosition,
			BoardPosition rookStartPosition,
			BoardPosition rookEndPosition)
		{
			Color             = color;
			Side              = side;
			KingStartPosition = kingStartPosition;
			KingEndPosition   = kingEndPosition;
			RookStartPosition = rookStartPosition;
			RookEndPosition   = rookEndPosition;
		}

		public BoardPosition KingEndPosition   { get; }
		public BoardPosition KingStartPosition { get; }
		public BoardPosition RookEndPosition   { get; }
		public BoardPosition RookStartPosition { get; }
		public CastleSide    Side              { get; }
		public PlayerColor   Color             { get; }
	}
}
