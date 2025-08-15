using System.Collections.Generic;
using System.Linq;

namespace Bezoro.UCI.API.Types;

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
	public static bool TryParse(string line, out PrincipalVariation pv)
	{
		pv = default;

		if (!line.StartsWith("info ", StringComparison.OrdinalIgnoreCase)) return false;
		if (!line.Contains("pv ")) return false;

		pv = ParseSearchLineTokens(line);
		return pv.Moves.Count != 0;
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

public readonly record struct SearchResult
{
	/// <summary>
	///     Initializes a new instance of the SearchResult struct.
	/// </summary>
	/// <param name="ReachedDepth">The depth reached by the search.</param>
	/// <param name="ReachedSelDepth">The selective depth reached by the search.</param>
	/// <param name="MultiPvValue">The number of principal variations per depth for the search.</param>
	/// <param name="TotalNodesSearched">Total nodes searched by the search.</param>
	/// <param name="TotalTbHits">Total tablebase hits during the search.</param>
	/// <param name="TotalSearchTimeMs">Total time taken for the search in milliseconds.</param>
	/// <param name="PrincipalVariations">The collection of PVs found in the search.</param>
	/// <param name="BestMove">The best move found by the search.</param>
	/// <param name="PonderMove">The ponder move found by the search.</param>
	public SearchResult(
		uint                              ReachedDepth,
		uint                              ReachedSelDepth,
		uint                              MultiPvValue,
		uint                              TotalNodesSearched,
		uint                              TotalTbHits,
		uint                              TotalSearchTimeMs,
		IReadOnlyList<PrincipalVariation> PrincipalVariations,
		string                            BestMove,
		string                            PonderMove
	)
	{
		this.ReachedDepth        = ReachedDepth;
		this.ReachedSelDepth     = ReachedSelDepth;
		this.MultiPvValue        = MultiPvValue;
		this.TotalNodesSearched  = TotalNodesSearched;
		this.TotalTbHits         = TotalTbHits;
		this.TotalSearchTimeMs   = TotalSearchTimeMs;
		this.PrincipalVariations = PrincipalVariations;
		this.BestMove            = BestMove;
		this.PonderMove          = PonderMove;
	}

	/// <summary>True if any PV is a mate line.</summary>
	public bool HasMate => PrincipalVariations.Any(v => v.ScoreMate.HasValue);

	/// <summary>Returns the centipawn score for the best PV (MultiPV=1), or null if not available.</summary>
	public int? BestCpScore => PrincipalVariations.Max(v => v.ScoreCp);

	/// <summary>Returns the shortest mate found (positive for winning, negative for losing), or null if none.</summary>
	public int? MateScore =>
		PrincipalVariations
			.Where(v => v.ScoreMate.HasValue)
			.Select(v => v.ScoreMate!.Value)
			.OrderBy(Math.Abs)
			.FirstOrDefault();

	public PrincipalVariation? BestPv =>
		PrincipalVariations
			.OrderByDescending(v => v.ScoreCp)
			.Select(v => (PrincipalVariation?)v)
			.FirstOrDefault();

	public IReadOnlyList<PrincipalVariation> PrincipalVariations { get; init; }
	public string                            BestMove            { get; init; }
	public string                            PonderMove          { get; init; }
	public uint                              MultiPvValue        { get; init; }
	public uint                              ReachedDepth        { get; init; }
	public uint                              ReachedSelDepth     { get; init; }
	public uint                              TotalNodesSearched  { get; init; }
	public uint                              TotalSearchTimeMs   { get; init; }
	public uint                              TotalTbHits         { get; init; }

	public static bool TryParse(IReadOnlyCollection<string> lines, out SearchResult result)
	{
		result = default;

		uint reachedDepth       = 0,
			 reachedSelDepth    = 0,
			 multiPvValue       = 0,
			 totalNodesSearched = 0,
			 totalTbHits        = 0,
			 totalSearchTimeMs  = 0;

		List<PrincipalVariation> principalVariations = [];

		string bestMove = string.Empty, ponderMove = string.Empty;

		foreach (string? line in lines)
		{
			string[] tokens = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);

			if (line.StartsWith("bestmove ", StringComparison.OrdinalIgnoreCase))
			{
				if (tokens.Length < 2)
					return false;

				bestMove   = tokens[1];
				ponderMove = tokens.Length > 3 && tokens[2] == "ponder" ? tokens[3] : string.Empty;
			}

			if (!PrincipalVariation.TryParse(line, out var pv)) continue;

			reachedDepth = pv.Depth;
			if (pv.SelDepth > reachedSelDepth) reachedSelDepth = pv.SelDepth;
			multiPvValue       =  pv.MultiPv;
			totalNodesSearched += pv.Nodes;
			totalTbHits        += pv.TbHits;
			totalSearchTimeMs  += pv.Time;
			principalVariations.Add(pv);
		}

		result = new(
			reachedDepth,
			reachedSelDepth,
			multiPvValue,
			totalNodesSearched,
			totalTbHits,
			totalSearchTimeMs,
			principalVariations,
			bestMove,
			ponderMove);

		return true;
	}

	/// <summary>True if any PV contains the given move in UCI notation.</summary>
	public bool ContainsMove(string move) =>
		PrincipalVariations.Any(v => v.Moves.Contains(move.ToLowerInvariant()));

	public PrincipalVariation? GetVariationContaining(string move) =>
		PrincipalVariations.FirstOrDefault(pv => pv.Moves.Contains(move.ToLowerInvariant()));

	/// <summary>Returns the PV that starts with the given move, or null if none.</summary>
	public PrincipalVariation? GetVariationStartingWith(string move) =>
		PrincipalVariations.FirstOrDefault(v => v.Moves.FirstOrDefault() == move.ToLowerInvariant());

	/// <summary>
	///     Deconstructs the search result into its constituent parts.
	/// </summary>
	/// <param name="reachedDepth">The depth reached by the search.</param>
	/// <param name="reachedSelDepth">The selective depth reached by the search.</param>
	/// <param name="multiPvValue">The number of principal variations per depth for the search.</param>
	/// <param name="totalNodesSearched">Total nodes searched by the search.</param>
	/// <param name="totalTbHits">Total tablebase hits during the search.</param>
	/// <param name="totalSearchTimeMs">Total time taken for the search in milliseconds.</param>
	/// <param name="principalVariations">The collection of PVs found in the search.</param>
	/// <param name="bestMove">The best move found by the search.</param>
	/// <param name="ponderMove">The ponder move found by the search.</param>
	public void Deconstruct(
		out uint                              reachedDepth,
		out uint                              reachedSelDepth,
		out uint                              multiPvValue,
		out uint                              totalNodesSearched,
		out uint                              totalTbHits,
		out uint                              totalSearchTimeMs,
		out IReadOnlyList<PrincipalVariation> principalVariations,
		out string                            bestMove,
		out string                            ponderMove)
	{
		reachedDepth        = ReachedDepth;
		reachedSelDepth     = ReachedSelDepth;
		multiPvValue        = MultiPvValue;
		totalNodesSearched  = TotalNodesSearched;
		totalTbHits         = TotalTbHits;
		totalSearchTimeMs   = TotalSearchTimeMs;
		principalVariations = PrincipalVariations;
		bestMove            = BestMove;
		ponderMove          = PonderMove;
	}
}
