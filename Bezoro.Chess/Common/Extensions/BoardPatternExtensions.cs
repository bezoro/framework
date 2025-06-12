using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Bezoro.Chess.Abstractions.Interfaces;
using Bezoro.Chess.Board;
using Bezoro.Chess.Common.Enums;
using Bezoro.Chess.Common.Helpers;

namespace Bezoro.Chess.Common.Extensions
{
	/// <summary>
	///     High-performance helpers for generating standard chess move patterns.
	///     All methods avoid per-call allocations and use <c>yield</c> enumeration
	///     so they can be consumed lazily by the move generator.
	/// </summary>
	public static class BoardPatternExtensions
	{
		private static readonly (int dx, int dy)[] _Diagonal   = DirectionVectors.DIAGONAL;
		private static readonly (int dx, int dy)[] _Orthogonal = DirectionVectors.ORTHOGONAL;
		private static readonly (int dx, int dy)[] _AllDirections =
			_Orthogonal.Concat(_Diagonal).ToArray();

		private static readonly (int dx, int dy)[] _KnightJumps =
		{
			(1, 2), (2, 1), (2, -1), (1, -2),
			(-1, -2), (-2, -1), (-2, 1), (-1, 2)
		};

		/* Pre-computed direction vectors ─ allocated once at type-init time */

		/// <summary>
		///     Returns the squares directly adjacent to <paramref name="position" />.
		///     Set <paramref name="includeDiagonals" /> to <c>true</c> to obtain the
		///     full king neighbourhood (8 squares); otherwise only the four
		///     orthogonal neighbours are returned.
		/// </summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static IEnumerable<IChessBoardSquareModel> GetAdjacentSquares(
			this IChessBoardModel board,
			BoardPosition position,
			bool includeDiagonals = false)
		{
			if (board is null) throw new ArgumentNullException(nameof(board));

			var dirs = includeDiagonals ? _AllDirections : _Orthogonal;

			foreach (var (dx, dy) in dirs)
			{
				if (board.IsInside((int)(position.Column + dx), (int)(position.Row + dy)))
					yield return board.Squares[position.Column + dx, position.Row + dy];
			}
		}

		/// <summary>Bishop move pattern (diagonal rays).</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static IEnumerable<IChessBoardSquareModel> GetDiagonalSquares(
			this IChessBoardModel board,
			BoardPosition origin) =>
			board.GetSlidingSquares(origin, _Diagonal);

		/// <summary>King move pattern (all adjacent squares).</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static IEnumerable<IChessBoardSquareModel> GetKingSquares(
			this IChessBoardModel board,
			BoardPosition origin) =>
			board.GetAdjacentSquares(origin, true);

		/// <summary>Knight move pattern (eight L-shaped jumps).</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static IEnumerable<IChessBoardSquareModel> GetKnightSquares(
			this IChessBoardModel board,
			BoardPosition origin)
		{
			if (board is null) throw new ArgumentNullException(nameof(board));

			foreach (var (dx, dy) in _KnightJumps)
			{
				if (board.IsInside((int)(origin.Column + dx), (int)(origin.Row + dy)))
					yield return board.Squares[origin.Column + dx, origin.Row + dy];
			}
		}

		/// <summary>Rook move pattern (orthogonal rays).</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static IEnumerable<IChessBoardSquareModel> GetOrthogonalSquares(
			this IChessBoardModel board,
			BoardPosition origin) =>
			board.GetSlidingSquares(origin, _Orthogonal);

		/// <summary>Queen move pattern (rook + bishop rays).</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static IEnumerable<IChessBoardSquareModel> GetQueenSquares(
			this IChessBoardModel board,
			BoardPosition origin) =>
			board.GetSlidingSquares(origin, _AllDirections);

		/// <summary>
		///     Returns every square reachable by sliding from <paramref name="origin" />
		///     in each of <paramref name="directions" /> until the board edge or a
		///     blocking piece is reached. The blocker square is yielded as well.
		/// </summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static IEnumerable<IChessBoardSquareModel> GetSlidingSquares(
			this IChessBoardModel board,
			BoardPosition origin,
			IEnumerable<(int dx, int dy)> directions)
		{
			if (board is null) throw new ArgumentNullException(nameof(board));
			if (directions is null) throw new ArgumentNullException(nameof(directions));

			foreach (var (dx, dy) in directions)
			foreach (var square in board.WalkRay(origin, dx, dy))
			{
				yield return square;
			}
		}

		/// <summary>
		///     Squares obtained by following a single <see cref="CardinalDirection" />
		///     ray from <paramref name="position" />.
		/// </summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static IEnumerable<IChessBoardSquareModel> GetSquaresInDirection(
			this IChessBoardModel board,
			BoardPosition position,
			CardinalDirection direction)
		{
			var (dx, dy) = BoardDirectionExtensions.MapDirectionToOffsets(direction);
			return board.WalkRay(position, dx, dy);
		}
	}

	/// <summary>
	///     Helpers for scanning a straight “ray” of squares starting from
	///     an origin and travelling in a constant <c>(dx,dy)</c> direction.
	/// </summary>
	public static class BoardRayExtensions
	{
		/// <summary>
		///     Enumerates all squares beginning one step away from
		///     <paramref name="origin" /> in the direction (<paramref name="dx" />,
		///     <paramref name="dy" />).
		///     The iteration stops at the first blocker (inclusive) or
		///     at the board edge.
		/// </summary>
		/// <remarks>
		///     • The starting square itself (<paramref name="origin" />) is
		///     never produced.<br />
		///     • The square containing a blocking piece <em>is</em> yielded so
		///     that the caller can decide whether a capture is legal.
		/// </remarks>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static IEnumerable<IChessBoardSquareModel> WalkRay(
			this IChessBoardModel board,
			BoardPosition origin,
			int dx,
			int dy)
		{
			if (board is null) throw new ArgumentNullException(nameof(board));

			// Advance one step to leave the origin square.
			var x = (int)(origin.Column + dx);
			var y = (int)(origin.Row    + dy);

			while (board.IsInside(x, y))
			{
				var square = board.Squares[x, y];
				yield return square;

				// Stop when a piece blocks further travel.
				if (square.Piece is not null)
					yield break;

				x += dx;
				y += dy;
			}
		}
	}
}
