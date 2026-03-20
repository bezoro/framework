using Bezoro.Chess.UCI.API.Common.Enums;
using Bezoro.Chess.UCI.API.Common.Extensions;

namespace Bezoro.Chess.UCI.API.Types;

/// <summary>
///     Immutable value type representing a chess piece with its symbol, type and color.
/// </summary>
public readonly record struct Piece
{
	private Piece(char pieceChar, PieceColor color, PieceType pieceType)
	{
		Type  = pieceType;
		Char  = pieceChar;
		Color = color;
	}

	/// <summary>True if the piece is black.</summary>
	public bool IsBlack => Color == PieceColor.Black;

	/// <summary>True if the piece is white.</summary>
	public bool IsWhite => Color == PieceColor.White;

	/// <summary>The original character representation of the piece (e.g. 'P' for white pawn, 'n' for black knight).</summary>
	public char Char { get; }

	/// <summary>The piece color inferred from character casing (uppercase = white, lowercase = black).</summary>
	public PieceColor Color { get; }

	/// <summary>The type of piece (pawn, knight, bishop, etc.).</summary>
	public PieceType Type { get; }

	/// <summary>
	///     Creates a new Piece instance from a character representation.
	/// </summary>
	/// <param name="pieceChar">Character representing the piece (e.g. 'P' for white pawn, 'n' for black knight)</param>
	/// <returns>A new Piece instance</returns>
	/// <exception cref="ArgumentException">Thrown when pieceChar is not a valid piece character</exception>
	public static Piece FromChar(char pieceChar)
	{
		pieceChar.ThrowIfNotPieceChar();
		var type  = pieceChar.ToPieceType();
		var color = char.IsUpper(pieceChar) ? PieceColor.White : PieceColor.Black;
		return new(pieceChar, color, type);
	}
}
