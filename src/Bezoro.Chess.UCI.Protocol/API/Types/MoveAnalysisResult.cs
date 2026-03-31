using System.Collections.Immutable;

namespace Bezoro.Chess.UCI.Protocol.API.Types;

/// <summary>
///     Represents analyzed legal moves for a single position.
/// </summary>
/// <param name="Evaluations">Move evaluations sorted by descending preference.</param>
public readonly record struct MoveAnalysisResult(ImmutableArray<MoveEvaluation> Evaluations)
{
	/// <summary>
	///     Gets an empty move-analysis result.
	/// </summary>
	public static MoveAnalysisResult Empty => new([]);
}
