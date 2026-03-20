using System.Linq;
using Bezoro.Core.Extensions;
using Bezoro.Chess.UCI.API.Common.Enums;
using Bezoro.Chess.UCI.API.Common.Extensions;

namespace Bezoro.Chess.UCI.API.Types;

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

	public bool   IsPromotion    => PromotionPiece != null;
	public Piece  MovingPiece    { get; }
	public Piece? PromotionPiece { get; }
	public string From           { get; }
	public string Notation       { get; }
	public string Raw            { get; }
	public string To             { get; }

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

public readonly record struct Promotion
{
	private Promotion(PieceType pieceType, Position position)
	{
		PieceType = pieceType;
		Position  = position;
	}

	public PieceType PieceType { get; }
	public Position  Position  { get; }

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
