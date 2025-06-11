using Bezoro.Chess.Board;
using Bezoro.Chess.Common.Data;
using Bezoro.Chess.Common.Enums;

namespace Bezoro.Chess.Common.Helpers
{
	public static class CastlingUtils
	{
		public static readonly CastlingData BlackKingSideData = new(
			PlayerColor.Black, CastleSide.King,
			BoardPosition.FromAlgebraic("e8"),
			BoardPosition.FromAlgebraic("g8"),
			BoardPosition.FromAlgebraic("h8"),
			BoardPosition.FromAlgebraic("f8"));
		public static readonly CastlingData BlackQueenSideData = new(
			PlayerColor.Black, CastleSide.Queen,
			BoardPosition.FromAlgebraic("e8"),
			BoardPosition.FromAlgebraic("c8"),
			BoardPosition.FromAlgebraic("a8"),
			BoardPosition.FromAlgebraic("d8"));
		public static readonly CastlingData WhiteKingSideData = new(
			PlayerColor.White, CastleSide.King,
			BoardPosition.FromAlgebraic("e1"),
			BoardPosition.FromAlgebraic("g1"),
			BoardPosition.FromAlgebraic("h1"),
			BoardPosition.FromAlgebraic("f1"));
		public static readonly CastlingData WhiteQueenSideData = new(
			PlayerColor.White, CastleSide.Queen,
			BoardPosition.FromAlgebraic("e1"),
			BoardPosition.FromAlgebraic("c1"),
			BoardPosition.FromAlgebraic("a1"),
			BoardPosition.FromAlgebraic("d1"));
	}
}
