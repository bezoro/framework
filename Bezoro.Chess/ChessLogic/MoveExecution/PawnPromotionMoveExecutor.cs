namespace Bezoro.Chess.ChessLogic
{
	/// <summary>
	///     Handles the execution of a pawn promotion, replacing the pawn with a queen.
	/// </summary>
	internal static class PawnPromotionMoveExecutor
	{
		public static void Execute(GameState state, Move move)
		{
			NormalMoveExecutor.Execute(state, move);

			// The color of the promoted piece belongs to the player who just moved.
			var movingPlayerColor = state.ActiveColor == PieceColor.White ? PieceColor.Black : PieceColor.White;

			state.PiecePositions[move.To.Row, move.To.Col] = new(PieceType.Queen, movingPlayerColor);
		}
	}
}
