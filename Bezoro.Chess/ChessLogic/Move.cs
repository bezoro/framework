namespace Bezoro.Chess.ChessLogic
{
	/// <summary>
	///     Represents a single move from a starting position to an end position.
	/// </summary>
	public class Move
	{
		public Move(Position from, Position to, MoveType type = MoveType.Normal)
		{
			From = from;
			To   = to;
			Type = type;
		}

		public MoveType Type { get; }
		public Position From { get; }
		public Position To   { get; }

		public override string ToString() => $"Move {From} -> {To} ({Type})";
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
