namespace Bezoro.Logging.Types;

/// <summary>
///     Configuration for log grouping.
/// </summary>
public readonly struct GroupingConfig
{
	/// <summary>
	///     Creates a grouping configuration with the specified strategy.
	/// </summary>
	/// <param name="groupBy">The grouping strategy.</param>
	/// <param name="includeInOutput">Whether to include grouping info in the formatted message.</param>
	/// <param name="timeWindowMs">Time window for TimeWindow grouping.</param>
	public static GroupingConfig Create(
		LoggerSettings.ContextGrouping groupBy,
		bool                           includeInOutput = true,
		int                            timeWindowMs    = 100) =>
		new() { GroupBy = groupBy, IncludeInOutput = includeInOutput, TimeWindowMs = timeWindowMs };

	/// <summary>
	///     No grouping.
	/// </summary>
	public static GroupingConfig None => new()
	{
		GroupBy         = LoggerSettings.ContextGrouping.None,
		IncludeInOutput = false,
		TimeWindowMs    = 100
	};

	/// <summary>
	///     Whether to include grouping context in the formatted output.
	/// </summary>
	public bool IncludeInOutput { get; init; }

	/// <summary>
	///     The grouping strategy to use.
	/// </summary>
	public LoggerSettings.ContextGrouping GroupBy { get; init; }

	/// <summary>
	///     Time window in milliseconds for TimeWindow grouping (only used when GroupBy is TimeWindow).
	/// </summary>
	public int TimeWindowMs { get; init; }
}
