using Bezoro.Chess.API.Shared.Enums;

namespace Bezoro.Chess.Domain.Extensions
{
	internal static class PieceTypeMapping
	{
		public static PieceType ToAPI(this Shared.Enums.PieceType p) => p switch
		{
			Shared.Enums.PieceType.Pawn   => PieceType.Pawn,
			Shared.Enums.PieceType.Knight => PieceType.Knight,
			Shared.Enums.PieceType.Bishop => PieceType.Bishop,
			Shared.Enums.PieceType.Rook   => PieceType.Rook,
			Shared.Enums.PieceType.Queen  => PieceType.Queen,
			Shared.Enums.PieceType.King   => PieceType.King,
			_                             => PieceType.None
		};

		public static Shared.Enums.PieceType ToDomain(this PieceType p) => p switch
		{
			PieceType.Pawn   => Shared.Enums.PieceType.Pawn,
			PieceType.Knight => Shared.Enums.PieceType.Knight,
			PieceType.Bishop => Shared.Enums.PieceType.Bishop,
			PieceType.Rook   => Shared.Enums.PieceType.Rook,
			PieceType.Queen  => Shared.Enums.PieceType.Queen,
			PieceType.King   => Shared.Enums.PieceType.King,
			_                => Shared.Enums.PieceType.None
		};
	}
}
