namespace Bezoro.Chess.Domain.Moves.Execution
{
	/// <summary>
	///     Handles the execution of an en passant capture.
	/// </summary>
	internal static class EnPassantMoveExecutor
	{
		public static void Execute(GameState state, Move move)
		{
			NormalMoveExecutor.Execute(state, move);

			// Remove the captured pawn, which is on the same rank as the moving pawn's starting square.
			var capturedPawnRow = move.From.Row;
			var capturedPawnCol = move.To.Col;
			state.PiecePositions[capturedPawnRow, capturedPawnCol] = default;
		}
	}
}
