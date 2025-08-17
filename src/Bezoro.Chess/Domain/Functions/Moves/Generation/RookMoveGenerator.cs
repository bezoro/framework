using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Bezoro.Chess.Domain.Shared.Consts;
using Bezoro.Chess.Domain.Types.Records;
using Bezoro.Chess.Domain.Types.Structs;

namespace Bezoro.Chess.Domain.Functions.Moves.Generation
{
	internal static class RookMoveGenerator
	{
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static IEnumerable<Move> GenerateMoves(Position from, GameState gameState) =>
			SlidingPieceMoveGenerator.GenerateMoves(from, gameState, AttackVectors.RookAttackVectors);
	}
}
