using System;
using System.Collections.Generic;
using System.Linq;
using Bezoro.Core.Chess.Interfaces;

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
		public static bool IsInside(this IChessBoardModel board, int col, int row)
		{
			if (board == null) throw new ArgumentNullException(nameof(board));
			return col >= 0 && col < board.Width && row >= 0 && row < board.Height;
		}

		public static IChessBoardSquareModel? GetEastSquare(this IChessBoardModel board, BoardPosition position) =>
			board.GetOffsetSquare(position, MapDirectionToOffsets(CardinalDirection.East));

		public static IChessBoardSquareModel? GetNorthEastSquare(this IChessBoardModel board, BoardPosition position) =>
			board.GetOffsetSquare(position, MapDirectionToOffsets(CardinalDirection.NorthEast));

		public static IChessBoardSquareModel? GetNorthSquare(this IChessBoardModel board, BoardPosition position) =>
			board.GetOffsetSquare(position, MapDirectionToOffsets(CardinalDirection.North));

		public static IChessBoardSquareModel? GetNorthWestSquare(this IChessBoardModel board, BoardPosition position) =>
			board.GetOffsetSquare(position, MapDirectionToOffsets(CardinalDirection.NorthWest));

		public static IChessBoardSquareModel? GetOffsetSquare(
			this IChessBoardModel board,
			BoardPosition position,
			int dx,
			int dy) =>
			board.GetOffsetSquare(position, (dx, dy));

		public static IChessBoardSquareModel? GetOffsetSquare(
			this IChessBoardModel board,
			BoardPosition position,
			(int dx, int dy) offset)
		{
			if (board == null)
				throw new ArgumentNullException(nameof(board));

			var file = position.File + offset.dx;
			var rank = position.Rank + offset.dy;

			return board.IsInside(file, rank) ? board.Squares[file, rank] : null;
		}

		public static IChessBoardSquareModel? GetSouthEastSquare(this IChessBoardModel board, BoardPosition position) =>
			board.GetOffsetSquare(position, MapDirectionToOffsets(CardinalDirection.SouthEast));

		public static IChessBoardSquareModel? GetSouthSquare(this IChessBoardModel board, BoardPosition position) =>
			board.GetOffsetSquare(position, MapDirectionToOffsets(CardinalDirection.South));

		public static IChessBoardSquareModel? GetSouthWestSquare(this IChessBoardModel board, BoardPosition position) =>
			board.GetOffsetSquare(position, MapDirectionToOffsets(CardinalDirection.SouthWest));

		public static IChessBoardSquareModel? GetWestSquare(this IChessBoardModel board, BoardPosition position) =>
			board.GetOffsetSquare(position, MapDirectionToOffsets(CardinalDirection.West));

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

		/// <summary>
		///     Returns all squares that can be reached by moving diagonally from the origin position,
		///     similar to a bishop's movement pattern.
		/// </summary>
		/// <param name="board">The chess board model.</param>
		/// <param name="origin">The starting position.</param>
		/// <returns>A collection of squares reachable by diagonal movement.</returns>
		public static IEnumerable<IChessBoardSquareModel> GetDiagonalSquares(
			this IChessBoardModel board,
			BoardPosition origin)
			=> board.GetSlidingSquares(origin, DirectionVectors.Diagonal);

		/// <summary>
		///     Returns all squares that can be reached by moving horizontally or vertically from the origin position,
		///     similar to a rook's movement pattern.
		/// </summary>
		/// <param name="board">The chess board model.</param>
		/// <param name="origin">The starting position.</param>
		/// <returns>A collection of squares reachable by orthogonal movement.</returns>
		public static IEnumerable<IChessBoardSquareModel> GetOrthogonalSquares(
			this IChessBoardModel board,
			BoardPosition origin)
			=> board.GetSlidingSquares(origin, DirectionVectors.Orthogonal);

		/// <summary>
		///     Returns all squares that can be reached by moving in any direction from the origin position,
		///     similar to a queen's movement pattern (combining rook and bishop movements).
		/// </summary>
		/// <param name="board">The chess board model.</param>
		/// <param name="origin">The starting position.</param>
		/// <returns>A collection of squares reachable by queen-like movement.</returns>
		public static IEnumerable<IChessBoardSquareModel> GetQueenSquares(
			this IChessBoardModel board,
			BoardPosition origin)
			=> board.GetSlidingSquares(
				origin,
				DirectionVectors.Orthogonal.Concat(DirectionVectors.Diagonal));

		/// <summary>
		///     Lists all squares reachable from <paramref name="origin" /> by
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

		public static IEnumerable<IChessBoardSquareModel> GetSquaresInDirection(
			this IChessBoardModel board,
			BoardPosition position,
			CardinalDirection direction)
		{
			var (dx, dy) = MapDirectionToOffsets(direction);

			return board.WalkRay(position, dx, dy);
		}

		/// <summary>
		///     Steps square-by-square from the field next to <paramref name="from" />
		///     following <paramref name="dx" /> / <paramref name="dy" /> until the board
		///     edge or the first occupied square (which is yielded and then the walk
		///     stops).
		/// </summary>
		public static IEnumerable<IChessBoardSquareModel> WalkRay(
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

		/// <summary>
		///     Creates a new chess piece of the specified type and color at the given algebraic position.
		/// </summary>
		/// <param name="board">The chess board model.</param>
		/// <param name="algebraicPosition">The target position in algebraic notation (e.g., "e4").</param>
		/// <param name="color">The color of the piece to create.</param>
		/// <param name="pieceType">The type of the piece to create.</param>
		public static void CreatePieceAt(
			this IChessBoardModel board,
			string algebraicPosition,
			PlayerColor color,
			ChessPieceType pieceType)
		{
			var position = AlgebraicNotationUtils.FromAlgebraic(algebraicPosition);
			var square   = board.GetSquare(position);
			var piece = ChessUtils.GetPieceFromChar(
				color == PlayerColor.White
					? char.ToUpper(pieceType.ToString()[0])
					: char.ToLower(pieceType.ToString()[0]));

			square.SetPiece(piece);
		}

		private static (int, int) MapDirectionToOffsets(CardinalDirection direction) =>
			direction switch
			{
				CardinalDirection.North     => (0, 1),
				CardinalDirection.NorthEast => (1, 1),
				CardinalDirection.East      => (1, 0),
				CardinalDirection.SouthEast => (1, -1),
				CardinalDirection.South     => (0, -1),
				CardinalDirection.SouthWest => (-1, -1),
				CardinalDirection.West      => (-1, 0),
				CardinalDirection.NorthWest => (-1, 1),
				_                           => throw new ArgumentException($"Invalid direction: {direction}")
			};
	}
}
