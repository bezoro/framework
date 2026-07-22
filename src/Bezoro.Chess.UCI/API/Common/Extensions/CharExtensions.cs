using Bezoro.Chess.UCI.API.Common.Enums;
using Bezoro.Chess.UCI.Domain.Common.Exceptions;
using Bezoro.Core.Extensions;

namespace Bezoro.Chess.UCI.API.Common.Extensions;

/// <summary>
///     Utilities for mapping piece characters to domain types.
/// </summary>
public static class CharExtensions
{
	/// <summary>
	///     Determines whether a piece character represents a bishop.
	/// </summary>
	/// <param name="pieceChar">The piece character to inspect.</param>
	/// <returns><see langword="true" /> when the character represents a bishop; otherwise, <see langword="false" />.</returns>
	/// <exception cref="InvalidPieceCharException"><paramref name="pieceChar" /> is not a valid piece character.</exception>
	public static bool IsBishop(this char pieceChar)
	{
		pieceChar.ThrowIfNotPieceChar();
		return UciConstants.Pieces.CHARS_BISHOP.Contains(pieceChar);
	}

	/// <summary>
	///     Determines whether a piece character belongs to the black side.
	/// </summary>
	/// <param name="pieceChar">The piece character to inspect.</param>
	/// <returns><see langword="true" /> for a lowercase black piece; otherwise, <see langword="false" />.</returns>
	/// <exception cref="InvalidPieceCharException"><paramref name="pieceChar" /> is not a valid piece character.</exception>
	public static bool IsBlack(this char pieceChar)
	{
		pieceChar.ThrowIfNotPieceChar();
		return UciConstants.Pieces.CHARS_ALL_BLACK.Contains(pieceChar);
	}

	/// <summary>
	///     Determines whether a piece character represents a king.
	/// </summary>
	/// <param name="pieceChar">The piece character to inspect.</param>
	/// <returns><see langword="true" /> when the character represents a king; otherwise, <see langword="false" />.</returns>
	/// <exception cref="InvalidPieceCharException"><paramref name="pieceChar" /> is not a valid piece character.</exception>
	public static bool IsKing(this char pieceChar)
	{
		pieceChar.ThrowIfNotPieceChar();
		return UciConstants.Pieces.CHARS_KING.Contains(pieceChar);
	}

	/// <summary>
	///     Determines whether a piece character represents a knight.
	/// </summary>
	/// <param name="pieceChar">The piece character to inspect.</param>
	/// <returns><see langword="true" /> when the character represents a knight; otherwise, <see langword="false" />.</returns>
	/// <exception cref="InvalidPieceCharException"><paramref name="pieceChar" /> is not a valid piece character.</exception>
	public static bool IsKnight(this char pieceChar)
	{
		pieceChar.ThrowIfNotPieceChar();
		return UciConstants.Pieces.CHARS_KNIGHT.Contains(pieceChar);
	}

	/// <summary>
	///     Determines whether a piece character represents a pawn.
	/// </summary>
	/// <param name="pieceChar">The piece character to inspect.</param>
	/// <returns><see langword="true" /> when the character represents a pawn; otherwise, <see langword="false" />.</returns>
	/// <exception cref="InvalidPieceCharException"><paramref name="pieceChar" /> is not a valid piece character.</exception>
	public static bool IsPawn(this char pieceChar)
	{
		pieceChar.ThrowIfNotPieceChar();
		return UciConstants.Pieces.CHARS_PAWN.Contains(pieceChar);
	}

	/// <summary>
	///     Determines whether a piece character represents a queen.
	/// </summary>
	/// <param name="pieceChar">The piece character to inspect.</param>
	/// <returns><see langword="true" /> when the character represents a queen; otherwise, <see langword="false" />.</returns>
	/// <exception cref="InvalidPieceCharException"><paramref name="pieceChar" /> is not a valid piece character.</exception>
	public static bool IsQueen(this char pieceChar)
	{
		pieceChar.ThrowIfNotPieceChar();
		return UciConstants.Pieces.CHARS_QUEEN.Contains(pieceChar);
	}

	/// <summary>
	///     Determines whether a piece character represents a rook.
	/// </summary>
	/// <param name="pieceChar">The piece character to inspect.</param>
	/// <returns><see langword="true" /> when the character represents a rook; otherwise, <see langword="false" />.</returns>
	/// <exception cref="InvalidPieceCharException"><paramref name="pieceChar" /> is not a valid piece character.</exception>
	public static bool IsRook(this char pieceChar)
	{
		pieceChar.ThrowIfNotPieceChar();
		return UciConstants.Pieces.CHARS_ROOK.Contains(pieceChar);
	}

	/// <summary>
	///     Checks if the given character is a valid chess piece letter (P/N/B/R/Q/K, any case).
	/// </summary>
	/// <param name="pieceChar">The character to inspect.</param>
	/// <returns><see langword="true" /> when the character is a recognized piece letter; otherwise, <see langword="false" />.</returns>
	public static bool IsValidPieceChar(this char pieceChar) => UciConstants.Pieces.CHARS_ALL.Contains(pieceChar);

	/// <summary>
	///     Determines whether a character is a valid promotion designator.
	/// </summary>
	/// <param name="pieceChar">The character to inspect.</param>
	/// <returns><see langword="true" /> for a knight, bishop, rook, or queen designator; otherwise, <see langword="false" />.</returns>
	public static bool IsValidPromotionChar(this char pieceChar) =>
		UciConstants.Pieces.CHARS_ALL_PROMOTION.Contains(pieceChar);

	/// <summary>
	///     Determines whether a piece character belongs to the white side.
	/// </summary>
	/// <param name="pieceChar">The piece character to inspect.</param>
	/// <returns><see langword="true" /> for an uppercase white piece; otherwise, <see langword="false" />.</returns>
	/// <exception cref="InvalidPieceCharException"><paramref name="pieceChar" /> is not a valid piece character.</exception>
	public static bool IsWhite(this char pieceChar)
	{
		pieceChar.ThrowIfNotPieceChar();
		return UciConstants.Pieces.CHARS_ALL_WHITE.Contains(pieceChar);
	}

	/// <summary>
	///     Throws an exception if the character is not a valid chess piece character.
	/// </summary>
	/// <param name="pieceChar">The character to validate.</param>
	/// <returns>The input character to enable method chaining.</returns>
	/// <exception cref="InvalidPieceCharException">Thrown when the character is not a valid chess piece character.</exception>
	public static char ThrowIfNotPieceChar(this char pieceChar)
	{
		pieceChar.ThrowIfNull().ThrowIfEmpty().ThrowIfNumber().ThrowIfSymbol()
				 .ThrowIf(c => !UciConstants.Pieces.CHARS_ALL.Contains(c));

		return pieceChar;
	}

	/// <summary>
	///     Converts a piece character (e.g. 'P', 'n') to a <see cref="PieceType" />.
	/// </summary>
	/// <param name="pieceChar">The piece character.</param>
	/// <returns>The corresponding <see cref="PieceType" />.</returns>
	/// <exception cref="ArgumentException">Thrown when <paramref name="pieceChar" /> is not a letter.</exception>
	/// <exception cref="InvalidPieceCharException">
	///     Thrown when <paramref name="pieceChar" /> is not one of P, N, B, R, Q, K
	///     (case-insensitive).
	/// </exception>
	public static PieceType ToPieceType(this char pieceChar)
	{
		pieceChar.ThrowIfNotPieceChar();

		var c = char.ToLowerInvariant(pieceChar);
		return c switch
		{
			'p' => PieceType.Pawn,
			'n' => PieceType.Knight,
			'b' => PieceType.Bishop,
			'r' => PieceType.Rook,
			'q' => PieceType.Queen,
			'k' => PieceType.King,
			_   => throw new InvalidPieceCharException(pieceChar)
		};
	}
}
