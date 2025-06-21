using Bezoro.Chess.Domain.Types.Structs;

namespace Bezoro.Chess.Domain.Helpers
{
	/// <summary>
	///     Produces ready-made <see cref="BoardBitboards" /> instances for common scenarios.
	/// </summary>
	internal static class BoardFactory
	{
		/// <summary>
		///     Classical starting position – delegates to the cached value inside
		///     <see cref="BitboardHelper" /> / <see cref="BitboardConsts" />.
		/// </summary>
		public static BoardBitboards CreateInitialBitboards() =>
			BitboardHelper.StartPosition();

		public static BoardBitboards CreateEmptyBitboards() =>
			BitboardHelper.EmptyBoard();
	}
}
