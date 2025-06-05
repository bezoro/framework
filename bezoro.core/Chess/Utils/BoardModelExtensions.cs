using System.Runtime.CompilerServices;
using Bezoro.Core.Chess.Interfaces;

namespace Bezoro.Core.Chess.Utils
{
	/// <summary>
	///     Extension methods for IChessBoardModel to provide common chess board operations
	///     including piece movement, square access, and move pattern generation.
	/// </summary>
	public static class BoardModelExtensions
	{
		/// <summary>
		///     Creates a new chess piece of the specified type and color at the given algebraic position.
		/// </summary>
		/// <param name="board">The chess board model.</param>
		/// <param name="algebraicPosition">The target position in algebraic notation (e.g., "e4").</param>
		/// <param name="color">The color of the piece to create.</param>
		/// <param name="pieceType">The type of the piece to create.</param>
		/// <remarks>
		///     This method creates a new piece instance and places it on the board. If there is already
		///     a piece at the target position, it will be replaced by the new piece.
		/// </remarks>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void CreatePieceAt(
			this IChessBoardModel board,
			string algebraicPosition,
			PlayerColor color,
			ChessPieceType pieceType)
		{
			var position = AlgebraicNotationUtils.FromAlgebraic(algebraicPosition);
			var square   = board.GetSquare(position);
			var piece = ChessUtils.GetPieceFromChar(
				color == PlayerColor.White
					? char.ToUpper(pieceType.ToString()[0])
					: char.ToLower(pieceType.ToString()[0]));

			square.SetPiece(piece);
		}
	}
}
