using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Bezoro.Core.Chess.Abstractions.Interfaces;
using Bezoro.Core.Chess.Board;
using Bezoro.Core.Chess.Common.Enums;
using Bezoro.Core.Chess.Common.Helpers;

namespace Bezoro.Core.Chess.Common.Extensions
{
	public static class BoardPatternExtensions
	{
		/// <summary>
		///     Returns every square that lies directly next to
		///     <paramref name="position" /> on the board.
		///     Set <paramref name="includeDiagonals" /> to <c>true</c> to get all eight
		///     neighbouring squares; otherwise only the four orthogonal ones are
		///     returned.
		/// </summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static IEnumerable<IChessBoardSquareModel> GetAdjacentSquares(
			this IChessBoardModel board,
			BoardPosition position,
			bool includeDiagonals = false)
		{
			if (board    == null) throw new ArgumentNullException(nameof(board));
			if (position == null) throw new ArgumentNullException(nameof(position));

			var dirs = includeDiagonals
				? DirectionVectors.ORTHOGONAL.Concat(DirectionVectors.DIAGONAL)
				: DirectionVectors.ORTHOGONAL;

			foreach (var (dx, dy) in dirs)
			{
				if (board.IsInside(position.File + dx, position.Rank + dy))
					yield return board.Squares[position.File + dx, position.Rank + dy];
			}
		}

		/// <summary>
		///     Returns all squares reachable by a bishop from the given position.
		///     This includes all diagonal rays extending from the origin until the board edge or a piece is encountered.
		/// </summary>
		/// <param name="board">The chess board.</param>
		/// <param name="origin">The starting position.</param>
		/// <returns>A collection of squares along all diagonal rays from the origin.</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static IEnumerable<IChessBoardSquareModel> GetDiagonalSquares(
			this IChessBoardModel board,
			BoardPosition origin) =>
			board.GetSlidingSquares(origin, DirectionVectors.DIAGONAL);

		/// <summary>
		///     Shortcut for king moves – simply returns the adjacent squares
		///     (orthogonal + diagonal).
		/// </summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static IEnumerable<IChessBoardSquareModel> GetKingSquares(
			this IChessBoardModel board,
			BoardPosition origin) =>
			board.GetAdjacentSquares(origin, true);

		/// <summary>
		///     Returns squares in the eight knight-jump positions around
		///     <paramref name="origin" />.
		/// </summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static unsafe IEnumerable<IChessBoardSquareModel> GetKnightSquares(
			this IChessBoardModel board,
			BoardPosition origin)
		{
			if (board == null) throw new ArgumentNullException(nameof(board));

			ReadOnlySpan<(int dx, int dy)> jumps = stackalloc (int, int)[]
			{
				(1, 2), (2, 1), (2, -1), (1, -2),
				(-1, -2), (-2, -1), (-2, 1), (-1, 2)
			};

			var result = new List<IChessBoardSquareModel>(8);

			foreach (var (dx, dy) in jumps)
			{
				if (board.IsInside(origin.File + dx, origin.Rank + dy))
					result.Add(board.Squares[origin.File + dx, origin.Rank + dy]);
			}

			return result;
		}

		/// <summary>
		///     Returns all squares reachable by a rook from the given position.
		///     This includes all horizontal and vertical rays extending from the origin
		///     until the board edge or a piece is encountered.
		/// </summary>
		/// <param name="board">The chess board.</param>
		/// <param name="origin">The starting position.</param>
		/// <returns>A collection of squares along all orthogonal rays from the origin.</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static IEnumerable<IChessBoardSquareModel> GetOrthogonalSquares(
			this IChessBoardModel board,
			BoardPosition origin) =>
			board.GetSlidingSquares(origin, DirectionVectors.ORTHOGONAL);

		/// <summary>
		///     Returns all squares reachable by a queen from the given position.
		///     This combines diagonal and orthogonal rays extending from the origin
		///     until the board edge or a piece is encountered.
		/// </summary>
		/// <param name="board">The chess board.</param>
		/// <param name="origin">The starting position.</param>
		/// <returns>A collection of squares along all queen-movement rays from the origin.</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static IEnumerable<IChessBoardSquareModel> GetQueenSquares(
			this IChessBoardModel board,
			BoardPosition origin) =>
			board.GetSlidingSquares(
				origin,
				DirectionVectors.ORTHOGONAL.Concat(DirectionVectors.DIAGONAL));

		/// <summary>
		///     Returns all squares reachable by sliding from the origin in the specified directions
		///     until a board edge or a blocking piece is encountered.
		/// </summary>
		/// <param name="board">The chess board.</param>
		/// <param name="origin">The starting position.</param>
		/// <param name="directions">A collection of direction vectors as (dx, dy) tuples.</param>
		/// <returns>A collection of squares reachable by sliding in the specified directions.</returns>
		/// <remarks>
		///     When a blocking piece is encountered, that square is included in the results before stopping in that direction.
		///     This method is used for generating moves for sliding pieces like bishops, rooks, and queens.
		/// </remarks>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
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

		/// <summary>
		///     Returns all squares along a ray in the specified cardinal direction from the given position.
		/// </summary>
		/// <param name="board">The chess board.</param>
		/// <param name="position">The starting position.</param>
		/// <param name="direction">The cardinal direction to follow.</param>
		/// <returns>A collection of squares along the ray in the specified direction.</returns>
		/// <remarks>
		///     This method walks the board in the specified direction until reaching the board edge or an occupied square.
		///     If an occupied square is encountered, it is included in the results before stopping.
		/// </remarks>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static IEnumerable<IChessBoardSquareModel> GetSquaresInDirection(
			this IChessBoardModel board,
			BoardPosition position,
			CardinalDirection direction)
		{
			var (dx, dy) = BoardDirectionExtensions.MapDirectionToOffsets(direction);

			return board.WalkRay(position, dx, dy);
		}

		/// <summary>
		///     Walks a ray starting from the position next to the origin in the specified direction
		///     until reaching a board edge or an occupied square.
		/// </summary>
		/// <param name="board">The chess board.</param>
		/// <param name="from">The origin position (the walk starts from the adjacent square).</param>
		/// <param name="dx">The horizontal direction component (-1, 0, or 1).</param>
		/// <param name="dy">The vertical direction component (-1, 0, or 1).</param>
		/// <returns>A collection of squares along the ray.</returns>
		/// <remarks>
		///     When an occupied square is encountered, it is included in the results before stopping.
		///     This method is used by the sliding piece move generators and for checking lines of attack.
		/// </remarks>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static IEnumerable<IChessBoardSquareModel> WalkRay(
			this IChessBoardModel board,
			BoardPosition from,
			int dx,
			int dy)
		{
			var file = from.Column + dx;
			var rank = from.Row    + dy;

			while (board.IsInside(file, rank))
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
