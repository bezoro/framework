using Bezoro.Core.Chess.Abstractions.Interfaces;

namespace Bezoro.Core.Chess.Common.Helpers
{
	public static class BoardUtils
	{
		private static bool IsPositionWithinBoardBounds(IChessBoardModel board, int file, int rank) =>
			file < 0 || file >= board.Width || rank < 0 || rank >= board.Height;
	}
}
