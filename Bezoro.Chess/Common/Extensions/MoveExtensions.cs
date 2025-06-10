using System.Runtime.CompilerServices;
using Bezoro.Chess.Abstractions.Interfaces;
using Bezoro.Chess.Moves.Models;

namespace Bezoro.Chess.Common.Extensions
{
	public static class MoveExtensions
	{
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static IChessPieceModel? GetMovingPiece(this Move move, IChessBoardModel board) =>
			board.GetPieceAt(move.From);
	}
}
