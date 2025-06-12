using System.Collections.Generic;
using System.Linq;

namespace Bezoro.Chess.ChessLogic.Generators
{
	internal static class QueenMoveGenerator
	{
		public static IEnumerable<Move> GenerateMoves(Position from, GameState gameState) =>
			RookMoveGenerator.GenerateMoves(from, gameState)
							 .Concat(BishopMoveGenerator.GenerateMoves(from, gameState));
	}
}
