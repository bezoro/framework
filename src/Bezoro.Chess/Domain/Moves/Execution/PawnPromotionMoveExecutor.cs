using Bezoro.Chess.Domain.Board;

namespace Bezoro.Chess.Domain.Moves.Execution
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
			PieceType promotionPieceType = move.PromotionPiece == PieceType.None
				? PieceType.Queen
				: move.PromotionPiece;

			state.PiecePositions[move.To.Row, move.To.Col] =
				new(promotionPieceType, move.Piece.Color);
		}
	}
}
