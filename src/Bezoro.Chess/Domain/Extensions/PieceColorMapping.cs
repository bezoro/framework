using Bezoro.Chess.API.Shared.Enums;

namespace Bezoro.Chess.Domain.Extensions
{
	internal static class PieceColorMapping
	{
		public static PieceColor ToAPI(this Shared.Enums.PieceColor p) => p switch
		{
			Shared.Enums.PieceColor.White => PieceColor.White,
			Shared.Enums.PieceColor.Black => PieceColor.Black,
			_                             => PieceColor.None
		};

		public static Shared.Enums.PieceColor ToDomain(this PieceColor p) => p switch
		{
			PieceColor.White => Shared.Enums.PieceColor.White,
			PieceColor.Black => Shared.Enums.PieceColor.Black,
			_                => Shared.Enums.PieceColor.None
		};
	}
}
