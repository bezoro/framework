using System.Text;

namespace Bezoro.UCI.Domain;

internal sealed class ProcessUciTransportOptions
{
	// When true (and supported by the runtime), attempts to kill the entire process tree on forced termination.
	public bool KillEntireProcessTree { get; init; } = false;

	// If only a single producer calls WriteLine/TryWriteLine concurrently, set to true for slightly lower overhead.
	public bool OutgoingSingleWriter { get; init; } = false;

	// Redirect stderr only when explicitly requested to minimize overhead.
	public bool RedirectStandardError { get; init; }
	public bool SendQuitOnDispose     { get; init; } = true;
	public bool SendQuitOnStop        { get; init; } = true;

	public bool SingleReader { get; init; } = true;

	// When false, skip command validation for maximum throughput on hot paths (WriteLine/TryWriteLine).
	public bool      ValidateCommands { get; init; } = true;
	public Encoding? StderrEncoding   { get; init; }

	public Encoding? StdinEncoding { get; init; }

	// Optional encodings; when null, platform defaults are used.
	public Encoding? StdoutEncoding { get; init; }

	public int ChannelCapacity { get; init; } = 1024;

	// Number of writes to batch before flushing stdin; tune for latency/throughput.
	public int FlushBatchSize { get; init; } = 8;

	public int QuitGracePeriodMs { get; init; } = 500;

	// Spin iterations used for very small timeouts in TryWriteLineAsync; set to 0 to disable spinning.
	public int SmallTimeoutSpinIterations { get; init; } = 3;

	// Optional logger for lifecycle and error events.
	public IUciTransportLogger? Logger { get; init; }

	public string NewLine { get; init; } = Environment.NewLine;

	// Preferred time-based grace period; when default (zero), QuitGracePeriodMs is used.
	public TimeSpan QuitGracePeriod { get; init; } = TimeSpan.Zero;

	// Bounded timeout for teardown waits (joining loops and waiting for process exit).
	public TimeSpan TeardownTimeout { get; init; } = TimeSpan.FromSeconds(5);

	// NOTE: Test-only hook to deterministically exercise backpressure paths. Defaults to false; no prod impact.
	internal bool DisableWriteLoop { get; init; } = false;
}
