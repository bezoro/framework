namespace Bezoro.Chess.API.Types
{
	internal static class PieceTypeMapping
	{
		public static PieceType ToAPI(this Domain.Types.Structs.PieceType p) => p switch
		{
			Domain.Types.Structs.PieceType.Pawn   => PieceType.Pawn,
			Domain.Types.Structs.PieceType.Knight => PieceType.Knight,
			Domain.Types.Structs.PieceType.Bishop => PieceType.Bishop,
			Domain.Types.Structs.PieceType.Rook   => PieceType.Rook,
			Domain.Types.Structs.PieceType.Queen  => PieceType.Queen,
			Domain.Types.Structs.PieceType.King   => PieceType.King,
			_                                     => PieceType.None
		};

		public static Domain.Types.Structs.PieceType ToDomain(this PieceType p) => p switch
		{
			PieceType.Pawn   => Domain.Types.Structs.PieceType.Pawn,
			PieceType.Knight => Domain.Types.Structs.PieceType.Knight,
			PieceType.Bishop => Domain.Types.Structs.PieceType.Bishop,
			PieceType.Rook   => Domain.Types.Structs.PieceType.Rook,
			PieceType.Queen  => Domain.Types.Structs.PieceType.Queen,
			PieceType.King   => Domain.Types.Structs.PieceType.King,
			_                => Domain.Types.Structs.PieceType.None
		};
	}
}
