using Bezoro.Chess.Abstractions.Interfaces;
using Bezoro.Chess.Moves.Models;

namespace Bezoro.Chess.Common.Extensions
{
	public static class MoveExtensions
	{
		public static IChessPieceModel? GetMovingPiece(this Move move, IChessBoardModel board) =>
			board.GetPieceAt(move.From);
	}
}
