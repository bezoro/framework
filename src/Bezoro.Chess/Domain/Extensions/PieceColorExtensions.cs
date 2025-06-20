using Bezoro.Chess.Domain.Types.Structs;

namespace Bezoro.Chess.Domain.Extensions
{
	public static class PieceColorExtensions
	{
		public static PieceColor Opposite(this PieceColor color) =>
			color == PieceColor.White ? PieceColor.Black : PieceColor.White;
	}
}
