using System.Collections.Generic;
using System.Linq;

namespace Bezoro.Chess.UCI.Protocol.API.Common.Extensions;

/// <summary>
///     Extension methods for formatting <see cref="SearchResult" /> values.
/// </summary>
public static class SearchResultExtensions
{
	/// <summary>
	///     Formats a compact engine-line summary similar to a console engine move annotation.
	/// </summary>
	/// <param name="result">Search result to format.</param>
	/// <param name="maxVariationMoves">Maximum number of PV moves to include.</param>
	/// <returns>Formatted summary, or an empty string when no details are available.</returns>
	public static string ToDisplayString(this SearchResult result, int maxVariationMoves = 6)
	{
		var parts = new List<string>();

		if (result.ReachedDepth > 0)
			parts.Add($"depth {result.ReachedDepth}");

		if (result.MateScore is int mateScore)
			parts.Add($"mate {mateScore}");
		else if (result.BestCpScore is int cpScore)
			parts.Add($"eval {cpScore} cp");

		var matchingVariation = result.GetVariationStartingWith(result.BestMove);
		if (matchingVariation is { } bestVariation && !bestVariation.Moves.IsDefaultOrEmpty)
			parts.Add($"pv {string.Join(' ', bestVariation.Moves.Take(Math.Max(1, maxVariationMoves)))}");

		return parts.Count == 0 ? string.Empty : $" ({string.Join(", ", parts)})";
	}
}
