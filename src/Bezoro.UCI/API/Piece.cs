using Bezoro.Core.Common.Extensions;

namespace Bezoro.UCI.API;

/// <summary>
///     Exception thrown when a character does not represent a valid chess piece.
/// </summary>
/// <param name="pieceChar">The invalid piece character.</param>
public sealed class InvalidPieceCharException(char pieceChar) : Exception($"Invalid piece character: {pieceChar}");

/// <summary>
///     Utilities for mapping piece characters to domain types.
/// </summary>
public static class PieceExtensions
{
	/// <summary>
	///     Checks if the given character is a valid chess piece letter (P/N/B/R/Q/K, any case).
	/// </summary>
	public static bool IsValidPieceChar(this char pieceChar) => pieceChar.TryToPieceType(out _);

	/// <summary>
	///     Tries to convert a character to <see cref="PieceType" /> without throwing.
	/// </summary>
	/// <param name="pieceChar">Piece character.</param>
	/// <param name="type">Resulting piece type on success.</param>
	/// <returns>true if mapping succeeded; otherwise false.</returns>
	public static bool TryToPieceType(this char pieceChar, out PieceType type)
	{
		type = PieceType.Empty;

		if (!char.IsLetter(pieceChar))
			return false;

		switch (char.ToLowerInvariant(pieceChar))
		{
			case 'p':
				type = PieceType.Pawn;
				return true;
			case 'n':
				type = PieceType.Knight;
				return true;
			case 'b':
				type = PieceType.Bishop;
				return true;
			case 'r':
				type = PieceType.Rook;
				return true;
			case 'q':
				type = PieceType.Queen;
				return true;
			case 'k':
				type = PieceType.King;
				return true;
			default: return false;
		}
	}

	/// <summary>
	///     Maps a <see cref="PieceType" /> and <see cref="PlayerColor" /> to its character representation.
	///     Uppercase denotes white; lowercase denotes black.
	/// </summary>
	/// <param name="type">Piece type (must not be <see cref="PieceType.Empty" />).</param>
	/// <param name="color">Piece color.</param>
	/// <returns>Piece character (P/N/B/R/Q/K in the appropriate case).</returns>
	/// <exception cref="ArgumentOutOfRangeException">If <paramref name="type" /> is <see cref="PieceType.Empty" /> or unknown.</exception>
	public static char ToChar(this PieceType type, PlayerColor color)
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
					 "Cannot convert PieceType.Empty or unknown type to a character.")
		};

		return color == PlayerColor.White ? char.ToUpperInvariant(baseChar) : baseChar;
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
		pieceChar.ThrowIf(c => !char.IsLetter(c));

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

/// <summary>
///     Chess piece type.
/// </summary>
public enum PieceType
{
	/// <summary>No piece (empty square).</summary>
	Empty,
	Pawn, Knight, Bishop, Rook, Queen, King
}

/// <summary>
///     Immutable value type representing a chess piece with its symbol, type and color.
/// </summary>
public readonly record struct Piece
{
	/// <summary>
	///     Initializes a new instance of <see cref="Piece" /> from a piece character.
	///     Uppercase denotes white, lowercase denotes black.
	/// </summary>
	/// <param name="pieceChar">Piece character: P/N/B/R/Q/K (case-insensitive).</param>
	/// <exception cref="InvalidPieceCharException">If the character is not a valid piece.</exception>
	public Piece(char pieceChar)
	{
		Type  = pieceChar.ToPieceType();
		Char  = pieceChar;
		Color = char.IsUpper(pieceChar) ? PlayerColor.White : PlayerColor.Black;
	}

	/// <summary>
	///     Initializes a new instance of <see cref="Piece" /> from a <see cref="PieceType" /> and <see cref="PlayerColor" />.
	/// </summary>
	/// <param name="type">Piece type (must not be <see cref="PieceType.Empty" />).</param>
	/// <param name="color">Piece color.</param>
	/// <exception cref="ArgumentOutOfRangeException">If <paramref name="type" /> is <see cref="PieceType.Empty" />.</exception>
	public Piece(PieceType type, PlayerColor color)
	{
		if (type is PieceType.Empty)
		{
			throw new ArgumentOutOfRangeException(
				nameof(type),
				"PieceType.Empty cannot be represented as a concrete piece.");
		}

		Type  = type;
		Color = color;
		Char  = type.ToChar(color);
	}

	/// <summary>True if the piece is black.</summary>
	public bool IsBlack => Color == PlayerColor.Black;

	/// <summary>True if the piece is white.</summary>
	public bool IsWhite => Color == PlayerColor.White;

	/// <summary>The original piece character.</summary>
	public char Char { get; }

	/// <summary>The piece type.</summary>
	public PieceType Type { get; }

	/// <summary>The piece color inferred from character casing.</summary>
	public PlayerColor Color { get; }

	/// <summary>
	///     Tries to parse a character into a <see cref="Piece" /> without throwing.
	/// </summary>
	/// <param name="pieceChar">Character to parse.</param>
	/// <param name="piece">Resulting piece on success; default on failure.</param>
	/// <returns>true if parsing succeeded; otherwise false.</returns>
	public static bool TryParse(char pieceChar, out Piece piece)
	{
		if (!pieceChar.IsValidPieceChar())
		{
			piece = default;
			return false;
		}

		piece = new(pieceChar);
		return true;
	}

	/// <summary>
	///     Parses a character into a <see cref="Piece" /> or throws if invalid.
	///     Uppercase denotes white; lowercase denotes black.
	/// </summary>
	public static Piece Parse(char pieceChar) => new(pieceChar);
}
