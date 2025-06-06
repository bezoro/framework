using System.Collections.Generic;
using System.Linq;

namespace Bezoro.Chess.Common.Helpers
{
	/// <summary>
	///     Central repository for (dx, dy) offset arrays used by move generators,
	///     board helpers, and attack detection.
	/// </summary>
	public static class DirectionVectors
	{
		/// <summary>Offsets for bishop-style sliding.</summary>
		public static readonly (int dx, int dy)[] DIAGONAL =
		{
			(-1, 1), (1, 1),
			(1, -1), (-1, -1)
		};

		/// <summary>
		///     Offsets for a knight’s L-shaped jumps (8 destinations).
		/// </summary>
		public static readonly (int dx, int dy)[] KNIGHT =
		{
			(-2, -1), (-1, -2), (1, -2), (2, -1),
			(2, 1), (1, 2), (-1, 2), (-2, 1)
		};
		/// <summary>Offsets for rook-style sliding.</summary>
		public static readonly (int dx, int dy)[] ORTHOGONAL =
		{
			(-1, 0), (1, 0),
			(0, -1), (0, 1)
		};
		/// <summary>
		///     Offsets to the eight neighboring squares around a single square.
		/// </summary>
		public static readonly IEnumerable<(int dx, int dy)> KING =
			ORTHOGONAL.Concat(DIAGONAL);

		/// <summary>
		///     Convenience helper that returns all queen directions
		///     (i.e. orthogonal and diagonal) in a single enumerable.
		/// </summary>
		public static IEnumerable<(int dx, int dy)> Queen =>
			ORTHOGONAL.Concat(DIAGONAL);
	}
}
