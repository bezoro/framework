using System.Collections.Generic;

namespace Bezoro.UCI.API.Types;

/// <summary>
///     Contains parsed information from a UCI 'info' string.
/// </summary>
public readonly record struct EngineAnalysisInfo(string RawLine)
{
	/// <summary> The raw 'info' string from the engine. </summary>
	public string RawLine { get; } = RawLine;
	/// <summary> Number of current move being searched. </summary>
	public int? CurrentMoveNumber { get; init; }
	/// <summary> Search depth in plies. </summary>
	public int? Depth { get; init; }
	/// <summary> Hash table occupancy in per mille. </summary>
	public int? HashFull { get; init; }
	/// <summary> The evaluation is a mate in X moves. </summary>
	public int? Mate { get; init; }
	/// <summary> The evaluation score from the engine's point of view in centipawns. </summary>
	public int? ScoreCp { get; init; }
	/// <summary> Selective search depth. </summary>
	public int? SelDepth { get; init; }
	/// <summary> The principal variation (the best line of moves found). </summary>
	public IReadOnlyList<string>? PrincipalVariation { get; init; }
	/// <summary> The number of nodes searched. </summary>
	public long? Nodes { get; init; }
	/// <summary> The search speed in nodes per second. </summary>
	public long? Nps { get; init; }
	/// <summary> Number of tablebase hits. </summary>
	public long? TbHits { get; init; }
	/// <summary> Time spent searching in milliseconds. </summary>
	public long? TimeMs { get; init; }
	/// <summary> Current move being searched. </summary>
	public string? CurrentMove { get; init; }

	public override string ToString() =>
		$"Depth: {Depth}, Score: {(ScoreCp.HasValue ? $"cp {ScoreCp}" : Mate.HasValue ? $"mate {Mate}" : "N/A")}, Nodes: {Nodes}, Nps: {Nps}";
}
