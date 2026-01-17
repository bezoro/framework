using System.Diagnostics;
using Bezoro.Logging.Types;

namespace Bezoro.Logging.Utilities;

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

		Logger.Log(message, LogLevel.Info, _category, _contextObject);
#endif
	}

#if DEBUG
	private readonly string       _operationName;
	private readonly LogCategory? _category;
	private readonly object?      _contextObject;
	private readonly long         _startTimestamp;
#endif
}
