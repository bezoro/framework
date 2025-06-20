using System;
using Bezoro.Chess.API.Shared.Enums;

namespace Bezoro.Chess.Domain.Extensions
{
	/// <summary>
	///     Helpers for converting between <see cref="ChessSquareCoordinate" />,
	///     zero-based board indices and algebraic notation.
	/// </summary>
	internal static class ChessSquareCoordinateExtensions
	{
		/// <summary>
		///     Converts a zero-based index back to the corresponding square.
		///     –1 returns <see cref="ChessSquareCoordinate.None" />.
		/// </summary>
		/// <param name="index">–1 or a value in 0 … 63.</param>
		/// <exception cref="index">
		///     Thrown if <paramref name="index" /> is not –1 and not in 0 … 63.
		/// </exception>
		public static ChessSquareCoordinate ToSquare(this int index)
		{
			if (index == -1)
			{
				return ChessSquareCoordinate.None;
			}

			if (index is < 0 or > 63)
			{
				throw new ArgumentOutOfRangeException(nameof(index), index, "Valid range is –1 or 0…63.");
			}

			return (ChessSquareCoordinate)(index + 1);
		}

		/// <summary>
		///     Converts zero-based column and row indices to a chess square coordinate.
		/// </summary>
		/// <param name="colRow">A tuple containing column and row indices</param>
		/// <param name="colRow.col">Column index (0-7, where 0 is 'A' and 7 is 'H')</param>
		/// <param name="colRow.row">Row index (0-7, where 0 is '1' and 7 is '8')</param>
		/// <returns>The corresponding chess square coordinate</returns>
		/// <exception cref="ArgumentOutOfRangeException">
		///     Thrown if either index is not in range 0...7
		/// </exception>
		public static ChessSquareCoordinate ToSquareCoordinate(this (int col, int row) colRow)
		{
			if (colRow.col is < 0 or > 7 || colRow.row is < 0 or > 7)
			{
				throw new ArgumentOutOfRangeException(
					colRow.col is < 0 or > 7 ? nameof(colRow.col) : nameof(colRow.row),
					"Both indices must be in range 0...7");
			}

			return (colRow.row * 8 + colRow.col).ToSquare();
		}

		/// <summary>
		///     Maps a square to its zero-based index:
		///     A1 → 0, …, H8 → 63.
		///     <see cref="ChessSquareCoordinate.None" /> maps to –1.
		/// </summary>
		public static int ToIndex(this ChessSquareCoordinate square) =>
			square == ChessSquareCoordinate.None ? -1 : (int)square - 1;
	}
}
