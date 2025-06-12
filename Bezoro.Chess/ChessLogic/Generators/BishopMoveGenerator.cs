using System.Collections.Generic;

namespace Bezoro.Chess.ChessLogic
{
	internal static class BishopMoveGenerator
	{
		private static readonly (int dRow, int dCol)[] Directions = { (-1, -1), (-1, 1), (1, -1), (1, 1) };

		public static IEnumerable<Move> GenerateMoves(Position from, GameState gameState) =>
			SlidingPieceMoveGenerator.GenerateMoves(from, gameState, Directions);
	}
}
