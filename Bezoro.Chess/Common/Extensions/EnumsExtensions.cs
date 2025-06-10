using System;
using System.Runtime.CompilerServices;
using Bezoro.Chess.Common.Enums;

namespace Bezoro.Chess.Common.Extensions
{
	public static class EnumsExtensions
	{
		/// <summary>
		///     Converts a ChessPieceType enum to its lowercase FEN character representation.
		/// </summary>
		/// <param name="pieceType">The chess piece type.</param>
		/// <returns>The lowercase FEN character for the piece type.</returns>
		/// <exception cref="ArgumentException">Thrown for None piece type.</exception>
		/// <exception cref="ArgumentOutOfRangeException">Thrown for unknown piece types.</exception>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static char ToFenChar(this ChessPieceType pieceType) =>
			pieceType switch
			{
				ChessPieceType.Pawn   => 'p',
				ChessPieceType.Knight => 'n',
				ChessPieceType.Bishop => 'b',
				ChessPieceType.Rook   => 'r',
				ChessPieceType.Queen  => 'q',
				ChessPieceType.King   => 'k',
				ChessPieceType.None => throw new ArgumentException(
					"Cannot get FEN char for None piece type.", nameof(pieceType)),
				_ => throw new ArgumentOutOfRangeException(nameof(pieceType), $"Unknown piece type: {pieceType}")
			};

		public static PlayerColor Opposite(this PlayerColor color) =>
			color == PlayerColor.White ? PlayerColor.Black : PlayerColor.White;
	}
}
