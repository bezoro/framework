using System.Collections.Generic;
using Bezoro.Chess.Common.Data;
using Bezoro.Chess.Common.Enums;

namespace Bezoro.Chess.Common.Helpers
{
	public static class CastlingUtils
	{
		public static readonly CastlingData BLACK_KING_SIDE_DATA = new(
			PlayerColor.Black, CastleSide.King,
			AlgebraicNotationUtils.FromAlgebraic("e8"),
			AlgebraicNotationUtils.FromAlgebraic("g8"),
			AlgebraicNotationUtils.FromAlgebraic("h8"),
			AlgebraicNotationUtils.FromAlgebraic("f8"));
		public static readonly CastlingData BLACK_QUEEN_SIDE_DATA = new(
			PlayerColor.Black, CastleSide.Queen,
			AlgebraicNotationUtils.FromAlgebraic("e8"),
			AlgebraicNotationUtils.FromAlgebraic("c8"),
			AlgebraicNotationUtils.FromAlgebraic("a8"),
			AlgebraicNotationUtils.FromAlgebraic("d8"));
		public static readonly CastlingData WHITE_KING_SIDE_DATA = new(
			PlayerColor.White, CastleSide.King,
			AlgebraicNotationUtils.FromAlgebraic("e1"),
			AlgebraicNotationUtils.FromAlgebraic("g1"),
			AlgebraicNotationUtils.FromAlgebraic("h1"),
			AlgebraicNotationUtils.FromAlgebraic("f1"));
		public static readonly CastlingData WHITE_QUEEN_SIDE_DATA = new(
			PlayerColor.White, CastleSide.Queen,
			AlgebraicNotationUtils.FromAlgebraic("e1"),
			AlgebraicNotationUtils.FromAlgebraic("c1"),
			AlgebraicNotationUtils.FromAlgebraic("a1"),
			AlgebraicNotationUtils.FromAlgebraic("d1"));

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
