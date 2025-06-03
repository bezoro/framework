namespace Bezoro.Core.Chess.Utils
{
	/// <summary>
	///     Central place for direction vectors used by sliding pieces and adjacency helpers.
	/// </summary>
	public static class DirectionVectors
	{
		/// <summary>Diagonal directions (bishop-like).</summary>
		public static readonly (int dx, int dy)[] Diagonal =
		{
			(-1, 1), (1, 1), (1, -1), (-1, -1)
		};
		/// <summary>Orthogonal directions (rook-like).</summary>
		public static readonly (int dx, int dy)[] Orthogonal =
		{
			(-1, 0), (1, 0), (0, -1), (0, 1)
		};
	}
}
