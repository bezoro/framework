using System.Text;

namespace Bezoro.UCI.Domain;

/// <summary>
///     Options for controlling process-based UCI transport behavior and performance.
/// </summary>
internal sealed class ProcessUciTransportOptions
{
	/// <summary>
	///     When true (and supported by the runtime), attempts to kill the entire process tree on forced termination.
	/// </summary>
	public bool KillEntireProcessTree { get; init; } = false;

	/// <summary>
	///     If only a single producer calls WriteLine/TryWriteLine concurrently, set to true for slightly lower overhead.
	/// </summary>
	public bool OutgoingSingleWriter { get; init; } = false;

	/// <summary>
	///     Redirect stderr only when explicitly requested to minimize overhead.
	/// </summary>
	public bool RedirectStandardError { get; init; }

	/// <summary>
	///     When disposing, send "quit" to the engine before shutdown.
	/// </summary>
	public bool SendQuitOnDispose { get; init; } = true;

	/// <summary>
	///     When stopping the transport, send "quit" to the engine.
	/// </summary>
	public bool SendQuitOnStop { get; init; } = true;

	/// <summary>
	///     If only a single reader consumes lines, set to true for lower overhead; disables multithreaded demux.
	/// </summary>
	public bool SingleReader { get; init; } = true;

	/// <summary>
	///     By default, validates engine commands for correctness and protocol compliance.
	/// </summary>
	public bool ValidateCommands { get; init; } = true;

	/// <summary>
	///     Custom encoding to use for reading engine stderr output; if null, use platform default.
	/// </summary>
	public Encoding? StderrEncoding { get; init; }

	/// <summary>
	///     Custom encoding to use for writing to engine stdin; if null, use platform default.
	/// </summary>
	public Encoding? StdinEncoding { get; init; }

	/// <summary>
	///     Custom encoding to use for reading engine stdout output; if null, use platform default.
	/// </summary>
	public Encoding? StdoutEncoding { get; init; }

	/// <summary>
	///     Capacity of the async channel used for outgoing messages to the engine. Tune for high-throughput scenarios.
	/// </summary>
	public int ChannelCapacity { get; init; } = ProcessUciTransportConstants.DEFAULT_CHANNEL_CAPACITY;

	/// <summary>
	///     Number of writes to batch before flushing stdin; increases throughput at the potential cost of latency.
	/// </summary>
	public int FlushBatchSize { get; init; } = ProcessUciTransportConstants.DEFAULT_FLUSH_BATCH_SIZE;

	/// <summary>
	///     Grace period in milliseconds to wait after sending "quit" before forcing termination, when
	///     <see cref="QuitGracePeriod" /> is not set.
	/// </summary>
	public int QuitGracePeriodMs { get; init; } = ProcessUciTransportConstants.DEFAULT_QUIT_GRACE_PERIOD_MS;

	/// <summary>
	///     Number of spin iterations for very short timeouts in TryWriteLineAsync; set to 0 to disable busy spinning.
	/// </summary>
	public int SmallTimeoutSpinIterations { get; init; } =
		ProcessUciTransportConstants.DEFAULT_SMALL_TIMEOUT_SPIN_ITERATIONS;

	/// <summary>
	///     Newline sequence used when writing engine commands; defaults to <see cref="Environment.NewLine" />.
	/// </summary>
	public string NewLine { get; init; } = Environment.NewLine;

	/// <summary>
	///     Time-based preferred grace period to wait after sending "quit"; if zero, <see cref="QuitGracePeriodMs" /> is used.
	/// </summary>
	public TimeSpan QuitGracePeriod { get; init; } = TimeSpan.Zero;

	/// <summary>
	///     Bounded timeout for teardown and wait operations (such as waiting for process exit or joining async loops).
	/// </summary>
	public TimeSpan TeardownTimeout { get; init; } =
		TimeSpan.FromSeconds(ProcessUciTransportConstants.DEFAULT_TEARDOWN_TIMEOUT_SECONDS);

	/// <summary>
	///     Test-only callback invoked when "quit" command is sent to the process. Used for verification in tests.
	/// </summary>
	internal Action? OnQuitSent { get; init; }

	/// <summary>
	///     Test-only hook to deterministically exercise backpressure code-paths. Defaults to false; no production impact.
	/// </summary>
	internal bool DisableWriteLoop { get; init; } = false;
}
