using Bezoro.Chess.UCI.Protocol.API.Types;

namespace Bezoro.Chess.UCI.Protocol.ConsoleDemo;

internal static class PlayedPositionEvaluationResolver
{
	public static bool TryResolveScore(
		MoveHistoryEntry                       entry,
		Func<string, PositionAnalysisResult?> resolveAnalysis,
		out PositionScore                     score)
	{
		if (resolveAnalysis(entry.ParentPositionKey) is not { } analysis)
		{
			score = default;
			return false;
		}

		foreach (var evaluation in analysis.Evaluations)
		{
			if (!string.Equals(evaluation.Move, entry.Move, StringComparison.Ordinal))
				continue;

			score = evaluation.Score;
			return true;
		}

		score = default;
		return false;
	}

	public static PositionAdvantage ResolveCurrentAdvantage(
		string                              currentPositionKey,
		IReadOnlyList<MoveHistoryEntry>     moveHistory,
		int                                 legalMoveCount,
		Func<string, PositionAnalysisResult?> resolveAnalysis)
	{
		if (legalMoveCount == 0)
			return PositionAdvantage.GameOver();

		if (moveHistory.Count == 0)
		{
			if (resolveAnalysis(currentPositionKey) is { } openingAnalysis)
				return openingAnalysis.Advantage;

			return PositionAdvantage.GameStart();
		}

		var lastMove = moveHistory[^1];
		if (string.Equals(lastMove.PositionKey, currentPositionKey, StringComparison.Ordinal) &&
			TryResolveScore(lastMove, resolveAnalysis, out var score))
		{
			return PositionAdvantage.FromScore(score);
		}

		return PositionAdvantage.Pending();
	}
}
