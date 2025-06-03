using System;
using System.Collections.Generic;
using System.Linq;

namespace Bezoro.Core.Chess.Utils
{
	public static class BoardModelExtensions
	{
		/// <summary>
		///     Determines whether the specified algebraic position is within the board boundaries.
		/// </summary>
		/// <param name="board">The chess board model to check against.</param>
		/// <param name="algebraicPosition">The position in algebraic notation (e.g., "e4", "h8").</param>
		/// <returns>
		///     <c>true</c> if the position is inside the board boundaries; otherwise, <c>false</c>.
		/// </returns>
		public static bool IsInside(this IChessBoardModel board, string algebraicPosition)
		{
			var position = AlgebraicNotationUtils.FromAlgebraic(algebraicPosition);
			return IsInside(board, position.Column, position.Row);
		}

		/// <summary>
		///     Determines whether the specified column and row coordinates are within the board boundaries.
		/// </summary>
		/// <param name="board">The chess board model to check against.</param>
		/// <param name="col">The column (file) coordinate to check.</param>
		/// <param name="row">The row (rank) coordinate to check.</param>
		/// <returns>
		///     <c>true</c> if the coordinates are inside the board boundaries; otherwise, <c>false</c>.
		/// </returns>
		public static bool IsInside(this IChessBoardModel board, int col, int row) =>
			col    >= 0
			&& col < board.Width
			&& row >= 0
			&& row < board.Height;

		/// <summary>
		///     Returns every square that lies directly next to
		///     <paramref name="position" /> on the board.
		///     Set <paramref name="includeDiagonals" /> to <c>true</c> to get all eight
		///     neighbouring squares; otherwise only the four orthogonal ones are
		///     returned.
		/// </summary>
		public static IEnumerable<IChessBoardSquareModel> GetAdjacentSquares(
			this IChessBoardModel board,
			BoardPosition position,
			bool includeDiagonals = false)
		{
			if (board    == null) throw new ArgumentNullException(nameof(board));
			if (position == null) throw new ArgumentNullException(nameof(position));

			var dirs = includeDiagonals
				? DirectionVectors.Orthogonal.Concat(DirectionVectors.Diagonal)
				: DirectionVectors.Orthogonal;

			foreach (var (dx, dy) in dirs)
			{
				var file = position.File + dx;
				var rank = position.Rank + dy;

				if (file >= 0 && file < board.Width && rank >= 0 && rank < board.Height)
				{
					yield return board.Squares[file, rank];
				}
			}
		}

		public static IEnumerable<IChessBoardSquareModel> GetDiagonalSquares(
			this IChessBoardModel board,
			BoardPosition origin)
			=> board.GetSlidingSquares(origin, DirectionVectors.Diagonal);

		public static IEnumerable<IChessBoardSquareModel> GetOrthogonalSquares(
			this IChessBoardModel board,
			BoardPosition origin)
			=> board.GetSlidingSquares(origin, DirectionVectors.Orthogonal);

		public static IEnumerable<IChessBoardSquareModel> GetQueenSquares(
			this IChessBoardModel board,
			BoardPosition origin)
			=> board.GetSlidingSquares(
				origin,
				DirectionVectors.Orthogonal.Concat(DirectionVectors.Diagonal));

		/// <summary>
		///     Enumerates all squares reachable from <paramref name="origin" /> by
		///     sliding in every given <paramref name="directions" /> ray until an
		///     edge or a blocking piece is met.
		///     The blocking square (if any) is included in the result.
		/// </summary>
		public static IEnumerable<IChessBoardSquareModel> GetSlidingSquares(
			this IChessBoardModel board,
			BoardPosition origin,
			IEnumerable<(int dx, int dy)> directions)
		{
			if (board      == null) throw new ArgumentNullException(nameof(board));
			if (origin     == null) throw new ArgumentNullException(nameof(origin));
			if (directions == null) throw new ArgumentNullException(nameof(directions));

			foreach (var (dx, dy) in directions)
			foreach (var sq in board.WalkRay(origin, dx, dy))
			{
				yield return sq;
			}
		}

		public static void CreatePieceAt(
			this IChessBoardModel board,
			string algebraicPosition,
			PlayerColor color,
			ChessPieceType pieceType) =>
			throw new NotImplementedException();

		/// <summary>
		///     Steps square-by-square from the field next to <paramref name="from" />
		///     following <paramref name="dx" /> / <paramref name="dy" /> until the board
		///     edge or the first occupied square (which is yielded and then the walk
		///     stops).
		/// </summary>
		private static IEnumerable<IChessBoardSquareModel> WalkRay(
			this IChessBoardModel board,
			BoardPosition from,
			int dx,
			int dy)
		{
			var file = from.File + dx;
			var rank = from.Rank + dy;

			while (file >= 0 && file < board.Width && rank >= 0 && rank < board.Height)
			{
				var square = board.Squares[file, rank];
				yield return square;

				if (square.GetPiece() != null)
					yield break; // stop after the first occupied square

				file += dx;
				rank += dy;
			}
		}
	}
}
