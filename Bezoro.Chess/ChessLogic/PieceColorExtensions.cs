namespace Bezoro.Chess.ChessLogic
{
	public static class PieceColorExtensions
	{
		public static PieceColor Opposite(this PieceColor color) =>
			color == PieceColor.White ? PieceColor.Black : PieceColor.White;
	}
}
