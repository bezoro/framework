using System.Globalization;

namespace Bezoro.Chess.UCI.API.Types;

/// <summary>
///     Represents either a centipawn evaluation or a distance-to-mate score.
/// </summary>
public readonly record struct MoveScore()
{
	private MoveScore(int? scoreCp, int? scoreMate) : this()
	{
		ScoreCp   = scoreCp;
		ScoreMate = scoreMate;
	}

	/// <summary>Gets the centipawn score, when available.</summary>
	public int? ScoreCp { get; }

	/// <summary>Gets the signed number of moves to mate, when available.</summary>
	public int? ScoreMate { get; }

	/// <summary>
	///     Attempts to parse a score from a UCI engine information line.
	/// </summary>
	/// <param name="line">The UCI information line.</param>
	/// <param name="score">When successful, receives the parsed score.</param>
	/// <returns><see langword="true" /> when a centipawn or mate score is present and valid; otherwise, <see langword="false" />.</returns>
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
					out int mateScore
				)) return false;

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
					out int cpScore
				)) return false;

			scoreCp = cpScore;
		}

		if (scoreCp == null && scoreMate == null) return false;

		score = new(scoreCp, scoreMate);
		return true;
	}

	/// <summary>Creates a centipawn score.</summary>
	/// <param name="cp">The signed centipawn evaluation.</param>
	/// <returns>A centipawn-based score.</returns>
	public static MoveScore FromCp(int cp) => new(cp, null);

	/// <summary>Creates a distance-to-mate score.</summary>
	/// <param name="mate">The signed number of moves to mate.</param>
	/// <returns>A mate-based score.</returns>
	public static MoveScore FromMate(int mate) => new(null, mate);

	/// <summary>
	///     Builds a MoveScore from a SearchResult returned by the engine.
	///     Prefers mate scores when present, otherwise falls back to centipawns.
	/// </summary>
	/// <param name="result">The engine search result to convert.</param>
	/// <returns>A mate or centipawn score, or the default value when no score is available.</returns>
	public static MoveScore FromSearchResult(SearchResult result)
	{
		if (result.HasMate && result.MateScore.HasValue)
			return FromMate(result.MateScore.Value);

		int? cp = result.BestCpScore;
		if (!cp.HasValue)
		{
			var variations = result.PrincipalVariations;
			if (variations.Length > 0)
				// Prefer the first available centipawn score
				foreach (var pv in variations)
				{
					if (pv.ScoreCp.HasValue)
					{
						cp = pv.ScoreCp;
						break;
					}
				}
		}

		return cp.HasValue ? FromCp(cp.Value) : default;
	}
}
