using System;

namespace Bezoro.Core.Chess.Utils
{
	public static class BoardUtils
	{
		public static IChessBoardSquareModel GetSquareAt(this IChessBoardModel board, string algebraicPosition)
		{
			BoardPosition position;

			try
			{
				position = AlgebraicNotationUtils.FromAlgebraic(algebraicPosition);
			}
			catch (ArgumentException e)
			{
				Console.WriteLine(e);
				throw;
			}

			var file = position.File;
			var rank = position.Rank;
			return IsPositionWithinBoardBounds(board, file, rank) ? null! : board.Squares[file, rank];
		}

		/// <summary>
		///     Helper method to get a piece from the board using algebraic notation (e.g., "e4").
		/// </summary>
		/// <remarks>
		///     Algebraic "a1" corresponds to Squares[0,0].
		///     Algebraic "h8" corresponds to Squares[7,7] on an 8x8 board.
		/// </remarks>
		public static IChessPieceModel? GetPieceAt(this IChessBoardModel board, string algebraicPosition)
		{
			BoardPosition position;

			try
			{
				position = AlgebraicNotationUtils.FromAlgebraic(algebraicPosition);
			}
			catch (ArgumentException e)
			{
				Console.WriteLine(e);
				throw;
			}

			var file = position.File;
			var rank = position.Rank;
			return IsPositionWithinBoardBounds(board, file, rank) ? null : board.Squares[file, rank].Piece;
		}

		private static bool IsPositionWithinBoardBounds(IChessBoardModel board, int file, int rank) =>
			file < 0 || file >= board.Width || rank < 0 || rank >= board.Height;
	}
}
