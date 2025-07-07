using System.Collections.Generic;

namespace Bezoro.UCI.API.Types;

/// <summary>
///     Represents the complete result of an engine search operation.
/// </summary>
/// <summary>
///     Represents the complete result of an engine search operation.
/// </summary>
public record struct SearchResult()
{
	// Search control and status
	/// <summary> Whether the search was stopped before completion. </summary>
	public bool WasStoppedEarly { get; set; } = false;
	/// <summary> The search depth reached. </summary>
	public int? Depth { get; set; } = null;

	// Engine evaluation results
	/// <summary> The final evaluation score from the engine's perspective. </summary>
	public int? FinalScore { get; set; } = null;
	/// <summary> Collection of all analysis info received during the search. </summary>
	public List<EngineAnalysisInfo>? AnalysisInfo { get; init; } = [ ];
	/// <summary> Time taken for the search in milliseconds. </summary>
	public long SearchTimeMs { get; set; } = 0;

	// Performance and analysis data
	/// <summary> Average nodes per second. </summary>
	public long? AverageNps { get; set; } = null;
	/// <summary> Total nodes searched. </summary>
	public long? TotalNodes { get; set; } = null;
	/// <summary> The best move found by the engine in UCI format (e.g., "e2e4"). </summary>
	public string? BestMove { get; set; } = null;
	public string? Checkers { get; set; } = null;
	/// <summary> The suggested ponder move (opponent's expected response). </summary>
	public string? PonderMove { get; set; } = null;

	public override string ToString() =>
		$"Best: {BestMove}, Score: {FinalScore}, Depth: {Depth}, Time: {SearchTimeMs}ms, Nodes: {TotalNodes}";
}
