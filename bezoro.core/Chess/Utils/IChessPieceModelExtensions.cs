using System;

namespace Bezoro.Core.Chess.Utils
{
	public static class IChessPieceModelExtensions
	{
		public static void MoveTo(this IChessPieceModel pieceToMove, IChessBoardSquareModel targetSquare)
		{
			if (pieceToMove == null)
				throw new ArgumentNullException(nameof(pieceToMove));

			if (targetSquare == null)
				throw new ArgumentNullException(nameof(targetSquare));

			pieceToMove.RemoveFromBoard();
			pieceToMove.SetAtSquare(targetSquare);
		}

		public static void SetAtSquare(this IChessPieceModel pieceToSet, IChessBoardSquareModel targetSquare)
		{
			pieceToSet.Square = targetSquare;
			pieceToSet.Position = targetSquare.Position;
			targetSquare.Piece = pieceToSet;
		}
		public static void RemoveFromBoard(this IChessPieceModel pieceToRemove)
		{
			pieceToRemove.Square?.TryRemovePiece(pieceToRemove);
		}
	}
}
