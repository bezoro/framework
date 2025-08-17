using Bezoro.Chess.Domain.Shared.Consts;
using Bezoro.Chess.Domain.Types.Structs;

namespace Bezoro.Chess.Domain.Helpers
{
	internal static class BitboardHelper
	{
		public static BoardBitboards EmptyBoard() =>
			BitboardConsts.EmptyBoard;

		/// <summary>Return the classical initial position.</summary>
		public static BoardBitboards StartPosition() =>
			BitboardConsts.StartPosition;
	}
}
