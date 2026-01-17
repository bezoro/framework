using System.Collections;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Bezoro.Logging.Types;
using Bezoro.Logging.Utilities;

namespace Bezoro.Logging;

/// <summary>
///     Provides a clean, flexible logging utility with optional complexity.
/// </summary>
public static class Logger
{
	/// <summary>
	///     Event invoked when a log message is processed.
	/// </summary>
	public static event Action<LogPayload>? OnLog;

	/// <summary>
	///     Minimum log level to process. Logs below this level are ignored.
	///     Default: <see cref="LogLevel.Info" />.
	/// </summary>
	public static LogLevel MinimumLevel { get; set; } = LogLevel.Info;

	/// <summary>
	///     Begins a performance timer that logs the operation duration when disposed.
	/// </summary>
	/// <param name="operationName">The name of the operation being timed.</param>
	/// <param name="category">Optional log category.</param>
	/// <param name="contextObject">Optional context object.</param>
	/// <returns>A disposable timer that logs when disposed (fully compiled away if DEBUG is not defined).</returns>
	/// <example>
	///     <code>
	/// using (Logger.BeginTimer("LoadLevel", LogCategory.Loading))
	/// {
	///     // ... operation code ...
	/// }
	/// // Logs: [ℹ️] [⏳] LoadLevel completed in 123.45ms
	/// </code>
	/// </example>
	public static PerformanceTimer BeginTimer(
		string       operationName,
		LogCategory? category      = null,
		object?      contextObject = null) =>
		new(operationName, category, contextObject);

	/// <summary>
	///     Logs a message with optional complexity.
	/// </summary>
	/// <param name="message">The message to log (string, number, object, FormattableString, etc.).</param>
	/// <param name="level">The severity level (default: Info).</param>
	/// <param name="category">Optional log category.</param>
	/// <param name="contextObject">Optional context object (e.g., Unity Object for console highlighting).</param>
	/// <param name="captureCallerInfo">Whether to automatically capture caller information.</param>
	/// <param name="memberName">
	///     Automatically populated with the calling member name via <see cref="CallerMemberNameAttribute" />.
	///     Do not provide this parameter manually.
	/// </param>
	/// <param name="filePath">
	///     Automatically populated with the source file path via <see cref="CallerFilePathAttribute" />.
	///     Do not provide this parameter manually.
	/// </param>
	[Conditional("DEBUG")]
	public static void Log(
		object                     message,
		LogLevel                   level             = LogLevel.Info,
		LogCategory?               category          = null,
		object?                    contextObject     = null,
		bool                       captureCallerInfo = false,
		[CallerMemberName] string? memberName        = null,
		[CallerFilePath]   string? filePath          = null)
	{
		// Capture caller info if requested
		string? callerInfo = null;
		if (captureCallerInfo && memberName != null)
		{
			string typeName = ExtractTypeNameFromFilePath(filePath);
			callerInfo = $"{typeName}.{memberName}()";
		}

		// Format the message based on type
		string formattedMessage = message is FormattableString formattable
									  ? FormatMessage(formattable)
									  : FormatMessage(message);

		BuildAndInvokePayload(
			formattedMessage,
			level,
			category,
			contextObject,
			null,
			callerInfo,
			null,
			null,
			null,
			filePath);
	}

	/// <summary>
	///     Logs an exception with automatic detail extraction.
	/// </summary>
	/// <param name="exception">The exception to log.</param>
	/// <param name="customMessage">Optional custom message to prepend to the exception message for additional context.</param>
	/// <param name="category">Optional log category.</param>
	/// <param name="contextObject">Optional context object (e.g., Unity Object for console highlighting).</param>
	/// <param name="captureCallerInfo">Whether to automatically capture caller information (default: true).</param>
	/// <param name="memberName">
	///     Automatically populated with the calling member name via <see cref="CallerMemberNameAttribute" />.
	///     Do not provide this parameter manually.
	/// </param>
	/// <param name="filePath">
	///     Automatically populated with the source file path via <see cref="CallerFilePathAttribute" />.
	///     Do not provide this parameter manually.
	/// </param>
	[Conditional("DEBUG")]
	public static void Log(
		Exception                  exception,
		string?                    customMessage     = null,
		LogCategory?               category          = null,
		object?                    contextObject     = null,
		bool                       captureCallerInfo = true,
		[CallerMemberName] string? memberName        = null,
		[CallerFilePath]   string? filePath          = null)
	{
		string message = string.IsNullOrEmpty(customMessage)
							 ? exception.Message
							 : $"{customMessage} | {exception.Message}";

		string  exceptionType = exception.GetType().Name;
		string? stackTrace    = exception.StackTrace;

		// Capture inner exception details
		string? innerExceptionType    = exception.InnerException?.GetType().Name;
		string? innerExceptionMessage = exception.InnerException?.Message;

		// Capture caller info if requested
		string? callerInfo = null;
		if (captureCallerInfo && memberName != null)
		{
			string typeName = ExtractTypeNameFromFilePath(filePath);
			callerInfo = $"{typeName}.{memberName}()";
		}

		BuildAndInvokePayload(
			message,
			LogLevel.Exception,
			category,
			contextObject,
			exceptionType,
			callerInfo,
			stackTrace,
			innerExceptionType,
			innerExceptionMessage,
			filePath);
	}

	/// <summary>
	///     Extracts the type name from a file path (e.g., "GameManager" from "path/to/GameManager.cs").
	/// </summary>
	private static string ExtractTypeNameFromFilePath(string? filePath)
	{
		if (string.IsNullOrEmpty(filePath))
			return "Unknown";

		string? fileName = Path.GetFileNameWithoutExtension(filePath);
		return fileName ?? "Unknown";
	}


	/// <summary>
	///     Formats a message object into a string representation.
	/// </summary>
	private static string FormatMessage(object message)
	{
		if (message is not (IEnumerable collection and not string)) return message.ToString() ?? string.Empty;

		var collectionAsStrings =
			collection.Cast<object>().Select(o => o?.ToString() ?? "null");

		return $"[{string.Join(", ", collectionAsStrings)}]";
	}

	/// <summary>
	///     Formats a formattable string message with proper collection handling.
	/// </summary>
	private static string FormatMessage(FormattableString formattableMessage)
	{
		object?[] arguments     = formattableMessage.GetArguments();
		var       formattedArgs = new object?[arguments.Length];

		for (var i = 0; i < arguments.Length; i++)
		{
			object? arg = arguments[i];

			if (arg is IEnumerable collection and not string)
			{
				var collectionAsStrings =
					collection.Cast<object>().Select(o => o?.ToString() ?? "null");

				formattedArgs[i] = $"\n[{string.Join("\n", collectionAsStrings)}]";
			}
			else
			{
				formattedArgs[i] = arg;
			}
		}

		return string.Format(formattableMessage.Format, formattedArgs);
	}

	/// <summary>
	///     Calculates a time window group identifier based on the current time and configured window size.
	/// </summary>
	private static string GetTimeWindowGroup(DateTime timestamp, int timeWindowMs)
	{
		long ticks        = timestamp.Ticks;
		long windowTicks  = TimeSpan.FromMilliseconds(timeWindowMs).Ticks;
		long windowNumber = ticks / windowTicks;

		return $"Window-{windowNumber}";
	}

	/// <summary>
	///     Builds the log payload and invokes the OnLog event.
	/// </summary>
	private static void BuildAndInvokePayload(
		string       message,
		LogLevel     level,
		LogCategory? category,
		object?      contextObject,
		string?      exceptionType,
		string?      callerInfo,
		string?      stackTrace,
		string?      innerExceptionType,
		string?      innerExceptionMessage,
		string?      filePath)
	{
		// Check master switch
		if (!LoggerSettings.Enabled)
			return;

		// Check minimum level filtering
		if (level < MinimumLevel)
			return;

		// Check category filtering
		if (category.HasValue && LoggerSettings.MutedCategories.Contains(category.Value))
			return;

		string severityEmoji = LogLevelEmoji.GetEmoji(level);
		string? categoryEmoji = category.HasValue
									? LogCategoryEmoji.GetEmoji(category.Value)
									: null;

		// Get async context info
		var asyncHierarchy = LoggerSettings.CurrentAsyncContextHierarchy;
		int asyncDepth     = asyncHierarchy?.Count ?? 0;

		// Determine grouping context
		var now = DateTime.UtcNow;
		string? groupingContext = LoggerSettings.Grouping.GroupBy switch
		{
			LoggerSettings.ContextGrouping.CallerType => ExtractTypeNameFromFilePath(filePath),
			LoggerSettings.ContextGrouping.CallerMethod => callerInfo,
			LoggerSettings.ContextGrouping.Category => category?.ToString(),
			LoggerSettings.ContextGrouping.Thread => Environment.CurrentManagedThreadId.ToString(),
			LoggerSettings.ContextGrouping.Level => level.ToString(),
			LoggerSettings.ContextGrouping.TimeWindow => GetTimeWindowGroup(now, LoggerSettings.Grouping.TimeWindowMs),
			LoggerSettings.ContextGrouping.AsyncContext => asyncHierarchy != null
															   ? string.Join(" > ", asyncHierarchy)
															   : null,
			_ => null
		};

		// Get style for this log level
		var style = LoggerSettings.GetStyle(level);

		// Get sequence number
		long sequenceNumber = LoggerSettings.GetNextSequenceNumber();

		// Get thread ID
		int threadId = Environment.CurrentManagedThreadId;

		// Get file location
		string? fileLocation = null;
		if (LoggerSettings.FileLocation.Enabled && filePath != null)
			fileLocation = LoggerSettings.FileLocation.ShowFullPath
							   ? filePath
							   : Path.GetFileName(filePath);

		// Build formatted message using hierarchical structure:
		// Line 0 (optional): 🔄 [AsyncContext > Hierarchy]
		// Line 1: [#seq][timestamp][thread] severity [category] Message
		// Line 2 (optional):   └─ file :: caller

		var lines = new List<string>();

		// Async context line (if present)
		if (asyncHierarchy != null)
		{
			var asyncContextLine = $"🔄 [{string.Join(" > ", asyncHierarchy)}]";
			lines.Add(asyncContextLine);
		}

		// Build main line
		string mainLine = string.Empty;

		// Metadata section
		if (LoggerSettings.SequenceNumber.Enabled)
			mainLine = $"[#{sequenceNumber}]";

		if (LoggerSettings.Timestamp.Enabled)
			mainLine += $"[{now.ToString(LoggerSettings.Timestamp.Format)}]";

		if (LoggerSettings.FrameCount.Enabled && LoggerSettings.FrameCount.Provider != null)
		{
			int frameCount = LoggerSettings.FrameCount.Provider();
			mainLine += $"[F:{frameCount}]";
		}

		if (LoggerSettings.ThreadId.Enabled)
			mainLine += $"[T:{threadId}]";

		if (LoggerSettings.Grouping.IncludeInOutput && groupingContext != null)
			mainLine += $"[{groupingContext}]";

		// Add space after metadata if any exists
		if (!string.IsNullOrEmpty(mainLine))
			mainLine += " ";

		// Severity and category
		mainLine += severityEmoji;

		if (categoryEmoji != null)
			mainLine += $" [{categoryEmoji}]";

		// Exception type prefix
		if (exceptionType != null)
			mainLine += $" {exceptionType} ::";

		// Main message
		mainLine += $" {message}";

		lines.Add(mainLine);

		// Build details line (file and caller info)
		if (LoggerSettings.FileLocation.Enabled || callerInfo != null)
		{
			var details = new List<string>();

			if (LoggerSettings.FileLocation.Enabled && fileLocation != null)
				details.Add(fileLocation);

			if (callerInfo != null)
				details.Add(callerInfo);

			if (details.Count > 0)
			{
				var detailsLine = $"  └─ {string.Join(" :: ", details)}";
				lines.Add(detailsLine);
			}
		}

		// Combine all lines
		var formattedMessage = string.Join("\n", lines);

		var payload = new LogPayload
		{
			Timestamp             = now,
			Level                 = level,
			Category              = category,
			Message               = message,
			SeverityEmoji         = severityEmoji,
			CategoryEmoji         = categoryEmoji,
			ExceptionType         = exceptionType,
			CallerInfo            = callerInfo,
			FormattedMessage      = formattedMessage,
			ContextObject         = contextObject,
			StackTrace            = stackTrace,
			InnerExceptionType    = innerExceptionType,
			InnerExceptionMessage = innerExceptionMessage,
			GroupingContext       = groupingContext,
			AsyncContextHierarchy = asyncHierarchy,
			AsyncContext          = asyncHierarchy != null ? string.Join(" > ", asyncHierarchy) : null,
			AsyncContextDepth     = asyncDepth,
			Style                 = style
		};

		OnLog?.Invoke(payload);
	}
}
