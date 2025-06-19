using System.Collections.Generic;
using Bezoro.Chess.Domain.Board;

namespace Bezoro.Chess.Domain.Moves.Generation
{
	/// <summary>
	///     Generates legal moves for bishop pieces on the chess board.
	/// </summary>
	internal static class BishopMoveGenerator
	{
		/// <summary>
		///     The four diagonal directions that a bishop can move: northwest, northeast, southwest, southeast.
		/// </summary>
		private static readonly (int dRow, int dCol)[] Directions = { (-1, -1), (-1, 1), (1, -1), (1, 1) };

		/// <summary>
		///     Generates all possible moves for a bishop at the specified position given the current game state.
		/// </summary>
		/// <param name="from">The position of the bishop on the board.</param>
		/// <param name="gameState">The current state of the game.</param>
		/// <returns>A collection of valid moves for the bishop.</returns>
		public static IEnumerable<Move> GenerateMoves(Position from, GameState gameState) =>
			SlidingPieceMoveGenerator.GenerateMoves(from, gameState, Directions);
	}
}
