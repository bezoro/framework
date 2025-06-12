using System.Collections.Generic;

namespace Bezoro.Chess.ChessLogic
{
	internal static class RookMoveGenerator
	{
		private static readonly (int dRow, int dCol)[] Directions = { (0, 1), (0, -1), (1, 0), (-1, 0) };

		public static IEnumerable<Move> GenerateMoves(Position from, GameState gameState) =>
			SlidingPieceMoveGenerator.GenerateMoves(from, gameState, Directions);
	}
}
