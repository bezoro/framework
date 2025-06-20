using Bezoro.Chess.Domain.Types.Records;
using Bezoro.Chess.Domain.Types.Structs;

namespace Bezoro.Chess.Domain.Functions.Moves.Execution
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
			bool isKingside = move.To.Col > move.From.Col;

			// Now, move the corresponding rook.
			int rookFromCol = isKingside ? 7 : 0;
			int rookToCol   = isKingside ? 5 : 3;
			int row         = move.From.Row;

			var   rookFromPosition = new Position(row, rookFromCol);
			var   rookToPosition   = new Position(row, rookToCol);
			Piece rook             = state.GetPieceAt(rookFromPosition);
			var   rookMove         = Move.CreateNormal(rookFromPosition, rookToPosition, rook);

			NormalMoveExecutor.Execute(state, rookMove);
		}
	}
}
