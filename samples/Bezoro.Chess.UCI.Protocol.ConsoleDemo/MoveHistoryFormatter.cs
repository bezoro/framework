using Bezoro.Chess.UCI.Protocol.API.Types;

namespace Bezoro.Chess.UCI.Protocol.ConsoleDemo;

internal static class MoveHistoryFormatter
{
	public static string[] BuildLines(
		IReadOnlyList<MoveHistoryEntry>      history,
		Func<MoveHistoryEntry, PositionScore?> resolveScore)
	{
		if (history.Count == 0)
			return ["No moves have been played yet."];

		var lines = new string[history.Count + 1];
		lines[0] = "Move history:";

		for (var i = 0; i < history.Count; i++)
		{
			var entry = history[i];
			string moveLabel = entry.Side == 'w'
				? $"{entry.Ply}. {entry.Move}"
				: $"{entry.Ply}... {entry.Move}";
			string scoreText = resolveScore(entry) is { } score
				? score.ToDisplayString()
				: "Analyzing...";

			lines[i + 1] = $"  {moveLabel,-10} {scoreText}";
		}

		return lines;
	}
}
