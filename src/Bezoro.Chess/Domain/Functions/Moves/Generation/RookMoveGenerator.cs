using System.Collections.Generic;
using Bezoro.Chess.Domain.Types.Records;
using Bezoro.Chess.Domain.Types.Structs;

namespace Bezoro.Chess.Domain.Functions.Moves.Generation
{
	internal static class RookMoveGenerator
	{
		private static readonly (int dRow, int dCol)[] Directions = { (0, 1), (0, -1), (1, 0), (-1, 0) };

		public static IEnumerable<Move> GenerateMoves(Position from, GameState gameState) =>
			SlidingPieceMoveGenerator.GenerateMoves(from, gameState, Directions);
	}
}
