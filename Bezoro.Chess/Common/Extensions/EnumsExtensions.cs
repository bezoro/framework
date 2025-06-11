using System;
using System.Runtime.CompilerServices;
using Bezoro.Chess.Common.Enums;

namespace Bezoro.Chess.Common.Extensions
{
	public static class EnumsExtensions
	{
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static char FenChar(this PromotionPieceType promo, PlayerColor color) =>
			promo switch
			{
				PromotionPieceType.Queen  => color == PlayerColor.White ? 'Q' : 'q',
				PromotionPieceType.Rook   => color == PlayerColor.White ? 'R' : 'r',
				PromotionPieceType.Bishop => color == PlayerColor.White ? 'B' : 'b',
				PromotionPieceType.Knight => color == PlayerColor.White ? 'N' : 'n',
				_ => throw new ArgumentOutOfRangeException(
					nameof(promo), $"Unknown promotion piece type: {promo}")
			};

		/// <summary>
		///     Converts a ChessPieceType enum to its lowercase FEN character representation.
		/// </summary>
		/// <param name="pieceType">The chess piece type.</param>
		/// <param name="color"></param>
		/// <returns>The lowercase FEN character for the piece type.</returns>
		/// <exception cref="ArgumentException">Thrown for None piece type.</exception>
		/// <exception cref="ArgumentOutOfRangeException">Thrown for unknown piece types.</exception>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static char ToFenChar(this ChessPieceType pieceType, PlayerColor color) =>
			pieceType switch
			{
				ChessPieceType.Pawn   => color == PlayerColor.White ? 'P' : 'p',
				ChessPieceType.Knight => color == PlayerColor.White ? 'N' : 'n',
				ChessPieceType.Bishop => color == PlayerColor.White ? 'B' : 'b',
				ChessPieceType.Rook   => color == PlayerColor.White ? 'R' : 'r',
				ChessPieceType.Queen  => color == PlayerColor.White ? 'Q' : 'q',
				ChessPieceType.King   => color == PlayerColor.White ? 'K' : 'k',
				ChessPieceType.None => throw new ArgumentException(
					"Cannot get FEN char for None piece type.", nameof(pieceType)),
				_ => throw new ArgumentOutOfRangeException(nameof(pieceType), $"Unknown piece type: {pieceType}")
			};

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static PlayerColor Opposite(this PlayerColor color) =>
			color == PlayerColor.White ? PlayerColor.Black : PlayerColor.White;
	}
}
