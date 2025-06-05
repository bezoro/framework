using System.Collections.Generic;
using Bezoro.Core.Chess.Abstractions.Interfaces;

namespace Bezoro.Core.Chess.Common.Extensions
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
		public static bool IsInside<T>(this T[,] m, int file, int rank) =>
			(uint)file < m.Width() && (uint)rank < m.Height();

		/// <summary>
		///     Attempts to get a piece at (<paramref name="file" />,
		///     <paramref name="rank" />).
		/// </summary>
		public static bool TryGetPiece(
			this IChessPieceModel?[,] m,
			int file,
			int rank,
			out IChessPieceModel? piece)
		{
			piece = m.IsInside(file, rank) ? m[file, rank] : null;
			return piece is not null;
		}

		/// <summary>
		///     Enumerates coordinates along a ray that starts one step away from
		///     (<paramref name="file" />, <paramref name="rank" />) and proceeds by
		///     (<paramref name="df" />, <paramref name="dr" />) until it runs off the
		///     board.
		/// </summary>
		public static IEnumerable<(int f, int r)> Ray(
			this IChessPieceModel?[,] m,
			int file,
			int rank,
			int df,
			int dr)
		{
			for (int f = file + df, r = rank + dr ;
				 m.IsInside(f, r) ;
				 f += df, r += dr)
			{
				yield return (f, r);
			}
		}

		public static int Height<T>(this T[,] m) => m.GetLength(1);
		public static int Width<T>(this T[,] m) => m.GetLength(0);
	}
}
