using Bezoro.Chess.Domain.Shared.Consts;
using Bezoro.Chess.Domain.Types.Structs;

namespace Bezoro.Chess.Domain.Helpers
{
	internal static class BitboardHelper
	{
		/// <summary>Return the classical initial position.</summary>
		public static BoardBitboards StartPosition() =>
			BitboardConsts.StartPosition;

		public static BoardBitboards EmptyBoard() =>
			BitboardConsts.EmptyBoard;
	}
}
