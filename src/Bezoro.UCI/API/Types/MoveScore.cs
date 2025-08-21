using System.Globalization;
using System.Linq;

namespace Bezoro.UCI.API.Types;

public readonly record struct MoveScore()
{
	private MoveScore(int? scoreCp, int? scoreMate) : this()
	{
		ScoreCp   = scoreCp;
		ScoreMate = scoreMate;
	}

	public int? ScoreCp   { get; }
	public int? ScoreMate { get; }

	public static bool TryParse(string line, out MoveScore? score)
	{
		score = null;

		if (string.IsNullOrEmpty(line)) return false;

		int? scoreCp   = null;
		int? scoreMate = null;

		int scoreIdx = line.IndexOf(" score ", StringComparison.OrdinalIgnoreCase);
		if (scoreIdx < 0) return false;

		int mateIdx = line.IndexOf(" mate ", scoreIdx, StringComparison.OrdinalIgnoreCase);
		if (mateIdx >= 0)
		{
			int start        = mateIdx + 6;
			int end          = line.IndexOf(' ', start);
			if (end < 0) end = line.Length;

			if (!int.TryParse(
					line.AsSpan(start, end - start),
					NumberStyles.Integer,
					CultureInfo.InvariantCulture,
					out int mateScore)) return false;

			scoreMate = mateScore;
		}

		int cpIdx = line.IndexOf(" cp ", scoreIdx, StringComparison.OrdinalIgnoreCase);
		if (cpIdx >= 0)
		{
			int start        = cpIdx + 4;
			int end          = line.IndexOf(' ', start);
			if (end < 0) end = line.Length;

			if (!int.TryParse(
					line.AsSpan(start, end - start),
					NumberStyles.Integer,
					CultureInfo.InvariantCulture,
					out int cpScore)) return false;

			scoreCp = cpScore;
		}

		if (scoreCp == null && scoreMate == null) return false;

		score = new(scoreCp, scoreMate);
		return true;
	}

	public static MoveScore FromCp(int   cp)   => new(cp, null);
	public static MoveScore FromMate(int mate) => new(null, mate);

	/// <summary>
	///     Builds a MoveScore from a SearchResult returned by the engine.
	///     Prefers mate scores when present, otherwise falls back to centipawns.
	/// </summary>
	public static MoveScore FromSearchResult(SearchResult result)
	{
		if (result.HasMate && result.MateScore.HasValue)
			return FromMate(result.MateScore.Value);

		int? cp = result.BestCpScore ?? result.PrincipalVariations.FirstOrDefault().ScoreCp;
		return cp.HasValue ? FromCp(cp.Value) : default;
	}
}
