using Bezoro.Chess.Domain.Board;

namespace Bezoro.Chess.Domain.Moves
{
	/// <summary>
	///     Represents a single move from a starting position to an end position.
	/// </summary>
	public class Move
	{
		public Move(Position from, Position to, PieceColor color, MoveType type = MoveType.Normal)
		{
			From  = from;
			To    = to;
			Type  = type;
			Color = color;
		}

		public Move(
			Position from, Position to, PieceColor color, MoveType type, PieceType captured,
			PromotionType promo) : this(
			from, to, color, type)
		{
			CapturedPiece  = captured;
			PromotionPiece = promo;
		}

		public MoveType      Type           { get; }
		public PieceColor    Color          { get; }
		public PieceType     CapturedPiece  { get; } = PieceType.None;
		public Position      From           { get; }
		public Position      To             { get; }
		public PromotionType PromotionPiece { get; } = PromotionType.None;

		public override string ToString() => $"Move {From} -> {To} ({Type})";

		public Move CapturePromotion(
			PieceColor color, Position from, Position to, PieceType captured,
			PromotionType promo = PromotionType.Queen) =>
			new(from, to, color, MoveType.Capture, captured, promo);

		public Move QuietPromotion(
			PieceColor color, Position from, Position to,
			PromotionType promo = PromotionType.Queen) =>
			new(from, to, color, MoveType.Normal, PieceType.None, promo);
	}

	/// <summary>
	///     Enumerates the types of possible moves in chess.
	/// </summary>
	public enum MoveType
	{
		Normal,
		Capture,
		CastleKingside,
		CastleQueenside,
		EnPassant,
		PawnPromotion,
		PawnPromotionCapture
	}
}
