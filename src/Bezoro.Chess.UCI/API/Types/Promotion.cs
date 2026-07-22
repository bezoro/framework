using System.Linq;
using Bezoro.Chess.UCI.API.Common.Enums;
using Bezoro.Chess.UCI.API.Common.Extensions;
using Bezoro.Core.Extensions;

namespace Bezoro.Chess.UCI.API.Types;

/// <summary>
///     Represents a pawn promotion choice and destination.
/// </summary>
public readonly record struct Promotion
{
	private Promotion(PieceType pieceType, Position position)
	{
		PieceType = pieceType;
		Position  = position;
	}

	/// <summary>Gets the piece type selected for promotion.</summary>
	public PieceType PieceType { get; }

	/// <summary>Gets the destination occupied by the promoting pawn.</summary>
	public Position Position { get; }

	/// <summary>Parses a promotion from coordinate move notation.</summary>
	/// <param name="moveNotation">Promotion notation ending in the chosen piece character.</param>
	/// <returns>The parsed promotion.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="moveNotation" /> is <see langword="null" />.</exception>
	/// <exception cref="ArgumentOutOfRangeException"><paramref name="moveNotation" /> has an unsupported length.</exception>
	public static Promotion FromNotation(string moveNotation)
	{
		moveNotation.ThrowIfNull().Length.ThrowIfLessThan(4).ThrowIfMoreThan(5);
		var  parsedMove  = ParsedMove.FromNotation(moveNotation);
		var  color       = DetermineColor(parsedMove);
		char pawnChar    = color == PieceColor.White ? 'P' : 'p';
		var  position    = Position.Create(parsedMove.To, Piece.FromChar(pawnChar));
		var  chosenPiece = moveNotation.Last().ToPieceType();
		return new(chosenPiece, position);
	}

	private static PieceColor DetermineColor(ParsedMove move)
	{
		if (move.From.Length < 2 || move.To.Length < 2)
			return PieceColor.White;

		char fromRank = move.From[1];
		char toRank   = move.To[1];

		// Promotions always occur on the last rank for the mover.
		return toRank > fromRank ? PieceColor.White : PieceColor.Black;
	}
}
