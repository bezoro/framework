using Bezoro.Chess.Domain.Shared.Enums;

namespace Bezoro.Chess.Domain.Extensions
{
	internal static class PieceColorExtensions
	{
		public static PieceColor Opposite(this PieceColor color) =>
			color == PieceColor.White ? PieceColor.Black : PieceColor.White;
	}
}
