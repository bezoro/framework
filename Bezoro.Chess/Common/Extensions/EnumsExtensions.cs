using Bezoro.Chess.Common.Enums;

namespace Bezoro.Chess.Common.Extensions
{
	public static class EnumsExtensions
	{
		public static PlayerColor Opposite(this PlayerColor color) =>
			color == PlayerColor.White ? PlayerColor.Black : PlayerColor.White;
	}
}
