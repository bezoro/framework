using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Bezoro.Chess.Domain.Types.Records;
using Bezoro.Chess.Domain.Types.Structs;

namespace Bezoro.Chess.Domain.Functions.Moves.Generation
{
	internal static class QueenMoveGenerator
	{
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static IEnumerable<Move> GenerateMoves(Position from, GameState gameState) =>
			RookMoveGenerator.GenerateMoves(from, gameState)
							 .Concat(BishopMoveGenerator.GenerateMoves(from, gameState));
	}
}
