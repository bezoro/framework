namespace Bezoro.Chess.ChessLogic
{
	/// <summary>
	///     Handles the execution of a normal move (moving a piece from one square to another).
	///     This also covers captures, as they are mechanically the same as a normal move.
	/// </summary>
	internal static class NormalMoveExecutor
	{
		public static void Execute(GameState state, Move move)
		{
			state.PiecePositions[move.To.Row, move.To.Col]     = state.PiecePositions[move.From.Row, move.From.Col];
			state.PiecePositions[move.From.Row, move.From.Col] = default;
		}
	}
}
