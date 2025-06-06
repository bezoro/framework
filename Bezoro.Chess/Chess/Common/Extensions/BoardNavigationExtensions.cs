using System;
using System.Runtime.CompilerServices;
using Bezoro.Chess.Chess.Abstractions.Interfaces;
using Bezoro.Chess.Chess.Board;
using Bezoro.Chess.Chess.Common.Enums;

namespace Bezoro.Chess.Chess.Common.Extensions
{
	public static class BoardNavigationExtensions
	{
		/// <summary>
		///     Attempts to retrieve a square at the specified offset from a given position.
		/// </summary>
		/// <param name="board">The chess board.</param>
		/// <param name="position">The starting position.</param>
		/// <param name="offset">The offset as (dx, dy) tuple.</param>
		/// <param name="square">When successful, contains the found square; otherwise, null.</param>
		/// <returns>True if a valid square was found at the offset, false otherwise.</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool TryGetOffsetSquare(
			this IChessBoardModel board,
			BoardPosition position,
			(int dx, int dy) offset,
			out IChessBoardSquareModel? square)
		{
			square = board.GetOffsetSquare(position, offset);
			return square != null;
		}

		/// <summary>
		///     Gets the square directly east of the specified position.
		/// </summary>
		/// <param name="board">The chess board.</param>
		/// <param name="position">The starting position.</param>
		/// <returns>The square to the east if it exists; otherwise, null.</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static IChessBoardSquareModel? GetEastSquare(this IChessBoardModel board, BoardPosition position) =>
			board.GetOffsetSquare(position, BoardDirectionExtensions.MapDirectionToOffsets(CardinalDirection.East));

		/// <summary>
		///     Gets the square to the northeast of the specified position (diagonal).
		/// </summary>
		/// <param name="board">The chess board.</param>
		/// <param name="position">The starting position.</param>
		/// <returns>The square to the northeast if it exists; otherwise, null.</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static IChessBoardSquareModel? GetNorthEastSquare(this IChessBoardModel board, BoardPosition position) =>
			board.GetOffsetSquare(
				position, BoardDirectionExtensions.MapDirectionToOffsets(CardinalDirection.NorthEast));

		/// <summary>
		///     Gets the square directly north of the specified position.
		/// </summary>
		/// <param name="board">The chess board.</param>
		/// <param name="position">The starting position.</param>
		/// <returns>The square to the north if it exists; otherwise, null.</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static IChessBoardSquareModel? GetNorthSquare(this IChessBoardModel board, BoardPosition position) =>
			board.GetOffsetSquare(position, BoardDirectionExtensions.MapDirectionToOffsets(CardinalDirection.North));

		/// <summary>
		///     Gets the square to the northwest of the specified position (diagonal).
		/// </summary>
		/// <param name="board">The chess board.</param>
		/// <param name="position">The starting position.</param>
		/// <returns>The square to the northwest if it exists; otherwise, null.</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static IChessBoardSquareModel? GetNorthWestSquare(this IChessBoardModel board, BoardPosition position) =>
			board.GetOffsetSquare(
				position, BoardDirectionExtensions.MapDirectionToOffsets(CardinalDirection.NorthWest));

		/// <summary>
		///     Retrieves a square by adding the given offset to the specified position.
		/// </summary>
		/// <param name="board">The chess board.</param>
		/// <param name="position">The starting position.</param>
		/// <param name="dx">The horizontal offset (positive for east, negative for west).</param>
		/// <param name="dy">The vertical offset (positive for north, negative for south).</param>
		/// <returns>The square at the offset position if it exists; otherwise, null.</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static IChessBoardSquareModel? GetOffsetSquare(
			this IChessBoardModel board,
			BoardPosition position,
			int dx,
			int dy) =>
			board.GetOffsetSquare(position, (dx, dy));

		/// <summary>
		///     Retrieves a square by adding the given offset tuple to the specified position.
		/// </summary>
		/// <param name="board">The chess board.</param>
		/// <param name="position">The starting position.</param>
		/// <param name="offset">The offset as a tuple (dx, dy) where dx is horizontal and dy is vertical.</param>
		/// <returns>The square at the offset position if it exists; otherwise, null.</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static IChessBoardSquareModel? GetOffsetSquare(
			this IChessBoardModel board,
			BoardPosition position,
			(int dx, int dy) offset)
		{
			if (board == null) throw new ArgumentNullException(nameof(board));

			var file = position.File + offset.dx;
			var rank = position.Rank + offset.dy;

			return board.IsInside(file, rank) ? board.Squares[file, rank] : null;
		}

		/// <summary>
		///     Gets the square to the southeast of the specified position (diagonal).
		/// </summary>
		/// <param name="board">The chess board.</param>
		/// <param name="position">The starting position.</param>
		/// <returns>The square to the southeast if it exists; otherwise, null.</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static IChessBoardSquareModel? GetSouthEastSquare(this IChessBoardModel board, BoardPosition position) =>
			board.GetOffsetSquare(
				position, BoardDirectionExtensions.MapDirectionToOffsets(CardinalDirection.SouthEast));

		/// <summary>
		///     Gets the square directly south of the specified position.
		/// </summary>
		/// <param name="board">The chess board.</param>
		/// <param name="position">The starting position.</param>
		/// <returns>The square to the south if it exists; otherwise, null.</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static IChessBoardSquareModel? GetSouthSquare(this IChessBoardModel board, BoardPosition position) =>
			board.GetOffsetSquare(position, BoardDirectionExtensions.MapDirectionToOffsets(CardinalDirection.South));

		/// <summary>
		///     Gets the square to the southwest of the specified position (diagonal).
		/// </summary>
		/// <param name="board">The chess board.</param>
		/// <param name="position">The starting position.</param>
		/// <returns>The square to the southwest if it exists; otherwise, null.</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static IChessBoardSquareModel? GetSouthWestSquare(this IChessBoardModel board, BoardPosition position) =>
			board.GetOffsetSquare(
				position, BoardDirectionExtensions.MapDirectionToOffsets(CardinalDirection.SouthWest));

		/// <summary>
		///     Gets the square directly west of the specified position.
		/// </summary>
		/// <param name="board">The chess board.</param>
		/// <param name="position">The starting position.</param>
		/// <returns>The square to the west if it exists; otherwise, null.</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static IChessBoardSquareModel? GetWestSquare(this IChessBoardModel board, BoardPosition position) =>
			board.GetOffsetSquare(position, BoardDirectionExtensions.MapDirectionToOffsets(CardinalDirection.West));
	}
}
