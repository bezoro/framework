namespace Bezoro.Chess.UCI.Protocol.API.Types;

/// <summary>
///     Canonical move payload for protocol-side match events.
/// </summary>
/// <param name="PlyIndex">Zero-based ply index for the move within the current match.</param>
/// <param name="MoveNumber">One-based human-readable move number.</param>
/// <param name="Side">Moving side: <c>w</c> or <c>b</c>.</param>
/// <param name="Notation">Move notation in lowercase UCI form.</param>
/// <param name="From">Source square.</param>
/// <param name="To">Destination square.</param>
/// <param name="MovingPiece">Moved piece.</param>
/// <param name="CapturedPiece">Captured piece when applicable.</param>
/// <param name="PromotionPiece">Chosen promotion piece when applicable.</param>
/// <param name="SecondaryMove">Secondary piece motion when applicable, such as castling rook movement.</param>
/// <param name="Classification">Resolved move classification.</param>
/// <param name="PreviousFen">Parent position before the move.</param>
/// <param name="ResultingFen">Resulting position after the move.</param>
public readonly record struct PlayableMatchMoveData(
	int                        PlyIndex,
	int                        MoveNumber,
	char                       Side,
	string                     Notation,
	string                     From,
	string                     To,
	PlayableMatchPiece         MovingPiece,
	PlayableMatchPiece?        CapturedPiece,
	PlayableMatchPiece?        PromotionPiece,
	PlayableMatchSecondaryMove? SecondaryMove,
	MoveClassification         Classification,
	Fen                        PreviousFen,
	Fen                        ResultingFen
);
