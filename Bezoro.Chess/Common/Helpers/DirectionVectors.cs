using System.Collections.Generic;
using System.Linq;

namespace Bezoro.Chess.Common.Helpers
{
	/// <summary>
	///     Central repository for (dx, dy) offset arrays used by move generators,
	///     board helpers, and attack detection.
	///     Provides direction vectors for all chess piece movement patterns.
	/// </summary>
	public static class DirectionVectors
	{
		/// <summary>
		///     Offsets for bishop-style sliding moves (diagonal directions).
		///     Used by bishops and as part of queen movement.
		/// </summary>
		public static readonly (int dx, int dy)[] DIAGONAL =
		{
			(-1, -1), (1, -1), // Down-Left, Down-Right
			(-1, 1), (1, 1)    // Up-Left, Up-Right
		};
		/// <summary>
		///     Offsets for rook-style sliding moves (horizontal and vertical).
		///     Used by rooks and as part of queen movement.
		/// </summary>
		public static readonly (int dx, int dy)[] ORTHOGONAL =
		{
			(-1, 0), (1, 0), // Left, Right
			(0, -1), (0, 1)  // Down, Up
		};
		/// <summary>
		///     Offsets to the eight neighboring squares around a single square.
		///     Used by kings for normal moves and for general adjacency checks.
		///     Combines both orthogonal and diagonal directions.
		/// </summary>
		public static readonly (int dx, int dy)[] KING =
			ORTHOGONAL.Concat(DIAGONAL).ToArray();

		/// <summary>
		///     Offsets for a knight's L-shaped jumps (8 possible destinations).
		///     Knights move in an L-shape: 2 squares in one direction and 1 square perpendicular.
		/// </summary>
		public static readonly (int dx, int dy)[] KNIGHT =
		{
			(-2, -1), (-1, -2), (1, -2), (2, -1), // Upper half
			(2, 1), (1, 2), (-1, 2), (-2, 1)      // Lower half
		};

		/// <summary>
		///     All sliding piece directions (rook + bishop = queen directions).
		///     Useful for general sliding piece calculations.
		/// </summary>
		public static IEnumerable<(int dx, int dy)> AllSliding => Queen;

		/// <summary>
		///     Convenience property that returns all queen directions
		///     (i.e. orthogonal and diagonal) in a single enumerable.
		///     Queens combine the movement patterns of rooks and bishops.
		/// </summary>
		public static IEnumerable<(int dx, int dy)> Queen =>
			ORTHOGONAL.Concat(DIAGONAL);

		/// <summary>
		///     Gets the number of directions for each piece type.
		/// </summary>
		public static class DirectionCounts
		{
			/// <summary>Number of diagonal directions (bishop moves).</summary>
			public const int Diagonal = 4;

			/// <summary>Number of king move directions.</summary>
			public const int King = 8;

			/// <summary>Number of knight move directions.</summary>
			public const int Knight = 8;
			/// <summary>Number of orthogonal directions (rook moves).</summary>
			public const int Orthogonal = 4;

			/// <summary>Number of queen move directions.</summary>
			public const int Queen = 8;
		}
	}
}
