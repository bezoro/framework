using Bezoro.UCI.API.Enums;

namespace Bezoro.UCI.API.Types;

/// <summary>
///     Represents a complete UCI engine output line with all possible data.
/// </summary>
public record struct EngineOutput(string RawLine)
{
	/// <summary> The raw line from the engine. </summary>
	public string RawLine { get; } = RawLine;
	/// <summary> Analysis information (for info lines). </summary>
	public EngineAnalysisInfo? AnalysisInfo { get; set; }
	/// <summary> Engine identification (for id lines). </summary>
	public EngineId? Id { get; init; }
	/// <summary> Engine option (for option lines). </summary>
	public EngineOption? Option { get; init; }
	/// <summary> The type of output this represents. </summary>
	public EngineOutputType Type { get; set; }
	/// <summary> Best move (for bestmove lines). </summary>
	public string? BestMove { get; set; }
	/// <summary> Ponder move (for bestmove lines). </summary>
	public string? PonderMove { get; set; }
	/// <summary> Status message (for readyok, uciok, etc.). </summary>
	public string? Status { get; init; }

	public override string ToString() => Type switch
	{
		EngineOutputType.Info     => AnalysisInfo?.ToString() ?? "Info (empty)",
		EngineOutputType.BestMove => $"Best: {BestMove}" + (PonderMove != null ? $", Ponder: {PonderMove}" : ""),
		EngineOutputType.Option   => Option?.ToString() ?? "Option (empty)",
		EngineOutputType.Id       => Id?.ToString()     ?? "Id (empty)",
		EngineOutputType.Status   => Status             ?? "Status (empty)",
		_                         => $"Unknown: {RawLine}"
	};
}
