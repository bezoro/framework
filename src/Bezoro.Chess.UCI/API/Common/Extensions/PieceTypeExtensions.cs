using Bezoro.Chess.UCI.API.Common.Enums;

namespace Bezoro.Chess.UCI.API.Common.Extensions;

public static class PieceTypeExtensions
{
	/// <summary>
	///     Maps a <see cref="PieceType" /> and <see cref="PieceColor" /> to its character representation.
	///     Uppercase denotes white; lowercase denotes black.
	/// </summary>
	/// <param name="type">Piece type (must not be <see cref="PieceType.Empty" />).</param>
	/// <param name="color">Piece color.</param>
	/// <returns>Piece character (P/N/B/R/Q/K in the appropriate case).</returns>
	/// <exception cref="ArgumentOutOfRangeException">If <paramref name="type" /> is <see cref="PieceType.Empty" /> or unknown.</exception>
	public static char ToChar(this PieceType type, PieceColor color)
	{
		char baseChar = type switch
		{
			PieceType.Pawn   => 'p',
			PieceType.Knight => 'n',
			PieceType.Bishop => 'b',
			PieceType.Rook   => 'r',
			PieceType.Queen  => 'q',
			PieceType.King   => 'k',
			_ => throw new ArgumentOutOfRangeException(
					 nameof(type),
					 "Cannot convert PieceType.Empty or unknown type to a character."
				 )
		};

		return color == PieceColor.White ? char.ToUpperInvariant(baseChar) : baseChar;
	}
}
