using Bezoro.Chess.Domain.Types.Records;
using Bezoro.Chess.Domain.Types.Structs;

namespace Bezoro.Chess.Domain.Functions.Moves.Execution
{
	/// <summary>
	///     Handles the execution of a normal move (moving a piece from one square to another).
	///     This also covers captures, as they are mechanically the same as a normal move.
	/// </summary>
	internal static class NormalMoveExecutor
	{
		public static Board Execute(in GameState state, in Move move)
		{
			Board board = state.Board;

			board = board.SetPiece(move.To, board.GetPiece(move.From));
			board = board.RemovePiece(move.From);
			return board;
		}
	}
}
