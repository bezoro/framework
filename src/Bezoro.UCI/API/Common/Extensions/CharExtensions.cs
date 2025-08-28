using Bezoro.Core.Common.Extensions;
using Bezoro.UCI.API.Common.Enums;
using Bezoro.UCI.API.Common.Exceptions;
using Bezoro.UCI.Domain.Common.Constants;

namespace Bezoro.UCI.API.Common.Extensions;

/// <summary>
///     Utilities for mapping piece characters to domain types.
/// </summary>
public static class CharExtensions
{
	public static bool IsBishop(this char pieceChar)
	{
		pieceChar.ThrowIfNotPieceChar();
		return char.ToLowerInvariant(pieceChar) == 'b';
	}

	public static bool IsBlack(this char pieceChar)
	{
		pieceChar.ThrowIfNotPieceChar();
		return char.IsLower(pieceChar);
	}

	public static bool IsKing(this char pieceChar)
	{
		pieceChar.ThrowIfNotPieceChar();
		return char.ToLowerInvariant(pieceChar) == 'k';
	}

	public static bool IsKnight(this char pieceChar)
	{
		pieceChar.ThrowIfNotPieceChar();
		return char.ToLowerInvariant(pieceChar) == 'n';
	}

	public static bool IsPawn(this char pieceChar)
	{
		pieceChar.ThrowIfNotPieceChar();
		return char.ToLowerInvariant(pieceChar) == 'p';
	}

	public static bool IsQueen(this char pieceChar)
	{
		pieceChar.ThrowIfNotPieceChar();
		return char.ToLowerInvariant(pieceChar) == 'q';
	}

	public static bool IsRook(this char pieceChar)
	{
		pieceChar.ThrowIfNotPieceChar();
		return char.ToLowerInvariant(pieceChar) == 'r';
	}

	/// <summary>
	///     Checks if the given character is a valid chess piece letter (P/N/B/R/Q/K, any case).
	/// </summary>
	public static bool IsValidPieceChar(this char pieceChar) => UciConstants.Pieces.CHARS.Contains(pieceChar);

	public static bool IsValidPromotionChar(this char pieceChar) =>
		UciConstants.Pieces.CHARS_PROMOTION.Contains(pieceChar);

	public static bool IsWhite(this char pieceChar)
	{
		pieceChar.ThrowIfNotPieceChar();
		return char.IsUpper(pieceChar);
	}

	/// <summary>
	///     Throws an exception if the character is not a valid chess piece character.
	/// </summary>
	/// <param name="pieceChar">The character to validate.</param>
	/// <returns>The input character to enable method chaining.</returns>
	/// <exception cref="InvalidPieceCharException">Thrown when the character is not a valid chess piece character.</exception>
	public static char ThrowIfNotPieceChar(this char pieceChar)
	{
		pieceChar.ThrowIfNull().ThrowIfEmpty().ThrowIfNumber().ThrowIfSymbol();
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
