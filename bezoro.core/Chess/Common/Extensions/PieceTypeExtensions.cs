using System;
using Bezoro.Core.Chess.Common.Enums;

namespace Bezoro.Core.Chess.Common.Extensions
{
	public static class PieceEnumExtensions
	{
		/// <summary>
		///     Gets the standard single character representation for a chess piece type.
		///     Pawn returns 'P', though in SAN it's often omitted or represented by file.
		/// </summary>
		public static char ToChar(this ChessPieceType pieceType, PlayerColor movingSide) =>
			movingSide == PlayerColor.White
				? pieceType switch
				{
					ChessPieceType.King => 'K',
					ChessPieceType.Queen => 'Q',
					ChessPieceType.Rook => 'R',
					ChessPieceType.Bishop => 'B',
					ChessPieceType.Knight => 'N',
					ChessPieceType.Pawn => 'P',
					_ => throw new ArgumentOutOfRangeException(nameof(pieceType), $"Unknown piece type: {pieceType}")
				}
				: pieceType switch
				{
					ChessPieceType.King => 'k',
					ChessPieceType.Queen => 'q',
					ChessPieceType.Rook => 'r',
					ChessPieceType.Bishop => 'b',
					ChessPieceType.Knight => 'n',
					ChessPieceType.Pawn => 'p',
					_ => throw new ArgumentOutOfRangeException(nameof(pieceType), $"Unknown piece type: {pieceType}")
				};

		/// <summary>
		///     Gets the standard single character representation for a promotion piece type.
		/// </summary>
		public static char ToChar(this PromotionPieceType pieceType) =>
			pieceType switch
			{
				PromotionPieceType.Queen  => 'Q',
				PromotionPieceType.Rook   => 'R',
				PromotionPieceType.Bishop => 'B',
				PromotionPieceType.Knight => 'N',
				_ => throw new ArgumentOutOfRangeException(
					nameof(pieceType), $"Unknown promotion piece type: {pieceType}")
			};
	}
}
