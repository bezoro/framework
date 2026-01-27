namespace Bezoro.Logging.Types;

/// <summary>
///     Contains all data for a single log event.
/// </summary>
public sealed class LogPayload
{
	/// <summary>
	///     UTC timestamp when the log was created.
	/// </summary>
	public DateTime Timestamp { get; init; } = DateTime.UtcNow;

	/// <summary>
	///     Async context nesting depth (0 = no context, 1 = root, 2+ = nested).
	/// </summary>
	public int AsyncContextDepth { get; init; }

	/// <summary>
	///     Async context hierarchy (e.g., ["GameLoop", "Player-123", "AI"]).
	/// </summary>
	public IReadOnlyList<string>? AsyncContextHierarchy { get; init; }

	/// <summary>
	///     Optional category for the log message.
	/// </summary>
	public LogCategory? Category { get; init; }

	/// <summary>
	///     Optional stage label included in the formatted output.
	/// </summary>
	public string? Stage { get; init; }

	/// <summary>
	///     The severity level of the log.
	/// </summary>
	public required LogLevel Level { get; init; }

	/// <summary>
	///     Visual style hints for rendering (color, bold, etc.).
	/// </summary>
	public LogStyle Style { get; init; } = LoggerSettings.InfoStyle;

	/// <summary>
	///     Optional context object (e.g., Unity Object for console highlighting).
	/// </summary>
	public object? ContextObject { get; init; }

	/// <summary>
	///     Fully formatted log message ready for output.
	/// </summary>
	public required string FormattedMessage { get; init; }

	/// <summary>
	///     The raw message content.
	/// </summary>
	public required string Message { get; init; }

	/// <summary>
	///     Emoji representing the severity level.
	/// </summary>
	public required string SeverityEmoji { get; init; }

	/// <summary>
	///     Formatted async context string (format depends on LoggerSettings.AsyncFormat).
	/// </summary>
	public string? AsyncContext { get; init; }

	/// <summary>
	///     Caller information in format "TypeName.MethodName()" (if captured).
	/// </summary>
	public string? CallerInfo { get; init; }

	/// <summary>
	///     Emoji representing the category (if category is specified).
	/// </summary>
	public string? CategoryEmoji { get; init; }

	/// <summary>
	///     Exception type name (only for exceptions).
	/// </summary>
	public string? ExceptionType { get; init; }

	/// <summary>
	///     Automatic grouping context based on LoggerSettings.GroupBy.
	/// </summary>
	public string? GroupingContext { get; init; }

	/// <summary>
	///     Inner exception message (if present).
	/// </summary>
	public string? InnerExceptionMessage { get; init; }

	/// <summary>
	///     Inner exception type name (if present).
	/// </summary>
	public string? InnerExceptionType { get; init; }

	/// <summary>
	///     Full stack trace (only for exceptions).
	/// </summary>
	public string? StackTrace { get; init; }
}
