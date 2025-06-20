using Bezoro.Chess.Domain.Types.Structs;

namespace Bezoro.Chess.Domain.Helpers
{
	internal static class BoardHelper
	{
		public static bool IsInsideBoard(Position pos) =>
			pos.Row is >= 0 and < 8 && pos.Col is >= 0 and < 8;
	}
}
