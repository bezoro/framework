using System;

namespace Bezoro.Core.Chess.Utils
{
	public static class ChessPieceModelExtensions
	{
		public static void SetAtSquare(this IChessPieceModel pieceToSet, IChessBoardSquareModel targetSquare)
		{
			pieceToSet.Square   = targetSquare;
			pieceToSet.Position = targetSquare.Position;
			targetSquare.Piece  = pieceToSet;
		}
	}
}
