using Bezoro.Chess.API.Shared.Enums;

namespace Bezoro.Chess.Domain.Extensions
{
	internal static class PieceColorMapping
	{
		public static PieceColor ToAPI(this Types.Structs.PieceColor p) => p switch
		{
			Types.Structs.PieceColor.White => PieceColor.White,
			Types.Structs.PieceColor.Black => PieceColor.Black,
			_                              => PieceColor.None
		};

		public static Types.Structs.PieceColor ToDomain(this PieceColor p) => p switch
		{
			PieceColor.White => Types.Structs.PieceColor.White,
			PieceColor.Black => Types.Structs.PieceColor.Black,
			_                => Types.Structs.PieceColor.None
		};
	}
}
