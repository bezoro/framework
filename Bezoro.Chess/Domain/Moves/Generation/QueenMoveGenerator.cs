using System.Collections.Generic;
using System.Linq;
using Bezoro.Chess.Domain.Board;

namespace Bezoro.Chess.Domain.Moves.Generation
{
	internal static class QueenMoveGenerator
	{
		public static IEnumerable<Move> GenerateMoves(Position from, GameState gameState) =>
			RookMoveGenerator.GenerateMoves(from, gameState)
							 .Concat(BishopMoveGenerator.GenerateMoves(from, gameState));
	}
}
