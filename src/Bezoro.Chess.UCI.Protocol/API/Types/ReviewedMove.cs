namespace Bezoro.Chess.UCI.Protocol.API.Types;

/// <summary>An immutable played-move review. Both scores and expected-points values use the mover's perspective.</summary>
/// <param name="MoveIndex">One-based halfmove index relative to the supplied initial FEN.</param>
/// <param name="Side">Side that played the move.</param>
/// <param name="Move">Normalized UCI notation.</param>
/// <param name="Before">Position evaluation before the move.</param>
/// <param name="After">Position evaluation after the move, whose underlying engine score uses its own side to move.</param>
/// <param name="ScoreBefore">Before score normalized to the mover.</param>
/// <param name="ScoreAfter">After score normalized to the mover. Mate zero denotes a finished position; use After.Result.Winner to identify the winner.</param>
/// <param name="CentipawnLoss">Nonnegative centipawn loss, absent if either position uses a mate score.</param>
/// <param name="ExpectedPointsBefore">Expected points for the mover before playing.</param>
/// <param name="ExpectedPointsAfter">Expected points for the mover after playing.</param>
/// <param name="ExpectedPointsLoss">Nonnegative expected-points loss rounded to twelve decimal places, clamped to zero for the engine's preferred move.</param>
/// <param name="Quality">Classification derived from expected-points loss.</param>
/// <param name="Classification">Independent structural and tactical chess-rule flags.</param>
public readonly record struct ReviewedMove(
	int MoveIndex,
	char Side,
	string Move,
	GameReviewEvaluation Before,
	GameReviewEvaluation After,
	PositionScore ScoreBefore,
	PositionScore ScoreAfter,
	long? CentipawnLoss,
	double ExpectedPointsBefore,
	double ExpectedPointsAfter,
	double ExpectedPointsLoss,
	MoveQuality Quality,
	MoveClassification Classification)
{
	/// <summary>Gets an optional application-provided special annotation. No proprietary heuristic is inferred.</summary>
	public MoveReviewAnnotation Annotation { get; init; }
}

