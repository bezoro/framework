namespace Bezoro.Chess.API.ViewModels
{
	internal static class PieceColorMapping
	{
		public static PieceColor ToAPI(this Domain.Types.Structs.PieceColor p) => p switch
		{
			Domain.Types.Structs.PieceColor.White => PieceColor.White,
			Domain.Types.Structs.PieceColor.Black => PieceColor.Black,
			_                                     => PieceColor.None
		};

		public static Domain.Types.Structs.PieceColor ToDomain(this PieceColor p) => p switch
		{
			PieceColor.White => Domain.Types.Structs.PieceColor.White,
			PieceColor.Black => Domain.Types.Structs.PieceColor.Black,
			_                => Domain.Types.Structs.PieceColor.None
		};
	}
}
