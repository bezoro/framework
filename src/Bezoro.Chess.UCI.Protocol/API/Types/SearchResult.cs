using System.Collections.Generic;
using System.Linq;
using Bezoro.Chess.UCI.Protocol.Domain.EngineClient;

namespace Bezoro.Chess.UCI.Protocol.API.Types;

/// <summary>
///     Represents the parsed result of a completed UCI search, including the final best move and any captured PVs.
/// </summary>
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
	public bool HasMate => GetPrincipalVariations().Any(v => v.ScoreMate.HasValue);

	/// <summary>Returns the centipawn score for the best PV (MultiPV=1), or null if not available.</summary>
	public int? BestCpScore
	{
		get
		{
			var principalVariations = GetPrincipalVariations();
			return principalVariations.Count == 0 ? null : principalVariations.Max(v => v.ScoreCp);
		}
	}

	/// <summary>Returns the shortest mate found (positive for winning, negative for losing), or null if none.</summary>
	public int? MateScore =>
		GetPrincipalVariations()
			.Where(v => v.ScoreMate.HasValue)
			.Select(v => v.ScoreMate!.Value)
			.OrderBy(Math.Abs)
			.FirstOrDefault();

	/// <summary>
	///     Returns the highest-scoring principal variation, or <see langword="null" /> when none were captured.
	/// </summary>
	public PrincipalVariation? BestPv =>
		GetPrincipalVariations()
			.OrderByDescending(v => v.ScoreCp)
			.Select(v => (PrincipalVariation?)v)
			.FirstOrDefault();

	/// <summary>
	///     Gets the principal variations captured from <c>info ... pv ...</c> lines.
	/// </summary>
	public IReadOnlyList<PrincipalVariation> PrincipalVariations { get; init; } = Array.Empty<PrincipalVariation>();

	/// <summary>
	///     Gets the move reported by the engine in the final <c>bestmove</c> line.
	/// </summary>
	public string BestMove { get; init; } = string.Empty;

	/// <summary>
	///     Gets the optional ponder move reported alongside <see cref="BestMove" />.
	/// </summary>
	public string PonderMove { get; init; } = string.Empty;

	/// <summary>
	///     Gets the last observed MultiPV value.
	/// </summary>
	public uint MultiPvValue { get; init; }

	/// <summary>
	///     Gets the deepest reported search depth.
	/// </summary>
	public uint ReachedDepth { get; init; }

	/// <summary>
	///     Gets the deepest reported selective depth.
	/// </summary>
	public uint ReachedSelDepth { get; init; }

	/// <summary>
	///     Gets the sum of nodes reported across parsed PV lines.
	/// </summary>
	public uint TotalNodesSearched { get; init; }

	/// <summary>
	///     Gets the sum of elapsed milliseconds reported across parsed PV lines.
	/// </summary>
	public uint TotalSearchTimeMs { get; init; }

	/// <summary>
	///     Gets the sum of tablebase hits reported across parsed PV lines.
	/// </summary>
	public uint TotalTbHits { get; init; }

	/// <summary>
	///     Attempts to parse a completed search transcript into a <see cref="SearchResult" />.
	/// </summary>
	/// <param name="lines">Captured engine output lines for a single search.</param>
	/// <param name="result">Parsed result when the transcript contains a valid <c>bestmove</c> line.</param>
	/// <returns><see langword="true" /> when parsing succeeds; otherwise <see langword="false" />.</returns>
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
		var    foundValidBestMove = false;

		foreach (string? line in lines)
		{
			if (string.IsNullOrWhiteSpace(line)) continue;

			if (line.StartsWith("bestmove ", StringComparison.OrdinalIgnoreCase))
			{
				if (!BestMoveLine.TryParse(line, out var bestMoveLine) ||
					!UciCommandBuilder.IsUciMoveString(bestMoveLine.BestMove) ||
					(bestMoveLine.HasPonder && !UciCommandBuilder.IsUciMoveString(bestMoveLine.PonderMove!)))
				{
					return false;
				}

				bestMove           = bestMoveLine.BestMove;
				ponderMove         = bestMoveLine.PonderMove ?? string.Empty;
				foundValidBestMove = true;
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

		if (!foundValidBestMove)
			return false;

		result = new(
			reachedDepth,
			reachedSelDepth,
			multiPvValue,
			totalNodesSearched,
			totalTbHits,
			totalSearchTimeMs,
			principalVariations,
			bestMove,
			ponderMove
		);

		return true;
	}

	/// <summary>True if any PV contains the given move in UCI notation.</summary>
	public bool ContainsMove(string move) =>
		GetPrincipalVariations().Any(v => v.Moves.Contains(move.ToLowerInvariant()));

	/// <summary>
	///     Returns the first principal variation containing the supplied move, or <see langword="null" /> when absent.
	/// </summary>
	public PrincipalVariation? GetVariationContaining(string move) =>
		GetPrincipalVariations().FirstOrDefault(pv => pv.Moves.Contains(move.ToLowerInvariant()));

	/// <summary>Returns the PV that starts with the given move, or null if none.</summary>
	public PrincipalVariation? GetVariationStartingWith(string move) =>
		GetPrincipalVariations().FirstOrDefault(v => v.Moves.FirstOrDefault() == move.ToLowerInvariant());

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

	private IReadOnlyList<PrincipalVariation> GetPrincipalVariations() =>
		PrincipalVariations ?? Array.Empty<PrincipalVariation>();
}
