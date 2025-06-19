using Bezoro.Chess.Domain.Board;
using Bezoro.Chess.Domain.Moves;

public readonly struct MoveResult
{
	public static MoveResult Failed(FailureReason reason) =>
		new(reason);

	public static MoveResult Succeeded(in Move move) =>
		new(move);

	public bool IsCapture   => CapturedPieceType != PieceType.None;
	public bool IsCastle    => Type is MoveType.CastleKingside or MoveType.CastleQueenside;
	public bool IsPromotion => PromotionPieceType != PromotionType.None;
	public bool IsQuiet     => Type               == MoveType.Normal;
	public bool IsValid     => Type               != MoveType.None;
	public bool Success     => Failure            == FailureReason.None;

	public FailureReason Failure            { get; }
	public MoveType      Type               { get; }
	public PieceType     CapturedPieceType  { get; }
	public PieceType     MovingPieceType    { get; }
	public Position      From               { get; }
	public Position      To                 { get; }
	public PromotionType PromotionPieceType { get; }

	private MoveResult(in Move m)
	{
		Failure            = FailureReason.None;
		Type               = m.Type;
		MovingPieceType    = m.Piece.Type;
		From               = m.From;
		To                 = m.To;
		CapturedPieceType  = m.CapturedPiece.Type;
		PromotionPieceType = m.PromotionPieceType;
	}

	private MoveResult(FailureReason failure)
	{
		Failure            = failure;
		Type               = default;
		MovingPieceType    = default;
		From               = default;
		To                 = default;
		CapturedPieceType  = default;
		PromotionPieceType = default;
	}

	public enum FailureReason
	{
		None,
		InvalidMove, InvalidPosition, InvalidCastling, InvalidPromotion, InvalidEnPassant, KingInCheck
	}
}
