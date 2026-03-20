using System.Collections.Generic;
using System.Linq;

namespace Bezoro.Chess.UCI.Protocol.API.Types;

/// <summary>
///     Represents a principal variation in a chess position evaluation.
/// </summary>
/// <param name="Depth">The depth of this principal variation</param>
/// <param name="SelDepth">The selective depth of this principal variation</param>
/// <param name="MultiPv">The MultiPv value of this principal variation</param>
/// <param name="ScoreCp">The centipawn score of this principal variation, or null if not available</param>
/// <param name="ScoreMate">The mate score of this principal variation, or null if not available</param>
/// <param name="Nodes">The number of nodes searched for this principal variation</param>
/// <param name="Nps">The nodes per second for this principal variation</param>
/// <param name="TbHits">The number of tablebase hits for this principal variation</param>
/// <param name="Time">The time taken for this principal variation in milliseconds</param>
/// <param name="Moves">The sequence of moves in this principal variation</param>
/// <param name="RawPv">The raw PV move string as emitted by the engine.</param>
public readonly record struct PrincipalVariation(
	uint                  Depth,
	uint                  SelDepth,
	uint                  MultiPv,
	int?                  ScoreCp,
	int?                  ScoreMate,
	uint                  Nodes,
	uint                  Nps,
	uint                  TbHits,
	uint                  Time,
	IReadOnlyList<string> Moves,
	string                RawPv
)
{
	/// <summary>
	///     Attempts to parse a UCI <c>info ... pv ...</c> line.
	/// </summary>
	/// <param name="line">Raw engine output line.</param>
	/// <param name="pv">Parsed principal variation when successful.</param>
	/// <returns><see langword="true" /> when the line contains a valid PV payload; otherwise <see langword="false" />.</returns>
	public static bool TryParse(string line, out PrincipalVariation pv)
	{
		pv = default;

		if (!line.StartsWith("info ", StringComparison.OrdinalIgnoreCase)) return false;

		// Ensure a standalone 'pv' token exists (avoid matching 'multipv')
		string[]? tokens = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
		if (!tokens.Contains("pv")) return false;

		pv = ParseSearchLineTokens(line);
		return pv.Moves is { Count: > 0 };
	}

	private static PrincipalVariation ParseSearchLineTokens(string line)
	{
		uint         depth   = 0,    selDepth  = 0, multiPv = 0, nodes = 0, nps = 0, tbHits = 0, time = 0;
		int?         scoreCp = null, scoreMate = null;
		List<string> pvMoves = [];
		string       rawPv   = string.Empty;

		string[] tokens = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);

		for (var i = 1; i < tokens.Length; i++)
		{
			switch (tokens[i])
			{
				case "depth" when i + 1 < tokens.Length && uint.TryParse(tokens[i + 1], out uint d):
					depth = d;
					i++;
					break;
				case "seldepth" when i + 1 < tokens.Length && uint.TryParse(tokens[i + 1], out uint sd):
					selDepth = sd;
					i++;
					break;
				case "multipv" when i + 1 < tokens.Length && uint.TryParse(tokens[i + 1], out uint mpv):
					multiPv = mpv;
					i++;
					break;

				case "score" when i + 2 < tokens.Length:
					string type = tokens[i + 1];
					string val  = tokens[i + 2];
					i += 2;

					switch (type)
					{
						case "cp" when int.TryParse(val,   out int cp): scoreCp   = cp; break;
						case "mate" when int.TryParse(val, out int m):  scoreMate = m; break;
					}

					break;

				case "nodes" when i + 1 < tokens.Length && uint.TryParse(tokens[i + 1], out uint n):
					nodes = n;
					i++;
					break;
				case "nps" when i + 1 < tokens.Length && uint.TryParse(tokens[i + 1], out uint perSec):
					nps = perSec;
					i++;
					break;
				case "tbhits" when i + 1 < tokens.Length && uint.TryParse(tokens[i + 1], out uint tb):
					tbHits = tb;
					i++;
					break;
				case "time" when i + 1 < tokens.Length && uint.TryParse(tokens[i + 1], out uint t):
					time = t;
					i++;
					break;

				case "pv":
					rawPv = string.Join(" ", tokens.Skip(i + 1));
					for (int j = i + 1; j < tokens.Length; j++)
						pvMoves.Add(tokens[j]);

					i = tokens.Length;
					break;
			}
		}

		if (pvMoves.Count == 0) return default;

		return new(depth, selDepth, multiPv, scoreCp, scoreMate, nodes, nps, tbHits, time, pvMoves, rawPv);
	}
}
