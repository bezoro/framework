using System.Collections.Immutable;

namespace Bezoro.Chess.UCI.Protocol.API.Types;

/// <summary>
///     Represents a high-quality position analysis snapshot including the current advantage and evaluated legal moves.
/// </summary>
/// <param name="Advantage">Current position advantage from the player's perspective.</param>
/// <param name="Evaluations">Legal move evaluations sorted by descending preference.</param>
public readonly record struct PositionAnalysisResult(PositionAdvantage Advantage, ImmutableArray<MoveEvaluation> Evaluations)
{
	/// <summary>
	///     Gets an empty position analysis result.
	/// </summary>
	public static PositionAnalysisResult Empty => new(PositionAdvantage.Pending(), []);
}
