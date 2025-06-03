using System;

namespace Bezoro.Core.Chess.Utils
{
	public static class BoardModelExtensions
	{
		public static bool IsValidPosition(this IChessBoardModel board, string algebraicPosition)
		{
			var position = AlgebraicNotationUtils.FromAlgebraic(algebraicPosition);
			return position.Column    >= 0
				   && position.Column < board.Width
				   && position.Rank   >= 0
				   && position.Rank   < board.Height;
		}

		public static void CreatePieceAt(
			this IChessBoardModel board,
			string algebraicPosition,
			PlayerColor color,
			ChessPieceType pieceType)
		{
			if (!Enum.IsDefined(typeof(PlayerColor), color) || color == PlayerColor.None)
				throw new ArgumentException("Invalid player color.", nameof(color));

			if (!Enum.IsDefined(typeof(ChessPieceType), pieceType) || pieceType == ChessPieceType.None)
				throw new ArgumentException("Invalid piece type.", nameof(pieceType));

			var position = AlgebraicNotationUtils.FromAlgebraic(algebraicPosition);
			var piece    = new PieceModel(color, pieceType);
			board.Squares[position.File, position.Rank].TrySetPiece(piece);
			board.BoardPieces.Add(piece);
		}
	}
}
