namespace Bezoro.Chess.UCI.Protocol.API.Common.Extensions;

/// <summary>
///     Extension methods for resolving played moves back to their parent-position evaluations.
/// </summary>
public static class PlayedMoveExtensions
{
	/// <summary>
	///     Resolves the played move's score from its parent position analysis.
	/// </summary>
	/// <param name="move">Played move to resolve.</param>
	/// <param name="resolveAnalysis">Resolver for cached parent-position analysis keyed by position key.</param>
	/// <param name="score">Resolved score when available.</param>
	/// <returns><see langword="true" /> when the parent analysis exists and contains the move.</returns>
	public static bool TryResolveScore(
		this PlayedMove                       move,
		Func<string, PositionAnalysisResult?> resolveAnalysis,
		out PositionScore                     score)
	{
		if (resolveAnalysis is null) throw new ArgumentNullException(nameof(resolveAnalysis));

		if (resolveAnalysis(move.ParentPositionKey) is not { } analysis)
		{
			score = default;
			return false;
		}

		foreach (var evaluation in analysis.Evaluations)
		{
			if (!string.Equals(evaluation.Move, move.Move, StringComparison.Ordinal))
				continue;

			score = evaluation.Score;
			return true;
		}

		score = default;
		return false;
	}
}
