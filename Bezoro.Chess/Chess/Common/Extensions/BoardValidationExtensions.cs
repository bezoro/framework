using System;
using System.Runtime.CompilerServices;
using Bezoro.Chess.Chess.Abstractions.Interfaces;
using Bezoro.Chess.Chess.Board;
using Bezoro.Chess.Chess.Common.Helpers;

namespace Bezoro.Chess.Chess.Common.Extensions
{
	public static class BoardValidationExtensions
	{
		/// <summary>
		///     Determines if the given algebraic coordinate lies inside the board boundaries.
		/// </summary>
		/// <param name="board">The chess board to check.</param>
		/// <param name="algebraicPosition">The position in algebraic notation (e.g., "e4", "h8").</param>
		/// <returns>True if the position is within board boundaries, false otherwise.</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool IsInside(this IChessBoardModel board, string algebraicPosition)
		{
			var position = AlgebraicNotationUtils.FromAlgebraic(algebraicPosition);
			return IsInside(board, position.Column, position.Row);
		}

		/// <summary>
		///     Determines if the given file and rank coordinates form a valid position on the board.
		/// </summary>
		/// <param name="board">The chess board to check.</param>
		/// <param name="col">The file (column) coordinate, 0-based.</param>
		/// <param name="row">The rank (row) coordinate, 0-based.</param>
		/// <returns>True if the coordinates are within board boundaries, false otherwise.</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool IsInside(this IChessBoardModel board, int col, int row)
		{
			if (board == null) throw new ArgumentNullException(nameof(board));
			return col >= 0 && col < board.Width &&
				   row >= 0 && row < board.Height;
		}

		public static bool IsInside(this IChessBoardModel board, BoardPosition position) =>
			board.IsInside(position.Column, position.Row);
	}
}
