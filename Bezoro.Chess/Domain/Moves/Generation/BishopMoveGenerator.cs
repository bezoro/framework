using System.Collections.Generic;
using Bezoro.Chess.Domain.Board;

namespace Bezoro.Chess.Domain.Moves.Generation
{
	internal static class BishopMoveGenerator
	{
		private static readonly (int dRow, int dCol)[] Directions = { (-1, -1), (-1, 1), (1, -1), (1, 1) };

		public static IEnumerable<Move> GenerateMoves(Position from, GameState gameState) =>
			SlidingPieceMoveGenerator.GenerateMoves(from, gameState, Directions);
	}
}
