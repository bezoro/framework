using System.Collections.Generic;
using Bezoro.Chess.Board;
using Bezoro.Chess.Common.Data;
using Bezoro.Chess.Common.Enums;

namespace Bezoro.Chess.Common.Helpers
{
	public static class CastlingUtils
	{
		public static readonly CastlingData BLACK_KING_SIDE_DATA = new(
			PlayerColor.Black, CastleSide.King,
			BoardPosition.FromAlgebraic("e8"),
			BoardPosition.FromAlgebraic("g8"),
			BoardPosition.FromAlgebraic("h8"),
			BoardPosition.FromAlgebraic("f8"));
		public static readonly CastlingData BLACK_QUEEN_SIDE_DATA = new(
			PlayerColor.Black, CastleSide.Queen,
			BoardPosition.FromAlgebraic("e8"),
			BoardPosition.FromAlgebraic("c8"),
			BoardPosition.FromAlgebraic("a8"),
			BoardPosition.FromAlgebraic("d8"));
		public static readonly CastlingData WHITE_KING_SIDE_DATA = new(
			PlayerColor.White, CastleSide.King,
			BoardPosition.FromAlgebraic("e1"),
			BoardPosition.FromAlgebraic("g1"),
			BoardPosition.FromAlgebraic("h1"),
			BoardPosition.FromAlgebraic("f1"));
		public static readonly CastlingData WHITE_QUEEN_SIDE_DATA = new(
			PlayerColor.White, CastleSide.Queen,
			BoardPosition.FromAlgebraic("e1"),
			BoardPosition.FromAlgebraic("c1"),
			BoardPosition.FromAlgebraic("a1"),
			BoardPosition.FromAlgebraic("d1"));

		public static readonly Dictionary<(PlayerColor, CastleSide), CastlingData> CASTLING_DATA_MAP = new()
		{
			{
				(BLACK_KING_SIDE_DATA.Color, BLACK_KING_SIDE_DATA.Side), BLACK_KING_SIDE_DATA
			},
			{
				(BLACK_QUEEN_SIDE_DATA.Color, BLACK_QUEEN_SIDE_DATA.Side), BLACK_QUEEN_SIDE_DATA
			},
			{
				(WHITE_KING_SIDE_DATA.Color, WHITE_KING_SIDE_DATA.Side), WHITE_KING_SIDE_DATA
			},
			{
				(WHITE_QUEEN_SIDE_DATA.Color, WHITE_QUEEN_SIDE_DATA.Side), WHITE_QUEEN_SIDE_DATA
			}
		};
	}
}
