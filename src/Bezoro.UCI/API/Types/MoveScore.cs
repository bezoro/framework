using Bezoro.Core.Common.Extensions;

namespace Bezoro.UCI.API.Types;

public readonly record struct MoveScore()
{
	public MoveScore(int? scoreCp, int? scoreMate) : this()
	{
		ScoreCp   = scoreCp;
		ScoreMate = scoreMate;
	}

	public int? ScoreCp   { get; }
	public int? ScoreMate { get; }

	public static bool TryParse(string line, out MoveScore? score)
	{
		line.ThrowIfNull();

		int? scoreCp   = null;
		int? scoreMate = null;
		score = null;

		int scoreIdx = line.IndexOf(" score ", StringComparison.OrdinalIgnoreCase);
		if (scoreIdx < 0)
		{
			score = null;
			return false;
		}

		int mateIdx = line.IndexOf(" mate ", scoreIdx, StringComparison.OrdinalIgnoreCase);
		if (mateIdx >= 0)
		{
			int start        = mateIdx + 6;
			int end          = line.IndexOf(' ', start);
			if (end < 0) end = line.Length;

			if (!int.TryParse(line.AsSpan(start, end - start), out int mateScore))
			{
				score = null;
				return false;
			}

			scoreMate = mateScore;
		}

		int cpIdx = line.IndexOf(" cp ", scoreIdx, StringComparison.Ordinal);
		if (cpIdx >= 0)
		{
			int start        = cpIdx + 4;
			int end          = line.IndexOf(' ', start);
			if (end < 0) end = line.Length;

			if (!int.TryParse(line.AsSpan(start, end - start), out int cpScore))
			{
				score = null;
				return false;
			}

			scoreCp = cpScore;
		}

		if (scoreCp == null && scoreMate == null)
		{
			score = null;
			return false;
		}

		score = new(scoreCp, scoreMate);
		return true;
	}
}