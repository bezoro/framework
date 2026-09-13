using System.Linq;
using Bezoro.Chess.UCI.API.Common.Extensions;
using Bezoro.Core.Extensions;

namespace Bezoro.Chess.UCI.API.Types;

/// <summary>
///     Represents normalized coordinate move notation and its optional piece designators.
/// </summary>
public readonly record struct ParsedMove
{
	private ParsedMove(Piece movingPiece, Piece? promotionPiece, string from, string to, string notation, string raw)
	{
		MovingPiece    = movingPiece;
		PromotionPiece = promotionPiece;
		From           = from;
		To             = to;
		Notation       = notation;
		Raw            = raw;
	}

	/// <summary>Gets whether the notation contains a promotion piece.</summary>
	public bool IsPromotion => PromotionPiece != null;

	/// <summary>Gets the explicit moving piece, or the default piece when notation omits it.</summary>
	public Piece MovingPiece { get; }

	/// <summary>Gets the promoted piece, when present.</summary>
	public Piece? PromotionPiece { get; }

	/// <summary>Gets the source square.</summary>
	public string From           { get; }

	/// <summary>Gets normalized coordinate notation without an explicit piece designator.</summary>
	public string Notation       { get; }

	/// <summary>Gets the original notation.</summary>
	public string Raw            { get; }

	/// <summary>Gets the destination square.</summary>
	public string To             { get; }

	/// <summary>
	///     Parses coordinate move notation containing four or five characters, with an optional leading piece designator.
	/// </summary>
	/// <param name="moveNotation">The notation to parse.</param>
	/// <returns>The parsed move.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="moveNotation" /> is <see langword="null" />.</exception>
	/// <exception cref="ArgumentOutOfRangeException"><paramref name="moveNotation" /> has an unsupported length.</exception>
	public static ParsedMove FromNotation(string moveNotation)
	{
		moveNotation.ThrowIfNull().Length.ThrowIfLessThan(4).ThrowIfMoreThan(5);

		string raw            = moveNotation;
		string notation       = string.Empty, from = string.Empty, to = string.Empty;
		Piece  movingPiece    = default;
		Piece? promotionPiece = null;

		char promotionChar = moveNotation.Last();
		if (promotionChar.IsValidPromotionChar())
		{
			promotionPiece = Piece.FromChar(promotionChar);
			int removeIndex = raw.IndexOf(promotionChar);
			moveNotation = moveNotation.Remove(removeIndex);
		}

		char pieceChar = moveNotation.First();

		if (moveNotation.Length > 4)
			if (pieceChar.IsValidPieceChar())
			{
				movingPiece  = Piece.FromChar(pieceChar);
				moveNotation = moveNotation[1..];
			}

		from = moveNotation[..2];
		to   = moveNotation[2..];

		return new(movingPiece, promotionPiece, from, to, moveNotation, raw);
	}
}
