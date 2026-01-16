using System.Collections;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Bezoro.Logging;

/// <summary>
///     Provides utility methods to get emoji representations for log categories.
/// </summary>
public static class LogCategoryEmoji
{
	private static readonly Dictionary<LogCategory, string> CategoryToEmoji = new()
	{
		// Core
		{ LogCategory.Default, "🔄" },
		{ LogCategory.System, "⚙️" },
		{ LogCategory.Other, "❓" },

		// Development & Debugging
		{ LogCategory.Debug, "🐞" },
		{ LogCategory.Test, "🧪" },
		{ LogCategory.Profiling, "📊" },
		{ LogCategory.Editor, "🔧" },
		{ LogCategory.Analytics, "📈" },
		{ LogCategory.Performance, "⚡" },

		// System Core
		{ LogCategory.Memory, "🧠" },
		{ LogCategory.Security, "🔒" },
		{ LogCategory.Database, "💾" },
		{ LogCategory.FileIO, "📁" },
		{ LogCategory.Configuration, "⚙️" },
		{ LogCategory.Loading, "⏳" },
		{ LogCategory.SaveSystem, "💾" },
		{ LogCategory.Resources, "📦" },
		{ LogCategory.AssetBundle, "📦" },
		{ LogCategory.SceneManagement, "🏙️" },

		// Rendering & Graphics
		{ LogCategory.Rendering, "🎨" },
		{ LogCategory.Particles, "✨" },
		{ LogCategory.Shaders, "🔆" },
		{ LogCategory.PostProcessing, "🖼️" },
		{ LogCategory.Lighting, "💡" },

		// Game Systems
		{ LogCategory.Gameplay, "🎮" },
		{ LogCategory.Combat, "⚔️" },
		{ LogCategory.Inventory, "🎒" },
		{ LogCategory.Quest, "📜" },
		{ LogCategory.Dialog, "💬" },
		{ LogCategory.LevelGeneration, "🏗️" },
		{ LogCategory.Progression, "📈" },
		{ LogCategory.Achievement, "🏆" },
		{ LogCategory.Economy, "💰" },

		// Input & UI
		{ LogCategory.Input, "🎮" },
		{ LogCategory.UI, "🖥️" },

		// Animation & Physics
		{ LogCategory.Animation, "🏃" },
		{ LogCategory.Physics, "🔮" },
		{ LogCategory.Physics2D, "🎯" },

		// AI & Behavior
		{ LogCategory.AI, "🧠" },

		// Audio
		{ LogCategory.Audio, "🔊" },

		// Networking & Services
		{ LogCategory.Network, "🌐" },
		{ LogCategory.Cloud, "☁️" },
		{ LogCategory.Authentication, "🔑" },
		{ LogCategory.Social, "👥" },
		{ LogCategory.Purchasing, "💲" },

		// Localization
		{ LogCategory.Localization, "🌍" },

		// Other
		{ LogCategory.Camera, "📷" },
		{ LogCategory.Initialization, "🚀" },
		{ LogCategory.Utilities, "🔧" },
		{ LogCategory.UCI, "♟️" }
	};

	/// <summary>
	///     Gets the emoji representation for a specific log category.
	/// </summary>
	/// <param name="category">The log category.</param>
	/// <returns>
	///     Emoji string for the specified category, or <c>❓</c> if the category is not recognized.
	/// </returns>
	public static string GetEmoji(LogCategory category) =>
		CategoryToEmoji.GetValueOrDefault(category, "❓");
}

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

	/// <summary>
	///     Performance timer struct for zero-allocation timing.
	///     Fully compiled away in release builds for true zero overhead.
	/// </summary>
	public readonly struct PerformanceTimer : IDisposable
	{
		/// <summary>
		///     Initializes a new instance of the <see cref="PerformanceTimer" /> struct and starts timing.
		/// </summary>
		/// <param name="operationName">The name of the operation being timed.</param>
		/// <param name="category">Optional log category for the completion message.</param>
		/// <param name="contextObject">Optional context object to associate with the log entry.</param>
		public PerformanceTimer(string operationName, LogCategory? category, object? contextObject)
		{
#if DEBUG
			_operationName  = operationName;
			_category       = category;
			_contextObject  = contextObject;
			_startTimestamp = Stopwatch.GetTimestamp();
#endif
		}

		/// <summary>
		///     Stops the timer and logs the elapsed time for the operation.
		///     Fully compiled away in release builds.
		/// </summary>
		public void Dispose()
		{
#if DEBUG
			long   elapsed   = Stopwatch.GetTimestamp() - _startTimestamp;
			double elapsedMs = elapsed * 1000.0 / Stopwatch.Frequency;

			var message = $"{_operationName} completed in {elapsedMs:F2}ms";

			Log(message, LogLevel.Info, _category, _contextObject);
#endif
		}
#if DEBUG
		private readonly string       _operationName;
		private readonly LogCategory? _category;
		private readonly object?      _contextObject;
		private readonly long         _startTimestamp;
#endif
	}
}

/// <summary>
///     Configuration settings for the Logger.
/// </summary>
public static class LoggerSettings
{
	/// <summary>
	///     AsyncLocal stack that tracks async context hierarchy.
	///     Automatically flows through async/await boundaries.
	/// </summary>
	private static readonly AsyncLocal<Stack<string>?> AsyncContextStack = new();

	/// <summary>
	///     Log sequence counter for tracking log order.
	/// </summary>
	private static long _sequenceNumber;

	/// <summary>
	///     Categories that should be ignored/muted.
	/// </summary>
	/// <remarks>
	///     <para>
	///         Use <see cref="HashSet{T}.Add(T)" /> to add a category to the list.
	///     </para>
	///     <para>
	///         Use <see cref="HashSet{T}.Remove(T)" /> to remove a category from the list.
	///     </para>
	///     <para>
	///         Use <see cref="HashSet{T}.Clear" /> to clear the list.
	///     </para>
	/// </remarks>
	public static HashSet<LogCategory> MutedCategories { get; } = [];


	/// <summary>
	///     Master switch to enable/disable all logging at runtime.
	/// </summary>
	public static bool Enabled { get; set; } = true;

	/// <summary>
	///     File location metadata configuration.
	/// </summary>
	public static FileLocationConfig FileLocation { get; set; } = FileLocationConfig.FullPath;

	/// <summary>
	///     Frame count metadata configuration.
	/// </summary>
	public static FrameCountConfig FrameCount { get; set; } = FrameCountConfig.Disabled;

	/// <summary>
	///     Grouping configuration.
	/// </summary>
	public static GroupingConfig Grouping { get; set; } = GroupingConfig.None;

	/// <summary>
	///     Visual style for Error level logs.
	/// </summary>
	public static LogStyle ErrorStyle { get; set; } = new(ConsoleColor.Red);

	/// <summary>
	///     Visual style for Exception level logs.
	/// </summary>
	public static LogStyle ExceptionStyle { get; set; } = new(ConsoleColor.DarkRed, true);

	/// <summary>
	///     Visual style for Info level logs.
	/// </summary>
	public static LogStyle InfoStyle { get; set; } = new(ConsoleColor.White);

	/// <summary>
	///     Visual style for Success level logs.
	/// </summary>
	public static LogStyle SuccessStyle { get; set; } = new(ConsoleColor.Green);

	/// <summary>
	///     Visual style for Warning level logs.
	/// </summary>
	public static LogStyle WarningStyle { get; set; } = new(ConsoleColor.Yellow);

	/// <summary>
	///     Sequence number metadata configuration.
	/// </summary>
	public static SequenceNumberConfig SequenceNumber { get; set; } = SequenceNumberConfig.On;

	/// <summary>
	///     Thread ID metadata configuration.
	/// </summary>
	public static ThreadIdConfig ThreadId { get; set; } = ThreadIdConfig.On;

	/// <summary>
	///     Timestamp metadata configuration.
	/// </summary>
	public static TimestampConfig Timestamp { get; set; } = TimestampConfig.Default;

	/// <summary>
	///     Gets the current async context hierarchy.
	/// </summary>
	internal static IReadOnlyList<string>? CurrentAsyncContextHierarchy
	{
		get
		{
			var stack = AsyncContextStack.Value;
			return stack?.Count > 0 ? stack.Reverse().ToArray() : null;
		}
	}

	/// <summary>
	///     Begins a new async context that automatically flows through async/await.
	/// </summary>
	/// <param name="contextName">The name for this async context.</param>
	/// <returns>A disposable that pops the context when disposed.</returns>
	public static IDisposable BeginAsyncContext(string contextName) => new AsyncContextScope(contextName);

	/// <summary>
	///     Gets the style for a specific log level.
	/// </summary>
	internal static LogStyle GetStyle(LogLevel level) => level switch
	{
		LogLevel.Info      => InfoStyle,
		LogLevel.Success   => SuccessStyle,
		LogLevel.Warning   => WarningStyle,
		LogLevel.Error     => ErrorStyle,
		LogLevel.Exception => ExceptionStyle,
		_                  => InfoStyle
	};

	/// <summary>
	///     Gets the next sequence number for log ordering.
	/// </summary>
	internal static long GetNextSequenceNumber() => Interlocked.Increment(ref _sequenceNumber);


	/// <summary>
	///     Defines how logs should be grouped/contextualized.
	/// </summary>
	public enum ContextGrouping
	{
		/// <summary>No automatic grouping.</summary>
		None,

		/// <summary>Group by calling class name.</summary>
		CallerType,

		/// <summary>Group by calling method name (ClassName.MethodName).</summary>
		CallerMethod,

		/// <summary>Group by log category.</summary>
		Category,

		/// <summary>Group by thread ID.</summary>
		Thread,

		/// <summary>Group by log severity level.</summary>
		Level,

		/// <summary>Group by time window (logs within TimeWindowMs are grouped together).</summary>
		TimeWindow,

		/// <summary>Group by async context (automatically flows through async/await).</summary>
		AsyncContext
	}

	/// <summary>
	///     Internal scope for managing async context stack.
	/// </summary>
	private sealed class AsyncContextScope : IDisposable
	{
		public AsyncContextScope(string contextName)
		{
			var stack = AsyncContextStack.Value;
			if (stack == null)
			{
				stack                   = new();
				AsyncContextStack.Value = stack;
			}

			stack.Push(contextName);
		}

		public void Dispose()
		{
			var stack = AsyncContextStack.Value;
			if (stack?.Count > 0) stack.Pop();
		}
	}
}

/// <summary>
///     Provides utility methods to get emoji representations for log severity levels.
/// </summary>
public static class LogLevelEmoji
{
	private static readonly Dictionary<LogLevel, string> LevelToEmoji = new()
	{
		{ LogLevel.Info, "ℹ️" },
		{ LogLevel.Success, "✅" },
		{ LogLevel.Warning, "⚠️" },
		{ LogLevel.Error, "❌" },
		{ LogLevel.Exception, "💥" }
	};

	/// <summary>
	///     Gets the emoji representation for a specific log level.
	/// </summary>
	/// <param name="level">The log level.</param>
	/// <returns>
	///     Emoji string for the specified level, or <c>ℹ️</c> if not recognized.
	/// </returns>
	public static string GetEmoji(LogLevel level) =>
		LevelToEmoji.GetValueOrDefault(level, "ℹ️");
}

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

/// <summary>
///     Defines visual styling for log output.
/// </summary>
public sealed class LogStyle
{
	/// <summary>
	///     Initializes a new instance of the <see cref="LogStyle" /> class with the specified visual styling options.
	/// </summary>
	/// <param name="color">The console color to use for this log style.</param>
	/// <param name="bold">Whether to render the text in bold. Default is <c>false</c>.</param>
	/// <param name="italic">Whether to render the text in italic. Default is <c>false</c>.</param>
	public LogStyle(ConsoleColor color, bool bold = false, bool italic = false)
	{
		Color  = color;
		Bold   = bold;
		Italic = italic;
	}

	/// <summary>
	///     Whether to render the text in bold.
	/// </summary>
	public bool Bold { get; init; }

	/// <summary>
	///     Whether to render the text in italic.
	/// </summary>
	public bool Italic { get; init; }

	/// <summary>
	///     The color hint for this log style.
	/// </summary>
	public ConsoleColor Color { get; init; }
}

/// <summary>
///     Specifies the category of a log message for classification and filtering.
/// </summary>
public enum LogCategory
{
	// Core

	/// <summary>
	///     Default/general log category.
	/// </summary>
	Default,

	/// <summary>
	///     System-level log messages.
	/// </summary>
	System,

	/// <summary>
	///     Other uncategorized log messages.
	/// </summary>
	Other,

	// Development & Debugging

	/// <summary>
	///     Messages related to debugging.
	/// </summary>
	Debug,

	/// <summary>
	///     Messages related to tests.
	/// </summary>
	Test,

	/// <summary>
	///     Profiling or performance-analysis messages.
	/// </summary>
	Profiling,

	/// <summary>
	///     Editor-specific messages.
	/// </summary>
	Editor,

	/// <summary>
	///     Analytics and telemetry messages.
	/// </summary>
	Analytics,

	/// <summary>
	///     Performance marker messages.
	/// </summary>
	Performance,

	// System Core

	/// <summary>
	///     Memory-related messages.
	/// </summary>
	Memory,

	/// <summary>
	///     Security, authorization, or permission-related messages.
	/// </summary>
	Security,

	/// <summary>
	///     Database access or query messages.
	/// </summary>
	Database,

	/// <summary>
	///     File input/output messages.
	/// </summary>
	FileIO,

	/// <summary>
	///     Configuration or settings-related log messages.
	/// </summary>
	Configuration,

	/// <summary>
	///     Loading processes and progress updates.
	/// </summary>
	Loading,

	/// <summary>
	///     Save/load system-related log messages.
	/// </summary>
	SaveSystem,

	/// <summary>
	///     Asset and resource management messages.
	/// </summary>
	Resources,

	/// <summary>
	///     Asset bundle-related log category.
	/// </summary>
	AssetBundle,

	/// <summary>
	///     Scene management and transitions.
	/// </summary>
	SceneManagement,

	// Rendering & Graphics

	/// <summary>
	///     Rendering and visual graphics messages.
	/// </summary>
	Rendering,

	/// <summary>
	///     Particle system messages.
	/// </summary>
	Particles,

	/// <summary>
	///     Shader and graphics pipeline messages.
	/// </summary>
	Shaders,

	/// <summary>
	///     Post-processing and image effect messages.
	/// </summary>
	PostProcessing,

	/// <summary>
	///     Lighting engine or lighting effect messages.
	/// </summary>
	Lighting,

	// Game Systems

	/// <summary>
	///     Gameplay-specific or mechanics messages.
	/// </summary>
	Gameplay,

	/// <summary>
	///     Combat system messages.
	/// </summary>
	Combat,

	/// <summary>
	///     Inventory system messages.
	/// </summary>
	Inventory,

	/// <summary>
	///     Quest system messages.
	/// </summary>
	Quest,

	/// <summary>
	///     Dialogue system messages.
	/// </summary>
	Dialog,

	/// <summary>
	///     Procedural or dynamic level generation messages.
	/// </summary>
	LevelGeneration,

	/// <summary>
	///     Player or system progression-related messages.
	/// </summary>
	Progression,

	/// <summary>
	///     Achievement system messages.
	/// </summary>
	Achievement,

	/// <summary>
	///     Economy or in-game currency messages.
	/// </summary>
	Economy,

	// Input & UI

	/// <summary>
	///     Player or system input messages.
	/// </summary>
	Input,

	/// <summary>
	///     User interface/in-game UI messages.
	/// </summary>
	UI,

	// Animation & Physics

	/// <summary>
	///     Animation subsystem messages.
	/// </summary>
	Animation,

	/// <summary>
	///     Physics engine or simulation messages.
	/// </summary>
	Physics,

	/// <summary>
	///     2D physics system messages.
	/// </summary>
	Physics2D,

	// AI & Behavior

	/// <summary>
	///     Artificial intelligence or behavior-tree messages.
	/// </summary>
	AI,

	// Audio

	/// <summary>
	///     Audio subsystem messages.
	/// </summary>
	Audio,

	// Networking & Services

	/// <summary>
	///     Networking, communication, or multiplayer messages.
	/// </summary>
	Network,

	/// <summary>
	///     Cloud service/messaging messages.
	/// </summary>
	Cloud,

	/// <summary>
	///     Authentication and login messages.
	/// </summary>
	Authentication,

	/// <summary>
	///     Online services, friends, or social integration messages.
	/// </summary>
	Social,

	/// <summary>
	///     Purchasing, microtransactions, or commerce system messages.
	/// </summary>
	Purchasing,

	// Localization

	/// <summary>
	///     Localization, language, or translation-related messages.
	/// </summary>
	Localization,

	// Other

	/// <summary>
	///     Camera subsystem or visual frustum messages.
	/// </summary>
	Camera,

	/// <summary>
	///     Game/system initialization messages.
	/// </summary>
	Initialization,

	/// <summary>
	///     Utility and helper methods or modules.
	/// </summary>
	Utilities,

	/// <summary>
	///     Universal Chess Interface or other application-specific protocols.
	/// </summary>
	UCI
}

/// <summary>
///     Configuration for file location metadata.
/// </summary>
public readonly struct FileLocationConfig
{
	/// <summary>
	///     Disabled file location configuration.
	/// </summary>
	public static FileLocationConfig Disabled => new() { Enabled = false, ShowFullPath = false };

	/// <summary>
	///     Creates an enabled file location configuration (filename only).
	/// </summary>
	public static FileLocationConfig FilenameOnly => new() { Enabled = true, ShowFullPath = false };

	/// <summary>
	///     Creates an enabled file location configuration (full path).
	/// </summary>
	public static FileLocationConfig FullPath => new() { Enabled = true, ShowFullPath = true };

	/// <summary>
	///     Whether file location is enabled.
	/// </summary>
	public bool Enabled { get; init; }

	/// <summary>
	///     Whether to show full path or just filename.
	/// </summary>
	public bool ShowFullPath { get; init; }
}

/// <summary>
///     Configuration for frame count metadata.
/// </summary>
public readonly struct FrameCountConfig
{
	/// <summary>
	///     Creates an enabled frame count configuration with the specified provider.
	/// </summary>
	/// <param name="provider">Function that returns the current frame count (e.g., () => Time.frameCount in Unity).</param>
	public static FrameCountConfig Create(Func<int> provider) => new() { Enabled = true, Provider = provider };

	/// <summary>
	///     Disabled frame count configuration.
	/// </summary>
	public static FrameCountConfig Disabled => new() { Enabled = false, Provider = null };

	/// <summary>
	///     Whether frame count is enabled.
	/// </summary>
	public bool Enabled { get; init; }

	/// <summary>
	///     Provider function that returns the current frame count.
	/// </summary>
	public Func<int>? Provider { get; init; }
}

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

/// <summary>
///     Configuration for sequence number metadata.
/// </summary>
public readonly struct SequenceNumberConfig
{
	/// <summary>
	///     Disabled sequence number configuration.
	/// </summary>
	public static SequenceNumberConfig Off => new() { Enabled = false };

	/// <summary>
	///     Enabled sequence number configuration.
	/// </summary>
	public static SequenceNumberConfig On => new() { Enabled = true };

	/// <summary>
	///     Whether sequence number is enabled.
	/// </summary>
	public bool Enabled { get; init; }
}

/// <summary>
///     Configuration for thread ID metadata.
/// </summary>
public readonly struct ThreadIdConfig
{
	/// <summary>
	///     Disabled thread ID configuration.
	/// </summary>
	public static ThreadIdConfig Off => new() { Enabled = false };

	/// <summary>
	///     Enabled thread ID configuration.
	/// </summary>
	public static ThreadIdConfig On => new() { Enabled = true };

	/// <summary>
	///     Whether thread ID is enabled.
	/// </summary>
	public bool Enabled { get; init; }
}

/// <summary>
///     Configuration for timestamp metadata.
/// </summary>
public readonly struct TimestampConfig
{
	/// <summary>
	///     Creates an enabled timestamp configuration with custom format.
	/// </summary>
	/// <param name="format">Custom DateTime format string.</param>
	public static TimestampConfig Create(string format) => new() { Enabled = true, Format = format };

	/// <summary>
	///     Creates an enabled timestamp configuration with default format.
	/// </summary>
	public static TimestampConfig Default => new() { Enabled = true, Format = "HH:mm:ss.fff" };

	/// <summary>
	///     Disabled timestamp configuration.
	/// </summary>
	public static TimestampConfig Disabled => new() { Enabled = false, Format = string.Empty };

	/// <summary>
	///     Whether timestamp is enabled.
	/// </summary>
	public bool Enabled { get; init; }

	/// <summary>
	///     The timestamp format string (default: "HH:mm:ss.fff").
	/// </summary>
	public string Format { get; init; }
}
