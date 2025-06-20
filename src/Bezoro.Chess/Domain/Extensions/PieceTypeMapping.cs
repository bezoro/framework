using Bezoro.Chess.API.Shared.Enums;

namespace Bezoro.Chess.Domain.Extensions
{
	internal static class PieceTypeMapping
	{
		public static PieceType ToAPI(this Types.Structs.PieceType p) => p switch
		{
			Types.Structs.PieceType.Pawn   => PieceType.Pawn,
			Types.Structs.PieceType.Knight => PieceType.Knight,
			Types.Structs.PieceType.Bishop => PieceType.Bishop,
			Types.Structs.PieceType.Rook   => PieceType.Rook,
			Types.Structs.PieceType.Queen  => PieceType.Queen,
			Types.Structs.PieceType.King   => PieceType.King,
			_                              => PieceType.None
		};

		public static Types.Structs.PieceType ToDomain(this PieceType p) => p switch
		{
			PieceType.Pawn   => Types.Structs.PieceType.Pawn,
			PieceType.Knight => Types.Structs.PieceType.Knight,
			PieceType.Bishop => Types.Structs.PieceType.Bishop,
			PieceType.Rook   => Types.Structs.PieceType.Rook,
			PieceType.Queen  => Types.Structs.PieceType.Queen,
			PieceType.King   => Types.Structs.PieceType.King,
			_                => Types.Structs.PieceType.None
		};
	}
}
