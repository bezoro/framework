using System.Runtime.CompilerServices;
using System.Text;
using Bezoro.Chess.Chess.Abstractions.Interfaces;
using Bezoro.Chess.Chess.Board;
using Bezoro.Chess.Chess.Common.Enums;
using Bezoro.Chess.Chess.Common.Helpers;

namespace Bezoro.Chess.Chess.Common.Extensions
{
	/// <summary>
	///     Extension methods for IChessBoardModel to provide common chess board operations
	///     including piece movement, square access, and move pattern generation.
	/// </summary>
	public static class BoardModelExtensions
	{
		public static IChessBoardSquareModel GetSquareAt(this IChessBoardModel board, string algebraicPosition)
		{
			var position = AlgebraicNotationUtils.FromAlgebraic(algebraicPosition);
			var col      = position.Column;
			var row      = position.Row;
			return board.GetSquareAt(col, row);
		}

		public static IChessBoardSquareModel GetSquareAt(this IChessBoardModel board, BoardPosition position) =>
			board.GetSquareAt(position.Column, position.Row);

		public static IChessBoardSquareModel GetSquareAt(this IChessBoardModel board, int col, int row) =>
			!board.IsInside(col, row) ? null! : board.Squares[col, row];

		/// <summary>
		///     Helper method to get a piece from the board using algebraic notation (e.g., "e4").
		/// </summary>
		/// <remarks>
		///     Algebraic "a1" corresponds to Squares[0,0].
		///     Algebraic "h8" corresponds to Squares[7,7] on an 8x8 board.
		/// </remarks>
		public static IChessPieceModel? GetPieceAt(this IChessBoardModel board, string algebraicPosition)
		{
			var position = AlgebraicNotationUtils.FromAlgebraic(algebraicPosition);

			var col = position.Column;
			var row = position.Row;
			return board.GetPieceAt(col, row);
		}

		public static IChessPieceModel? GetPieceAt(this IChessBoardModel board, BoardPosition position) =>
			board.GetPieceAt(position.Column, position.Row);

		public static IChessPieceModel? GetPieceAt(this IChessBoardModel board, int col, int row) =>
			!board.IsInside(col, row) ? null : board.Squares[col, row].Piece;

		/// <summary>
		///     Generates the piece placement part of the Forsyth-Edwards Notation (FEN) string.
		/// </summary>
		/// <returns>The FEN piece placement string.</returns>
		public static string GetPiecePlacementFen(this IChessBoardModel board)
		{
			var fenPart = new StringBuilder();
			for (var rank = board.Height - 1 ; rank >= 0 ; rank--)
			{
				var emptySquares = 0;
				for (var file = 0 ; file < board.Width ; file++)
				{
					var piece = board.GetPieceAt(new BoardPosition(file, rank));
					if (piece == null)
						emptySquares++;
					else
					{
						if (emptySquares > 0)
						{
							fenPart.Append(emptySquares);
							emptySquares = 0;
						}

						fenPart.Append(ChessUtils.GetCharFromPiece(piece));
					}
				}

				if (emptySquares > 0)
					fenPart.Append(emptySquares);

				if (rank > 0)
					fenPart.Append('/');
			}

			return fenPart.ToString();
		}

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
			board.CreatePieceAt(position.Column, position.Row, color, pieceType);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void CreatePieceAt(
			this IChessBoardModel board,
			BoardPosition position,
			PlayerColor color,
			ChessPieceType pieceType) =>
			board.CreatePieceAt(position.Column, position.Row, color, pieceType);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void CreatePieceAt(
			this IChessBoardModel board,
			int col,
			int row,
			PlayerColor color,
			ChessPieceType pieceType)
		{
			var square = board.GetSquareAt(col, row);
			var piece = ChessUtils.GetPieceFromChar(
				color == PlayerColor.White
					? char.ToUpper(pieceType.ToString()[0])
					: char.ToLower(pieceType.ToString()[0]));

			square.SetPiece(piece);
		}
	}
}
