using Bezoro.Chess.Domain.Board;

namespace Bezoro.Chess.Domain.Moves.Execution
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

			var rookFromPosition = new Position(row, rookFromCol);
			var rookToPosition   = new Position(row, rookToCol);
			var rook             = state.GetPieceAt(rookFromPosition);
			var rookMove         = Move.CreateNormal(rookFromPosition, rookToPosition, rook);

			NormalMoveExecutor.Execute(state, rookMove);
		}
	}
}
