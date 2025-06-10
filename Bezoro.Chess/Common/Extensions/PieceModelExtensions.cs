using System;
using Bezoro.Chess.Abstractions.Interfaces;
using Bezoro.Chess.Common.Enums;
using Bezoro.Chess.Pieces.Models;

namespace Bezoro.Chess.Common.Extensions
{
	/// <summary>Maps concrete piece models to <see cref="ChessPieceType" />.</summary>
	internal static class PieceModelExtensions
	{
		public static char ToFenChar(this IChessPieceModel piece) =>
			piece.GetPieceType().ToFenChar();

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
