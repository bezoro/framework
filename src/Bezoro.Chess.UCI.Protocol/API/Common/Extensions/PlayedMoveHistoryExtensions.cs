using System.Collections.Generic;

namespace Bezoro.Chess.UCI.Protocol.API.Common.Extensions;

/// <summary>
///     Extension methods for played-move history display and current-position advantage resolution.
/// </summary>
public static class PlayedMoveHistoryExtensions
{
	/// <summary>
	///     Resolves the current position advantage from completed high-quality played-move evaluations when available.
	/// </summary>
	/// <param name="history">Played-move history in chronological order.</param>
	/// <param name="currentPositionKey">Stable identifier for the current position.</param>
	/// <param name="legalMoveCount">Current legal move count.</param>
	/// <param name="resolveAnalysis">Resolver for cached position analysis keyed by position key.</param>
	/// <returns>Resolved current advantage, game-over state, or pending analysis state.</returns>
	public static PositionAdvantage ResolveCurrentAdvantage(
		this IReadOnlyList<PlayedMove>        history,
		string                                currentPositionKey,
		int                                   legalMoveCount,
		Func<string, PositionAnalysisResult?> resolveAnalysis)
	{
		if (history is null) throw new ArgumentNullException(nameof(history));
		if (resolveAnalysis is null) throw new ArgumentNullException(nameof(resolveAnalysis));

		if (legalMoveCount == 0)
			return PositionAdvantage.GameOver();

		if (history.Count == 0)
		{
			if (resolveAnalysis(currentPositionKey) is { } openingAnalysis)
				return openingAnalysis.Advantage;

			return PositionAdvantage.GameStart();
		}

		var lastMove = history[^1];
		if (string.Equals(lastMove.PositionKey, currentPositionKey, StringComparison.Ordinal) &&
			lastMove.TryResolveScore(resolveAnalysis, out var score))
			return PositionAdvantage.FromScore(score);

		return PositionAdvantage.Pending();
	}

	/// <summary>
	///     Formats played-move history for simple debugging or console output.
	/// </summary>
	/// <param name="history">Played-move history in chronological order.</param>
	/// <param name="resolveScore">Resolver for played-move scores.</param>
	/// <returns>Formatted history lines.</returns>
	public static string[] ToDisplayLines(
		this IReadOnlyList<PlayedMove>   history,
		Func<PlayedMove, PositionScore?> resolveScore)
	{
		if (history is null) throw new ArgumentNullException(nameof(history));
		if (resolveScore is null) throw new ArgumentNullException(nameof(resolveScore));

		if (history.Count == 0)
			return ["No moves have been played yet."];

		var lines = new string[history.Count + 1];
		lines[0] = "Move history:";

		for (var i = 0; i < history.Count; i++)
		{
			var move = history[i];
			string moveLabel = move.Side == 'w'
								   ? $"{move.Ply}. {move.Move}"
								   : $"{move.Ply}... {move.Move}";

			string scoreText = resolveScore(move) is { } score
								   ? score.ToDisplayString()
								   : "Analyzing...";

			string suffix = move.Classification.ToDebugSuffix();

			lines[i + 1] = $"  {moveLabel,-10} {scoreText}{suffix}";
		}

		return lines;
	}
}
