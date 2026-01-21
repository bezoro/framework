namespace Bezoro.UCI.Domain;

/// <summary>
///     Defines default constants for ProcessUciTransport configuration values.
/// </summary>
internal static class ProcessUciTransportConstants
{
	/// <summary>
	///     Default capacity for async channels used for message buffering.
	/// </summary>
	public const int DEFAULT_CHANNEL_CAPACITY = 1024;

	/// <summary>
	///     Default number of writes to batch before flushing stdin.
	/// </summary>
	public const int DEFAULT_FLUSH_BATCH_SIZE = 8;

	/// <summary>
	///     Default maximum allowed length for a command line in characters (10MB).
	/// </summary>
	public const int DEFAULT_MAX_LINE_LENGTH = 10 * 1024 * 1024;

	/// <summary>
	///     Default grace period in milliseconds to wait after sending "quit" before forcing termination.
	/// </summary>
	public const int DEFAULT_QUIT_GRACE_PERIOD_MS = 500;

	/// <summary>
	///     Default number of spin iterations for very short timeouts in TryWriteLineAsync.
	/// </summary>
	public const int DEFAULT_SMALL_TIMEOUT_SPIN_ITERATIONS = 3;

	/// <summary>
	///     Default timeout in seconds for teardown and wait operations.
	/// </summary>
	public const int DEFAULT_TEARDOWN_TIMEOUT_SECONDS = 5;

	/// <summary>
	///     Default line length threshold in characters at which a warning will be logged (1MB).
	/// </summary>
	public const int DEFAULT_WARN_LINE_LENGTH = 1024 * 1024;

	/// <summary>
	///     Default timeout in seconds before logging a warning that WriteLineAsync is blocked waiting for channel space.
	/// </summary>
	public const int DEFAULT_WRITE_HANG_WARNING_SECONDS = 5;
}
