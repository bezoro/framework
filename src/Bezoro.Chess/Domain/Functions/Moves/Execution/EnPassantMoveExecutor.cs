using Bezoro.Chess.Domain.Types.Records;
using Bezoro.Chess.Domain.Types.Structs;

namespace Bezoro.Chess.Domain.Functions.Moves.Execution
{
	/// <summary>
	///     Handles the execution of an en passant capture.
	/// </summary>
	internal static class EnPassantMoveExecutor
	{
		public static Board Execute(GameState state, Move move)
		{
			int       capturedPawnRow = move.From.Row;
			int       capturedPawnCol = move.To.Col;
			var       capturedPawnPos = new Position(capturedPawnRow, capturedPawnCol);
			Board     newBoard        = state.Board.RemovePiece(capturedPawnPos);
			GameState newState        = state with { Board = newBoard };

			newBoard = NormalMoveExecutor.Execute(newState, move);
			return newBoard;
		}
	}
}
