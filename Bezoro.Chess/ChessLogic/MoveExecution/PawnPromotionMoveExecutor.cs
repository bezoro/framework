namespace Bezoro.Chess.ChessLogic.MoveExecution
{
	/// <summary>
	///     Executes a pawn-promotion move on the <see cref="GameState" />.
	/// </summary>
	internal static class PawnPromotionMoveExecutor
	{
		public static void Execute(GameState state, Move move)
		{
			// Move the pawn to its destination first.
			NormalMoveExecutor.Execute(state, move);

			// Chosen promotion piece, defaulting to Queen.
			var promotionType = move.PromotionPiece == PromotionType.None
				? PromotionType.Queen
				: move.PromotionPiece;

			state.PiecePositions[move.To.Row, move.To.Col] =
				new(promotionType.ToPieceType(), move.Color);
		}
	}
}
