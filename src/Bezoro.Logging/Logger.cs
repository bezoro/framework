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
	private static string? _lastStage;

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
			null,
			callerInfo,
			null,
			null,
			null,
			filePath
		);
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
		LogExceptionInternal(
			exception,
			LogLevel.Exception,
			customMessage,
			category,
			contextObject,
			captureCallerInfo,
			memberName,
			filePath
		);
	}


	/// <summary>
	///     Logs an error message with optional complexity.
	/// </summary>
	/// <param name="message">The message to log (string, number, object, FormattableString, etc.).</param>
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
	public static void LogError(
		object                     message,
		LogCategory?               category          = null,
		object?                    contextObject     = null,
		bool                       captureCallerInfo = false,
		[CallerMemberName] string? memberName        = null,
		[CallerFilePath]   string? filePath          = null) =>
		Log(message, LogLevel.Error, category, contextObject, captureCallerInfo, memberName, filePath);

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
	public static void LogException(
		Exception                  exception,
		string?                    customMessage     = null,
		LogCategory?               category          = null,
		object?                    contextObject     = null,
		bool                       captureCallerInfo = true,
		[CallerMemberName] string? memberName        = null,
		[CallerFilePath]   string? filePath          = null) =>
		LogExceptionInternal(
			exception,
			LogLevel.Exception,
			customMessage,
			category,
			contextObject,
			captureCallerInfo,
			memberName,
			filePath
		);


	/// <summary>
	///     Logs a success message with optional complexity.
	/// </summary>
	/// <param name="message">The message to log (string, number, object, FormattableString, etc.).</param>
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
	public static void LogSuccess(
		object                     message,
		LogCategory?               category          = null,
		object?                    contextObject     = null,
		bool                       captureCallerInfo = false,
		[CallerMemberName] string? memberName        = null,
		[CallerFilePath]   string? filePath          = null) =>
		Log(message, LogLevel.Success, category, contextObject, captureCallerInfo, memberName, filePath);

	/// <summary>
	///     Logs a warning message with optional complexity.
	/// </summary>
	/// <param name="message">The message to log (string, number, object, FormattableString, etc.).</param>
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
	public static void LogWarning(
		object                     message,
		LogCategory?               category          = null,
		object?                    contextObject     = null,
		bool                       captureCallerInfo = false,
		[CallerMemberName] string? memberName        = null,
		[CallerFilePath]   string? filePath          = null) =>
		Log(message, LogLevel.Warning, category, contextObject, captureCallerInfo, memberName, filePath);

	private static bool ShouldIncludeDetails(LogLevel level, string? callerInfo) =>
		callerInfo != null || level is LogLevel.Warning or LogLevel.Error or LogLevel.Exception;

	private static bool ShouldSkipLog(LogLevel level, LogCategory? category)
	{
		if (!LoggerSettings.Enabled)
			return true;

		if (level < MinimumLevel)
			return true;

		return category.HasValue && LoggerSettings.MutedCategories.Contains(category.Value);
	}

	private static bool TryUpdateStage(string? stage, out string? previousStage)
	{
		previousStage = null;

		if (stage == null)
			return false;

		string? currentStage = Volatile.Read(ref _lastStage);
		if (string.Equals(currentStage, stage, StringComparison.Ordinal))
			return false;

		Volatile.Write(ref _lastStage, stage);
		previousStage = currentStage;
		return previousStage != null;
	}

	private static string BuildFormattedMessage(
		string                 message,
		LogLevel               level,
		string                 severityEmoji,
		string?                categoryEmoji,
		string?                exceptionType,
		DateTime               timestamp,
		long                   sequenceNumber,
		int                    threadId,
		string?                groupingContext,
		IReadOnlyList<string>? asyncHierarchy,
		string?                fileLocation,
		string?                callerInfo)
	{
		// Build formatted message using hierarchical structure:
		// Line 0 (optional): 🔄 [AsyncContext > Hierarchy]
		// Line 1: [#seq timestamp F# T#] severity [category] Message
		// Line 2 (optional):   └─ file :: caller

		var lines = new List<string>();

		AddAsyncContextLine(lines, asyncHierarchy);
		lines.Add(
			BuildMainLine(
				message,
				severityEmoji,
				categoryEmoji,
				exceptionType,
				timestamp,
				sequenceNumber,
				threadId,
				groupingContext
			)
		);

		bool includeDetails = ShouldIncludeDetails(level, callerInfo);
		AddDetailsLine(lines, fileLocation, callerInfo, includeDetails);

		return string.Join("\n", lines);
	}

	private static string BuildMainLine(
		string   message,
		string   severityEmoji,
		string?  categoryEmoji,
		string?  exceptionType,
		DateTime timestamp,
		long     sequenceNumber,
		int      threadId,
		string?  groupingContext)
	{
		string metadata = BuildMetadataSection(timestamp, sequenceNumber, threadId, groupingContext);
		string mainLine = string.IsNullOrEmpty(metadata) ? string.Empty : $"{metadata} ";

		mainLine += severityEmoji;

		if (categoryEmoji != null)
			mainLine += $" [{categoryEmoji}]";

		if (exceptionType != null)
			mainLine += $" {exceptionType} ::";

		mainLine += $" {message}";

		return mainLine;
	}

	private static string BuildMetadataSection(
		DateTime timestamp,
		long     sequenceNumber,
		int      threadId,
		string?  groupingContext)
	{
		var parts = new List<string>();

		if (LoggerSettings.SequenceNumber.Enabled)
			parts.Add($"#{sequenceNumber}");

		if (LoggerSettings.Timestamp.Enabled)
			parts.Add(timestamp.ToString(LoggerSettings.Timestamp.Format));

		if (LoggerSettings.FrameCount.Enabled && LoggerSettings.FrameCount.Provider != null)
		{
			int frameCount = LoggerSettings.FrameCount.Provider();
			parts.Add($"F{frameCount}");
		}

		if (LoggerSettings.ThreadId.Enabled)
			parts.Add($"T{threadId}");

		if (LoggerSettings.Grouping.IncludeInOutput && groupingContext != null)
			parts.Add($"G:{groupingContext}");

		return parts.Count == 0 ? string.Empty : $"[{string.Join(" ", parts)}]";
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

	private static string? BuildGroupingContext(
		LoggerSettings.ContextGrouping grouping,
		DateTime                       now,
		string?                        filePath,
		string?                        callerInfo,
		LogCategory?                   category,
		LogLevel                       level,
		IReadOnlyList<string>?         asyncHierarchy) =>
		grouping switch
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

	private static string? GetFileLocation(string? filePath)
	{
		if (!LoggerSettings.FileLocation.Enabled || filePath == null)
			return null;

		return LoggerSettings.FileLocation.ShowFullPath
				   ? filePath
				   : Path.GetFileName(filePath);
	}

	private static string? GetStage()
	{
		if (!LoggerSettings.Stage.Enabled || LoggerSettings.Stage.Provider == null)
			return null;

		return LoggerSettings.Stage.Provider();
	}

	private static string? GetStageDividerLine(string? fromStage, string? toStage)
	{
		var dividerProvider = LoggerSettings.Stage.DividerProvider;
		if (dividerProvider == null)
			return null;

		string? dividerLine = dividerProvider(fromStage, toStage);
		return string.IsNullOrWhiteSpace(dividerLine) ? null : dividerLine;
	}

	private static string? NormalizeStage(string? stage)
	{
		if (string.IsNullOrWhiteSpace(stage))
			return null;

		return stage.Trim();
	}

	private static void AddAsyncContextLine(List<string> lines, IReadOnlyList<string>? asyncHierarchy)
	{
		if (asyncHierarchy == null)
			return;

		var asyncContextLine = $"🔄 [{string.Join(" > ", asyncHierarchy)}]";
		lines.Add(asyncContextLine);
	}

	private static void AddDetailsLine(
		List<string> lines,
		string?      fileLocation,
		string?      callerInfo,
		bool         includeDetails)
	{
		if (!includeDetails)
			return;

		if (!LoggerSettings.FileLocation.Enabled && callerInfo == null)
			return;

		var details = new List<string>();

		if (LoggerSettings.FileLocation.Enabled && fileLocation != null)
			details.Add(fileLocation);

		if (callerInfo != null)
			details.Add(callerInfo);

		if (details.Count == 0)
			return;

		var detailsLine = $"  └─ {string.Join(" :: ", details)}";
		lines.Add(detailsLine);
	}

	/// <summary>
	///     Builds the log payload and invokes the OnLog event.
	/// </summary>
	private static void BuildAndInvokePayload(
		string       message,
		LogLevel     level,
		LogCategory? category,
		object?      contextObject,
		Exception?   exception,
		string?      exceptionType,
		string?      callerInfo,
		string?      stackTrace,
		string?      innerExceptionType,
		string?      innerExceptionMessage,
		string?      filePath)
	{
		if (ShouldSkipLog(level, category)) return;

		string? stage           = GetStage();
		string? normalizedStage = NormalizeStage(stage);
		bool    stageChanged    = TryUpdateStage(normalizedStage, out string? previousStage);

		string severityEmoji = LogLevelEmoji.GetEmoji(level);
		string? categoryEmoji = category.HasValue
									? LogCategoryEmoji.GetEmoji(category.Value)
									: null;

		// Get async context info
		var asyncHierarchy = LoggerSettings.CurrentAsyncContextHierarchy;
		int asyncDepth     = asyncHierarchy?.Count ?? 0;

		TryEmitStageDividerPayload(
			level,
			category,
			contextObject,
			previousStage,
			normalizedStage,
			stageChanged,
			asyncHierarchy
		);

		// Determine grouping context
		var now = DateTime.UtcNow;
		string? groupingContext = BuildGroupingContext(
			LoggerSettings.Grouping.GroupBy,
			now,
			filePath,
			callerInfo,
			category,
			level,
			asyncHierarchy
		);

		// Get style for this log level
		var style = LoggerSettings.GetStyle(level);

		// Get sequence number
		long sequenceNumber = LoggerSettings.GetNextSequenceNumber();

		// Get thread ID
		int threadId = Environment.CurrentManagedThreadId;

		string? fileLocation = GetFileLocation(filePath);

		string formattedMessage = BuildFormattedMessage(
			message,
			level,
			severityEmoji,
			categoryEmoji,
			exceptionType,
			now,
			sequenceNumber,
			threadId,
			groupingContext,
			asyncHierarchy,
			fileLocation,
			callerInfo
		);

		var payload = new LogPayload
		{
			Timestamp             = now,
			Level                 = level,
			Category              = category,
			Message               = message,
			SeverityEmoji         = severityEmoji,
			CategoryEmoji         = categoryEmoji,
			Exception             = exception,
			ExceptionType         = exceptionType,
			CallerInfo            = callerInfo,
			Stage                 = normalizedStage,
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

	private static void EmitStageDividerPayload(
		string                 dividerLine,
		LogLevel               level,
		LogCategory?           category,
		object?                contextObject,
		string?                stage,
		IReadOnlyList<string>? asyncHierarchy)
	{
		var payload = new LogPayload
		{
			Timestamp             = DateTime.UtcNow,
			Level                 = LogLevel.Divider,
			Category              = LogCategory.None,
			Message               = dividerLine,
			SeverityEmoji         = LogLevelEmoji.GetEmoji(level),
			CategoryEmoji         = category.HasValue ? LogCategoryEmoji.GetEmoji(category.Value) : null,
			Exception             = null,
			ExceptionType         = null,
			CallerInfo            = null,
			Stage                 = stage,
			FormattedMessage      = dividerLine,
			ContextObject         = contextObject,
			StackTrace            = null,
			InnerExceptionType    = null,
			InnerExceptionMessage = null,
			GroupingContext       = null,
			AsyncContextHierarchy = asyncHierarchy,
			AsyncContext          = asyncHierarchy != null ? string.Join(" > ", asyncHierarchy) : null,
			AsyncContextDepth     = asyncHierarchy?.Count ?? 0,
			Style                 = LoggerSettings.GetStyle(level)
		};

		OnLog?.Invoke(payload);
	}

	private static void LogExceptionInternal(
		Exception    exception,
		LogLevel     level,
		string?      customMessage,
		LogCategory? category,
		object?      contextObject,
		bool         captureCallerInfo,
		string?      memberName,
		string?      filePath)
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
			level,
			category,
			contextObject,
			exception,
			exceptionType,
			callerInfo,
			stackTrace,
			innerExceptionType,
			innerExceptionMessage,
			filePath
		);
	}

	private static void TryEmitStageDividerPayload(
		LogLevel               level,
		LogCategory?           category,
		object?                contextObject,
		string?                previousStage,
		string?                stage,
		bool                   stageChanged,
		IReadOnlyList<string>? asyncHierarchy)
	{
		if (!stageChanged)
			return;

		string? dividerLine = GetStageDividerLine(previousStage, stage);
		if (dividerLine == null)
			return;

		EmitStageDividerPayload(dividerLine, level, category, contextObject, stage, asyncHierarchy);
	}
}
