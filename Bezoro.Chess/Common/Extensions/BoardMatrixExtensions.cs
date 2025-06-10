using System.Runtime.CompilerServices;

namespace Bezoro.Chess.Common.Extensions
{
	/// <summary>
	///     Extension helpers for working with the internal 2-D piece array that
	///     represents a board.
	/// </summary>
	internal static class BoardMatrixExtensions
	{
		/// <summary>
		///     Checks whether the supplied coordinates are on the board.
		/// </summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool IsInside<T>(this T[,] m, int file, int rank) =>
			(uint)file < m.Width() && (uint)rank < m.Height();

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static int Height<T>(this T[,] m) => m.GetLength(1);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static int Width<T>(this T[,] m) => m.GetLength(0);
	}
}
