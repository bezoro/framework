namespace Bezoro.Chess.ChessLogic.MoveExecution
{
	/// <summary>
	///     Handles the execution of a castling move (both kingside and queenside).
	/// </summary>
	internal static class CastleMoveExecutor
	{
		public static void Execute(GameState state, Move move)
		{
			// Move the king first.
			NormalMoveExecutor.Execute(state, move);

			// Determine if it's kingside or queenside based on the king's destination.
			var isKingside = move.To.Col > move.From.Col;

			// Now, move the corresponding rook.
			var rookFromCol = isKingside ? 7 : 0;
			var rookToCol   = isKingside ? 5 : 3;
			var row         = move.From.Row;

			var rookMove = new Move(new(row, rookFromCol), new(row, rookToCol), move.Color);
			NormalMoveExecutor.Execute(state, rookMove);
		}
	}
}
