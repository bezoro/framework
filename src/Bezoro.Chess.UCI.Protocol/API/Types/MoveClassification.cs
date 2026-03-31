namespace Bezoro.Chess.UCI.Protocol.API.Types;

/// <summary>
///     Describes structural move semantics immediately and tactical check/mate/stalemate flags once resolved.
/// </summary>
/// <param name="Flags">Classification flags for the move.</param>
/// <param name="MovingPiece">Moving piece character from the source square.</param>
/// <param name="CapturedPiece">Captured piece character, when applicable.</param>
/// <param name="PromotionPiece">Promotion piece character, when applicable.</param>
/// <param name="IsResolved">Whether tactical flags have been resolved for this move.</param>
public readonly record struct MoveClassification(
	MoveClassificationFlags Flags,
	char                    MovingPiece,
	char?                   CapturedPiece,
	char?                   PromotionPiece,
	bool                    IsResolved)
{
	/// <summary>
	///     Gets an unknown unresolved classification.
	/// </summary>
	public static MoveClassification Unknown() => new(MoveClassificationFlags.None, '\0', null, null, false);

	/// <summary>
	///     Creates a structural move classification with unresolved tactical flags.
	/// </summary>
	public static MoveClassification CreateStructural(
		MoveClassificationFlags flags,
		char                    movingPiece,
		char?                   capturedPiece  = null,
		char?                   promotionPiece = null) =>
		new(flags, movingPiece, capturedPiece, promotionPiece, false);

	/// <summary>
	///     Returns a copy with resolved tactical check, mate, and stalemate flags.
	/// </summary>
	public MoveClassification WithTacticalOutcome(bool isCheck, bool isMate, bool isStalemate)
	{
		var flags = Flags;
		flags = isCheck ? flags | MoveClassificationFlags.Check : flags & ~MoveClassificationFlags.Check;
		flags = isMate ? flags | MoveClassificationFlags.Mate : flags & ~MoveClassificationFlags.Mate;
		flags = isStalemate ? flags | MoveClassificationFlags.Stalemate : flags & ~MoveClassificationFlags.Stalemate;
		return new(flags, MovingPiece, CapturedPiece, PromotionPiece, true);
	}

	/// <summary>Gets whether the move is a normal move.</summary>
	public bool IsNormal => Flags.HasFlag(MoveClassificationFlags.Normal);

	/// <summary>Gets whether the move is a capture.</summary>
	public bool IsCapture => Flags.HasFlag(MoveClassificationFlags.Capture);

	/// <summary>Gets whether the move is en passant.</summary>
	public bool IsEnPassant => Flags.HasFlag(MoveClassificationFlags.EnPassant);

	/// <summary>Gets whether the move is a promotion.</summary>
	public bool IsPromotion => Flags.HasFlag(MoveClassificationFlags.Promotion);

	/// <summary>Gets whether the move is any form of castling.</summary>
	public bool IsCastling => IsKingsideCastling || IsQueensideCastling;

	/// <summary>Gets whether the move is kingside castling.</summary>
	public bool IsKingsideCastling => Flags.HasFlag(MoveClassificationFlags.KingsideCastling);

	/// <summary>Gets whether the move is queenside castling.</summary>
	public bool IsQueensideCastling => Flags.HasFlag(MoveClassificationFlags.QueensideCastling);

	/// <summary>Gets whether the move is a two-square pawn push.</summary>
	public bool IsDoublePawnPush => Flags.HasFlag(MoveClassificationFlags.DoublePawnPush);

	/// <summary>Gets whether the move gives check.</summary>
	public bool IsCheck => Flags.HasFlag(MoveClassificationFlags.Check);

	/// <summary>Gets whether the move gives mate.</summary>
	public bool IsMate => Flags.HasFlag(MoveClassificationFlags.Mate);

	/// <summary>Gets whether the move stalemates the opponent.</summary>
	public bool IsStalemate => Flags.HasFlag(MoveClassificationFlags.Stalemate);
}
