namespace Bezoro.UCI;

/// <summary>
///     Optional logger abstraction for UCI transport lifecycle and error reporting.
///     Implementations should be non-throwing.
/// </summary>
internal interface IUciTransportLogger
{
	/// <summary>Logs verbose diagnostics useful for troubleshooting and tests.</summary>
	void LogDebug(string message);

	/// <summary>Logs errors together with the associated exception.</summary>
	void LogError(Exception exception, string message);

	/// <summary>Logs informational lifecycle events (start/stop/dispose and key transitions).</summary>
	void LogInfo(string message);
}
