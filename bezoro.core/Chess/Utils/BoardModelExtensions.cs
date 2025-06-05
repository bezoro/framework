using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Bezoro.Core.Chess.Interfaces;

namespace Bezoro.Core.Chess.Utils
{
	/// <summary>
	///     Extension methods for IChessBoardModel to provide common chess board operations
	///     including piece movement, square access, and move pattern generation.
	/// </summary>
	public static class BoardModelExtensions
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
			board.GetOffsetSquare(position, MapDirectionToOffsets(CardinalDirection.East));

		/// <summary>
		///     Gets the square to the northeast of the specified position (diagonal).
		/// </summary>
		/// <param name="board">The chess board.</param>
		/// <param name="position">The starting position.</param>
		/// <returns>The square to the northeast if it exists; otherwise, null.</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static IChessBoardSquareModel? GetNorthEastSquare(this IChessBoardModel board, BoardPosition position) =>
			board.GetOffsetSquare(position, MapDirectionToOffsets(CardinalDirection.NorthEast));

		/// <summary>
		///     Gets the square directly north of the specified position.
		/// </summary>
		/// <param name="board">The chess board.</param>
		/// <param name="position">The starting position.</param>
		/// <returns>The square to the north if it exists; otherwise, null.</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static IChessBoardSquareModel? GetNorthSquare(this IChessBoardModel board, BoardPosition position) =>
			board.GetOffsetSquare(position, MapDirectionToOffsets(CardinalDirection.North));

		/// <summary>
		///     Gets the square to the northwest of the specified position (diagonal).
		/// </summary>
		/// <param name="board">The chess board.</param>
		/// <param name="position">The starting position.</param>
		/// <returns>The square to the northwest if it exists; otherwise, null.</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static IChessBoardSquareModel? GetNorthWestSquare(this IChessBoardModel board, BoardPosition position) =>
			board.GetOffsetSquare(position, MapDirectionToOffsets(CardinalDirection.NorthWest));

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
			board.GetOffsetSquare(position, MapDirectionToOffsets(CardinalDirection.SouthEast));

		/// <summary>
		///     Gets the square directly south of the specified position.
		/// </summary>
		/// <param name="board">The chess board.</param>
		/// <param name="position">The starting position.</param>
		/// <returns>The square to the south if it exists; otherwise, null.</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static IChessBoardSquareModel? GetSouthSquare(this IChessBoardModel board, BoardPosition position) =>
			board.GetOffsetSquare(position, MapDirectionToOffsets(CardinalDirection.South));

		/// <summary>
		///     Gets the square to the southwest of the specified position (diagonal).
		/// </summary>
		/// <param name="board">The chess board.</param>
		/// <param name="position">The starting position.</param>
		/// <returns>The square to the southwest if it exists; otherwise, null.</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static IChessBoardSquareModel? GetSouthWestSquare(this IChessBoardModel board, BoardPosition position) =>
			board.GetOffsetSquare(position, MapDirectionToOffsets(CardinalDirection.SouthWest));

		/// <summary>
		///     Gets the square directly west of the specified position.
		/// </summary>
		/// <param name="board">The chess board.</param>
		/// <param name="position">The starting position.</param>
		/// <returns>The square to the west if it exists; otherwise, null.</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static IChessBoardSquareModel? GetWestSquare(this IChessBoardModel board, BoardPosition position) =>
			board.GetOffsetSquare(position, MapDirectionToOffsets(CardinalDirection.West));

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
				? DirectionVectors.Orthogonal.Concat(DirectionVectors.Diagonal)
				: DirectionVectors.Orthogonal;

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
			board.GetSlidingSquares(origin, DirectionVectors.Diagonal);

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
		public static IEnumerable<IChessBoardSquareModel> GetKnightSquares(
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
			board.GetSlidingSquares(origin, DirectionVectors.Orthogonal);

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
				DirectionVectors.Orthogonal.Concat(DirectionVectors.Diagonal));

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
			var (dx, dy) = MapDirectionToOffsets(direction);

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
		/// <remarks>
		///     This method creates a new piece instance and places it on the board. If there is already
		///     a piece at the target position, it will be replaced by the new piece.
		/// </remarks>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
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

		/// <summary>
		///     Converts a cardinal direction enum value to corresponding offset coordinates.
		/// </summary>
		/// <param name="d">The cardinal direction.</param>
		/// <returns>A tuple containing the dx and dy offsets for the specified direction.</returns>
		/// <remarks>
		///     The coordinate system has (0,0) at the bottom-left with x increasing eastward and y increasing northward.
		///     North = (0,1), East = (1,0), South = (0,-1), West = (-1,0), and diagonals are combinations.
		/// </remarks>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static (int dx, int dy) MapDirectionToOffsets(CardinalDirection d) => d switch
		{
			CardinalDirection.North     => (0, 1),
			CardinalDirection.NorthEast => (1, 1),
			CardinalDirection.East      => (1, 0),
			CardinalDirection.SouthEast => (1, -1),
			CardinalDirection.South     => (0, -1),
			CardinalDirection.SouthWest => (-1, -1),
			CardinalDirection.West      => (-1, 0),
			CardinalDirection.NorthWest => (-1, 1),
			_                           => throw new ArgumentOutOfRangeException(nameof(d), d, null)
		};
	}
}
