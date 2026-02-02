namespace Bezoro.Logging.Types;

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
	public static FileLocationConfig FileLocation { get; set; } = FileLocationConfig.FilenameOnly;

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
	///     Stage metadata configuration.
	/// </summary>
	public static StageConfig Stage { get; set; } = StageConfig.Disabled;

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
