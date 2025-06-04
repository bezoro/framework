using System;
using Bezoro.Core.Chess.Pieces;

namespace Bezoro.Core.Chess.Utils
{
	/// <summary>Maps concrete piece models to <see cref="ChessPieceType" />.</summary>
	internal static class PieceModelExtensions
	{
		public static ChessPieceType GetPieceType(this IChessPieceModel piece) =>
			piece switch
			{
				KingModel   => ChessPieceType.King,
				QueenModel  => ChessPieceType.Queen,
				RookModel   => ChessPieceType.Rook,
				BishopModel => ChessPieceType.Bishop,
				KnightModel => ChessPieceType.Knight,
				PawnModel   => ChessPieceType.Pawn,
				_ => throw new ArgumentOutOfRangeException(
					$"Unknown piece model: {piece.GetType().Name}")
			};
	}
}
