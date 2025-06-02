using System;

namespace Bezoro.Core.Chess.Utils
{
	public static class ChessBoardModelExtensions
	{
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
			var piece    = new ChessPieceModel(color, pieceType);
			board.Squares[position.File, position.Rank].TrySetPiece(piece);
			board.BoardPieces.Add(piece);
		}
	}
}
