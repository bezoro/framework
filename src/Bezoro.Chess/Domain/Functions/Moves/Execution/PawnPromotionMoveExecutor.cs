using Bezoro.Chess.Domain.Shared.Enums;
using Bezoro.Chess.Domain.Types.Records;
using Bezoro.Chess.Domain.Types.Structs;

namespace Bezoro.Chess.Domain.Functions.Moves.Execution
{
	/// <summary>
	///     Executes a pawn-promotion move on the <see cref="GameState" />.
	/// </summary>
	internal static class PawnPromotionMoveExecutor
	{
		public static Board Execute(GameState state, Move move)
		{
			if (!move.IsPromotion)
			{
				return state.Board;
			}

			// Move the pawn to its destination first.
			Board newBoard = NormalMoveExecutor.Execute(state, move);
			// Chosen promotion piece, defaulting to Queen.
			PromotionType promotionPieceType = move.PromotionPieceType == PromotionType.None
				? PromotionType.Queen
				: move.PromotionPieceType;

			state    = state with { Board = newBoard };
			newBoard = state.Board.SetPiece(move.To, new Piece((PieceType)promotionPieceType, move.Piece.Color));
			return newBoard;
		}
	}
}
