using Bezoro.Chess.Domain.Types.Records;
using Bezoro.Chess.Domain.Types.Structs;

namespace Bezoro.Chess.Domain.Functions.Moves.Execution
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
			PromotionType promotionPieceType = move.PromotionPieceType == PromotionType.None
				? PromotionType.Queen
				: move.PromotionPieceType;

			state.PiecePositions[move.To.Row, move.To.Col] =
				new Piece((PieceType)promotionPieceType, move.Piece.Color);
		}
	}
}
