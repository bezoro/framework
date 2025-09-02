using Bezoro.Core.Common.Extensions;
using Bezoro.UCI.API.Common.Enums;
using Bezoro.UCI.Domain.Common.Constants;
using Bezoro.UCI.Domain.Common.Exceptions;

namespace Bezoro.UCI.API.Common.Extensions;

/// <summary>
///     Utilities for mapping piece characters to domain types.
/// </summary>
public static class CharExtensions
{
	public static bool IsBishop(this char pieceChar)
	{
		pieceChar.ThrowIfNotPieceChar();
		return UciConstants.Pieces.CHARS_BISHOP.Contains(pieceChar);
	}

	public static bool IsBlack(this char pieceChar)
	{
		pieceChar.ThrowIfNotPieceChar();
		return UciConstants.Pieces.CHARS_ALL_BLACK.Contains(pieceChar);
	}

	public static bool IsKing(this char pieceChar)
	{
		pieceChar.ThrowIfNotPieceChar();
		return UciConstants.Pieces.CHARS_KING.Contains(pieceChar);
	}

	public static bool IsKnight(this char pieceChar)
	{
		pieceChar.ThrowIfNotPieceChar();
		return UciConstants.Pieces.CHARS_KNIGHT.Contains(pieceChar);
	}

	public static bool IsPawn(this char pieceChar)
	{
		pieceChar.ThrowIfNotPieceChar();
		return UciConstants.Pieces.CHARS_PAWN.Contains(pieceChar);
	}

	public static bool IsQueen(this char pieceChar)
	{
		pieceChar.ThrowIfNotPieceChar();
		return UciConstants.Pieces.CHARS_QUEEN.Contains(pieceChar);
	}

	public static bool IsRook(this char pieceChar)
	{
		pieceChar.ThrowIfNotPieceChar();
		return UciConstants.Pieces.CHARS_ROOK.Contains(pieceChar);
	}

	/// <summary>
	///     Checks if the given character is a valid chess piece letter (P/N/B/R/Q/K, any case).
	/// </summary>
	public static bool IsValidPieceChar(this char pieceChar) => UciConstants.Pieces.CHARS_ALL.Contains(pieceChar);

	public static bool IsValidPromotionChar(this char pieceChar) =>
		UciConstants.Pieces.CHARS_ALL_PROMOTION.Contains(pieceChar);

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
