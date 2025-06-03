namespace Bezoro.Core.Chess.Utils
{
	public static class PieceModelExtensions
	{
		public static void SetAtSquare(this IChessPieceModel pieceToSet, IChessBoardSquareModel targetSquare)
		{
			pieceToSet.Square   = targetSquare;
			pieceToSet.Position = targetSquare.Position;
			targetSquare.Piece  = pieceToSet;
		}
	}
}
