using System;

namespace Bezoro.Core.Chess.Utils
{
	public static class BoardUtils
	{
		// Helper method to get a piece from the board using algebraic notation (e.g., "e4")
		// Algebraic "a1" corresponds to Squares[0,0]
		// Algebraic "h8" corresponds to Squares[7,7] on an 8x8 board
		public static IChessPieceModel? GetPieceAt(IChessBoardModel board, string algebraicPosition)
		{
			// Use AlgebraicNotationUtils to convert algebraic string to ChessPosition
			ChessPosition position;
			try
			{
				position = AlgebraicNotationUtils.FromAlgebraic(algebraicPosition);
			}
			catch (ArgumentException ex) // Catch exceptions from FromAlgebraic
			{
				// Rethrow or handle as appropriate for GetPieceAt's contract
				// For example, you might want to throw a new ArgumentException
				// or return null if invalid algebraicPosition means "not found".
				// For now, rethrowing with the original parameter name if possible,
				// or a generic one.
				throw new ArgumentException(
					$"Invalid algebraic position: {ex.Message}",
					ex.ParamName == "algebraicSquare" ? nameof(algebraicPosition) : ex.ParamName,
					ex);
			}

			var file = position.File;
			var rank = position.Rank;

			if (file < 0 || file >= board.Width || rank < 0 || rank >= board.Height)
			{
				return null;
			}

			return board.Squares[file, rank]?.Piece;
		}
	}
}
