using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Bezoro.UCI.Domain.Common.Helpers;

namespace Bezoro.UCI.Domain;

internal sealed class ProcessUciTransport : IUciTransport
{
	private readonly BackgroundLoopManager      _loopManager;
	private readonly BackgroundLoopMetrics      _metrics;
	private readonly GateManager                _gateManager;
	private readonly IReadOnlyList<string>?     _args;
	private readonly ProcessStreams             _streams;
	private readonly ProcessUciTransportOptions _options;
	private readonly string                     _path;
	private readonly string?                    _workingDirectory;
	private readonly TransportStateManager      _stateManager;

	private Channel<string>? _lines;
	private Channel<string>? _outgoing;
	private Process?         _process;

	public event Action<Exception>?     Error;
	public event Action<int?, string?>? Exited;
	public event Action<string>?        StderrReceived;

	public ProcessUciTransport(string path, IEnumerable<string>? args = null, string? workingDirectory = null)
		: this(path, args, workingDirectory, null) { }

	public ProcessUciTransport(
		string                      path,
		IEnumerable<string>?        args,
		string?                     workingDirectory,
		ProcessUciTransportOptions? options)
	{
		if (string.IsNullOrWhiteSpace(path))
			throw new ArgumentException("Engine path must be provided.", nameof(path));

		_path             = path;
		_workingDirectory = workingDirectory;
		_args             = args is null ? null : ValidateAndCreateArgsList(args);
		_options          = options ?? new ProcessUciTransportOptions();

		ProcessUciTransportValidator.ValidateOptions(_options);

		_metrics      = new();
		_stateManager = new();
		_gateManager  = new();
		_streams      = new();
		_loopManager = new(
			_options,
			ReportError,
			_metrics,
			line => StderrReceived?.Invoke(line));
	}

	public bool            IsHealthy          => IsStarted && ProcessIsAlive() && _loopManager.AreLoopsHealthy();
	public bool            IsStarted          => _stateManager.IsStarted;
	public long            BackpressureEvents => _metrics.BackpressureEvents;
	public long            LinesRead          => _metrics.LinesRead;
	public long            LinesWritten       => _metrics.LinesWritten;
	public TransportStatus Status             => _stateManager.Status;

	/// <summary>
	/// Asynchronously reads lines from the UCI engine's stdout.
	/// </summary>
	/// <param name="ct">Cancellation token to stop reading.</param>
	/// <returns>An async enumerable of lines read from the engine.</returns>
	/// <exception cref="InvalidOperationException">Thrown if transport is not started or is disposed.</exception>
	/// <remarks>
	/// If SingleReader option is true, only one concurrent reader is allowed.
	/// The enumerable will complete when the transport is stopped or the process exits.
	/// </remarks>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public async IAsyncEnumerable<string> ReadLinesAsync([EnumeratorCancellation] CancellationToken ct = default)
	{
		_stateManager.ThrowIfDisposed(nameof(ProcessUciTransport));

		var reader = _lines?.Reader ?? throw new InvalidOperationException("Transport not started.");

		_stateManager.EnsureSingleReader(_options.SingleReader);

		try
		{
			await foreach (string line in reader.ReadAllAsync(ct).ConfigureAwait(false))
				yield return line;
		}
		finally
		{
			_stateManager.ReleaseReaderIfSingle(_options.SingleReader);
		}
	}

	/// <summary>
	/// Starts the UCI engine process and initializes the transport.
	/// </summary>
	/// <param name="ct">Cancellation token to cancel the start operation.</param>
	/// <exception cref="InvalidOperationException">Thrown if the process fails to start or file system validation fails.</exception>
	/// <exception cref="OperationCanceledException">Thrown if the cancellation token is triggered.</exception>
	/// <remarks>
	/// This method is idempotent - calling it multiple times concurrently will result in only one start operation.
	/// If the transport is already started and alive, this method returns immediately.
	/// Thread-safe: Multiple concurrent calls are serialized via internal gating.
	/// </remarks>
	public async Task StartAsync(CancellationToken ct = default)
	{
		_stateManager.ThrowIfDisposed(nameof(ProcessUciTransport));

		if (IsStartedAndAlive()) return;

		var existingStart = _gateManager.GetStartingSignal();
		if (existingStart is { })
		{
			await GateManager.AwaitExistingStartAsync(existingStart, ct).ConfigureAwait(false);
			return;
		}

		_stateManager.EnsureStartable();

		bool acquired = await _gateManager.AcquireStartGateAsync(ct).ConfigureAwait(false);
		if (!acquired) return;

		var startingSignal = _gateManager.PublishStartingSignal();

		try
		{
			PrepareForStart();
			ct.ThrowIfCancellationRequested();

			string workingDir = ResolveWorkingDirectory();
			ProcessUciTransportValidator.ValidateFileSystem(_path, workingDir, _stateManager);

			CleanupExitedProcess();

			await CreateProcessAndRunPipelinesAsync(workingDir, startingSignal, ct).ConfigureAwait(false);
		}
		catch (Exception ex)
		{
			SignalStartFailure(startingSignal, ex, ct);
			await CleanupAfterFailedStartSafeAsync().ConfigureAwait(false);
			_stateManager.ResetStatusIfNeeded();
			Logger.LogException($"UCI engine failed to start. ex={ex}", category: LogCategory.UCI);
			throw;
		}
		finally
		{
			_gateManager.ReleaseStartGate();
		}
	}

	/// <summary>
	/// Gracefully stops the UCI engine process and cleans up resources.
	/// </summary>
	/// <param name="ct">Cancellation token to cancel the stop operation.</param>
	/// <exception cref="OperationCanceledException">Thrown if the cancellation token is triggered.</exception>
	/// <remarks>
	/// Sends "quit" command to the engine if SendQuitOnStop option is true.
	/// Waits for graceful process exit before forcefully terminating if necessary.
	/// Thread-safe: Multiple concurrent calls are serialized via internal gating.
	/// If transport is not started, this method completes immediately with no operation.
	/// </remarks>
	public async Task StopAsync(CancellationToken ct = default)
	{
		_stateManager.ThrowIfDisposed(nameof(ProcessUciTransport));
		ct.ThrowIfCancellationRequested();

		var existingStart = _gateManager.GetStartingSignal();
		if (existingStart != null)
			await GateManager.AwaitExistingStartAsync(existingStart, ct).ConfigureAwait(false);

		if (!_stateManager.IsStarted && _process is null)
		{
			_stateManager.SetStatus(TransportStatus.Stopped);
			Logger.LogInfo("StopAsync: transport not started; no-op.", category: LogCategory.UCI);
			return;
		}

		if (await _gateManager.AwaitExistingStopIfAnyAsync(ct).ConfigureAwait(false)) return;

		var stopTcs = _gateManager.PublishStoppingSignal();

		try
		{
			_stateManager.SetStatus(TransportStatus.Stopping);
			Logger.LogInfo("Stopping UCI transport.", category: LogCategory.UCI);

			await TearDownCoreAsync(_options.SendQuitOnStop, TransportStatus.Stopped, "Stopped UCI transport.")
				.ConfigureAwait(false);

			stopTcs.TrySetResult(null);
		}
		catch (Exception ex)
		{
			try
			{
				stopTcs.TrySetException(ex);
			}
			catch { }

			throw;
		}
		finally
		{
			_gateManager.ReleaseStopGate();
		}
	}

	/// <summary>
	/// Asynchronously writes a command line to the UCI engine's stdin.
	/// </summary>
	/// <param name="line">The command line to send to the engine.</param>
	/// <param name="ct">Cancellation token to cancel the write operation.</param>
	/// <exception cref="ArgumentNullException">Thrown if line is null.</exception>
	/// <exception cref="InvalidOperationException">Thrown if transport is not started, disposed, or process is not alive.</exception>
	/// <exception cref="OperationCanceledException">Thrown if the cancellation token is triggered.</exception>
	/// <remarks>
	/// This method will wait indefinitely if the channel is full (backpressure).
	/// Use TryWriteLineAsync with a timeout if you need bounded wait time.
	/// Command validation is performed if ValidateCommands option is true.
	/// </remarks>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public async Task WriteLineAsync(string line, CancellationToken ct = default)
	{
		ValidateWritePreconditions(line, ct);

		var writer = GetOutgoingWriterOrThrow();

		if (writer.TryWrite(line)) return;

		_metrics.IncrementBackpressureEvents();

		try
		{
			await writer.WriteAsync(line, ct).ConfigureAwait(false);
		}
		catch (ChannelClosedException)
		{
			throw new InvalidOperationException("Transport is stopping or stopped; cannot write.");
		}
	}

	/// <summary>
	/// Attempts to write a command line to the UCI engine's stdin with a timeout.
	/// </summary>
	/// <param name="line">The command line to send to the engine.</param>
	/// <param name="timeout">Maximum time to wait for the write operation. Use TimeSpan.Zero for immediate return, or Timeout.InfiniteTimeSpan for no timeout.</param>
	/// <param name="ct">Cancellation token to cancel the write operation.</param>
	/// <returns>True if the line was successfully written within the timeout; false if the timeout expired.</returns>
	/// <exception cref="ArgumentNullException">Thrown if line is null.</exception>
	/// <exception cref="ArgumentOutOfRangeException">Thrown if timeout is negative (except Timeout.InfiniteTimeSpan).</exception>
	/// <exception cref="InvalidOperationException">Thrown if transport is not started, disposed, or process is not alive.</exception>
	/// <exception cref="OperationCanceledException">Thrown if the cancellation token is triggered (not timeout expiration).</exception>
	/// <remarks>
	/// For very small timeouts (≤1ms), uses busy-waiting to reduce context switching overhead.
	/// Command validation is performed if ValidateCommands option is true.
	/// </remarks>
	public async Task<bool> TryWriteLineAsync(string line, TimeSpan timeout, CancellationToken ct = default)
	{
		ValidateTryWritePreconditions(line, timeout);

		if (_options.ValidateCommands) ProcessUciTransportValidator.ValidateCommandLine(line);

		var writer = GetOutgoingWriterOrThrow();

		if (writer.TryWrite(line)) return true;

		_metrics.IncrementBackpressureEvents();

		if (timeout == TimeSpan.Zero) return false;

		if (WriteOperationHelper.ShouldSpinForSmallTimeout(timeout))
			if (WriteOperationHelper.SpinUntilWriteOrCancel(writer, line, ct, _options.SmallTimeoutSpinIterations))
				return true;

		try
		{
			if (timeout == Timeout.InfiniteTimeSpan)
			{
				await WriteOperationHelper.WriteWithCallerCancellationAsync(writer, line, ct).ConfigureAwait(false);
				return true;
			}

			using var timeoutCts = new CancellationTokenSource(timeout);
			if (!ct.CanBeCanceled)
			{
				await writer.WriteAsync(line, timeoutCts.Token).ConfigureAwait(false);
			}
			else
			{
				using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);
				await writer.WriteAsync(line, linked.Token).ConfigureAwait(false);
			}

			return true;
		}
		catch (OperationCanceledException)
		{
			if (ct.IsCancellationRequested) throw;

			return false;
		}
		catch (ChannelClosedException)
		{
			throw new InvalidOperationException("Transport is stopping or stopped; cannot write.");
		}
	}

	public async ValueTask DisposeAsync()
	{
		if (!_stateManager.TryMarkDisposed()) return;

		// If a stop operation is in progress, wait for it to complete first
		try
		{
			if (await _gateManager.AwaitExistingStopIfAnyAsync(default).ConfigureAwait(false))
			{
				// Stop already completed, just update status to Disposed
				_stateManager.SetStatus(TransportStatus.Disposed);
				Logger.LogInfo("Disposed UCI transport (after stop).", category: LogCategory.UCI);
				_gateManager.ReleaseStopGate();
				return;
			}
		}
		catch
		{
			// Ignore exceptions from stop operation - we'll proceed with disposal
		}

		_stateManager.SetStatus(TransportStatus.Stopping);
		Logger.LogInfo("Disposing UCI transport.", category: LogCategory.UCI);

		try
		{
			await TearDownCoreAsync(_options.SendQuitOnDispose, TransportStatus.Disposed, "Disposed UCI transport.")
				.ConfigureAwait(false);
		}
		finally
		{
			_gateManager.ReleaseStopGate();
		}
	}

	/// <summary>
	/// Synchronous dispose implementation. Prefer <see cref="DisposeAsync"/> for async scenarios.
	/// </summary>
	/// <remarks>
	/// WARNING: This synchronous dispose blocks the calling thread until all async cleanup operations complete.
	/// This can potentially cause deadlocks if called from a synchronization context (e.g., UI thread).
	/// Always prefer <see cref="DisposeAsync"/> when possible. This method is provided only for compatibility
	/// with code that requires synchronous IDisposable (e.g., using statements without await).
	/// </remarks>
#pragma warning disable VSTHRD002 // Avoid problematic synchronous waits
	public void Dispose() => DisposeAsync().AsTask().GetAwaiter().GetResult();
#pragma warning restore VSTHRD002

	private bool IsStartedAndAlive() =>
		_stateManager.IsStarted && _process is { HasExited: false };

	private bool ProcessIsAlive() => _process is { HasExited: false };

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private ChannelWriter<string> GetOutgoingWriterOrThrow()
	{
		if (_outgoing is null) throw new InvalidOperationException("Transport is not started.");

		return _outgoing.Writer;
	}

	private string ResolveWorkingDirectory() =>
		string.IsNullOrWhiteSpace(_workingDirectory) ? Environment.CurrentDirectory : _workingDirectory;

	/// <summary>
	/// Cancels read loop and disposes stdout/stderr streams (but does not await loop completion).
	/// </summary>
	private void CancelReadLoopAndDisposeStreams()
	{
		try
		{
			_loopManager.CancelReadLoop();
			ChannelFactory.TryComplete(_lines?.Writer);
		}
		catch (Exception ex)
		{
			ReportError(ex, "Failed to cancel read loop during cleanup.");
		}

		try
		{
			StreamInitializer.SafeDispose(_streams.Stdout);
			_streams.Stdout = null;
		}
		catch (Exception ex)
		{
			ReportError(ex, "Failed to dispose stdout during cleanup.");
		}

		try
		{
			StreamInitializer.SafeDispose(_streams.Stderr);
			_streams.Stderr = null;
		}
		catch (Exception ex)
		{
			ReportError(ex, "Failed to dispose stderr during cleanup.");
		}
	}

	/// <summary>
	/// Awaits completion of read loops and nulls the lines channel.
	/// </summary>
	private async Task AwaitReadLoopsCompletionAsync()
	{
		try
		{
			await _loopManager.AwaitReadLoopAsync().ConfigureAwait(false);
			await _loopManager.AwaitStderrLoopAsync().ConfigureAwait(false);
			_lines = null;
		}
		catch (Exception ex)
		{
			ReportError(ex, "Failed to await read loops completion.");
		}
	}

	/// <summary>
	/// Cleans up write loop and stdin stream.
	/// </summary>
	private async Task CleanupWriteLoopAndStdinAsync(bool sendQuit)
	{
		// Send quit BEFORE canceling write loop if requested
		if (sendQuit && _streams.Stdin != null)
		{
			try
			{
				await _streams.Stdin.WriteLineAsync("quit").ConfigureAwait(false);
				await _streams.Stdin.FlushAsync().ConfigureAwait(false);
				_options.OnQuitSent?.Invoke();
			}
			catch (Exception ex)
			{
				ReportError(ex, "Failed to write quit to stdin during cleanup.");
			}
		}

		try
		{
			_loopManager.CancelWriteLoop();
			ChannelFactory.TryComplete(_outgoing?.Writer);
			await _loopManager.AwaitWriteLoopAsync().ConfigureAwait(false);
			_loopManager.DisposeCancellationSources();
			_outgoing = null;
		}
		catch (Exception ex)
		{
			ReportError(ex, "Failed to cleanup write loop during cleanup.");
		}

		try
		{
			if (_streams.Stdin != null)
			{
				await StreamInitializer.SafeDisposeAsync(_streams.Stdin).ConfigureAwait(false);
				_streams.Stdin = null;
			}
		}
		catch (Exception ex)
		{
			Error?.Invoke(ex);
		}
	}

	/// <summary>
	/// Cleans up process, killing if necessary and waiting for exit.
	/// </summary>
	private async Task CleanupProcessAsync(Process? process, bool killIfNotExited)
	{
		if (process is null) return;

		try
		{
			if (killIfNotExited)
			{
				try
				{
					if (!ProcessHasExitedSafe(process))
					{
						Logger.LogInfo(
							$"Killing UCI engine process (tree={_options.KillEntireProcessTree}).",
							category: LogCategory.UCI);

						ProcessHelper.SafeKillProcess(process, _options.KillEntireProcessTree);
					}
				}
				catch (Exception ex)
				{
					Logger.LogException(
						$"Failed to kill process during cleanup. ex={ex}",
						category: LogCategory.UCI);
				}
			}
			else
			{
				// Wait for graceful exit
				if (!ProcessHasExitedSafe(process))
				{
					try
					{
						using var timeoutCts = new CancellationTokenSource(GetQuitGracePeriod());
						try
						{
							await ProcessHelper.WaitForProcessExitAsync(process, timeoutCts.Token).ConfigureAwait(false);
						}
						catch (OperationCanceledException) { }
					}
					catch (Exception ex)
					{
						Error?.Invoke(ex);
					}

					// Re-check after grace period - process may have exited
					if (!ProcessHasExitedSafe(process))
					{
						try
						{
							Logger.LogInfo(
								$"Killing UCI engine process after grace period (tree={_options.KillEntireProcessTree}).",
								category: LogCategory.UCI);

							ProcessHelper.SafeKillProcess(process, _options.KillEntireProcessTree);
						}
						catch (Exception ex)
						{
							Error?.Invoke(ex);
						}
					}
				}
			}

			await _loopManager.AwaitExitNotificationAsync(_options.TeardownTimeout).ConfigureAwait(false);
			ProcessHelper.SafeDisposeProcess(process);
		}
		catch (Exception ex)
		{
			Error?.Invoke(ex);
		}
	}

	/// <summary>
	/// Safely checks if a process has exited, handling race conditions.
	/// </summary>
	private static bool ProcessHasExitedSafe(Process? process)
	{
		if (process is null) return true;

		try
		{
			return process.HasExited;
		}
		catch (InvalidOperationException)
		{
			// Process was disposed between null check and property access
			return true;
		}
	}

	/// <summary>
	/// Cleans up resources after a failed start attempt.
	/// This includes canceling loops, disposing streams, and killing the process if necessary.
	/// </summary>
	private async Task CleanupAfterFailedStartAsync()
	{
		_stateManager.MarkProcessAlive(false);

		// For failed start, we can dispose everything immediately - no need to drain output
		CancelReadLoopAndDisposeStreams();
		await CleanupWriteLoopAndStdinAsync(sendQuit: false).ConfigureAwait(false);
		await AwaitReadLoopsCompletionAsync().ConfigureAwait(false);

		var p = Interlocked.Exchange(ref _process, null);
		await CleanupProcessAsync(p, killIfNotExited: true).ConfigureAwait(false);

		_stateManager.ResetStatusIfNeeded();
	}

	private async Task CleanupAfterFailedStartSafeAsync()
	{
		try
		{
			await CleanupAfterFailedStartAsync().ConfigureAwait(false);
		}
		catch (Exception cleanupEx)
		{
			ReportError(cleanupEx, "Cleanup after failed start threw.");
		}
	}

	private async Task CreateProcessAndRunPipelinesAsync(
		string                        workingDir,
		TaskCompletionSource<object?> startingSignal,
		CancellationToken             token)
	{
		var startInfo = ProcessHelper.CreateProcessStartInfo(
			_path,
			_args,
			workingDir,
			_options.RedirectStandardError,
			_options.StdoutEncoding,
			_options.StderrEncoding);

		var startedProcess = new Process { StartInfo = startInfo, EnableRaisingEvents = true };

		token.ThrowIfCancellationRequested();
		if (!startedProcess.Start()) throw new InvalidOperationException("Failed to start UCI engine process.");

		_process = startedProcess;
		_stateManager.MarkProcessAlive(true);

		InitializeStreams(startedProcess);
		CreateChannels();
		_loopManager.InitializeCancellationSources();
		StartBackgroundLoops(startedProcess);

		_stateManager.SetStatus(TransportStatus.Started);

		Logger.LogInfo($"UCI engine started. PID={startedProcess.Id}", category: LogCategory.UCI);

		startingSignal.TrySetResult(null);
		await Task.CompletedTask;
	}

	/// <summary>
	/// Core teardown logic for stopping or disposing the transport.
	/// Cleans up all resources in the proper order: read loops, write loops, streams, and process.
	/// </summary>
	/// <param name="sendQuit">Whether to send "quit" command before closing stdin.</param>
	/// <param name="finalStatus">The final status to set after teardown completes.</param>
	/// <param name="finalLog">Log message to write after successful teardown.</param>
	/// <param name="ct">Cancellation token (currently unused but reserved for future use).</param>
	private async Task TearDownCoreAsync(
		bool              sendQuit,
		TransportStatus   finalStatus,
		string            finalLog,
		CancellationToken ct = default)
	{
		// Atomically exchange process reference to avoid race conditions
		var p = Interlocked.Exchange(ref _process, null);
		_stateManager.MarkProcessAlive(false);

		// Cancel read loop and dispose streams, but don't await completion yet
		// The read loops need to keep running while we send quit and drain final output
		CancelReadLoopAndDisposeStreams();

		// Send quit (if requested) and cleanup write side
		await CleanupWriteLoopAndStdinAsync(sendQuit).ConfigureAwait(false);

		// NOW await read loops completion - they've had time to drain final output
		await AwaitReadLoopsCompletionAsync().ConfigureAwait(false);

		// Finally cleanup the process
		await CleanupProcessAsync(p, killIfNotExited: false).ConfigureAwait(false);

		_stateManager.SetStatus(finalStatus);
		Logger.LogInfo(finalLog, category: LogCategory.UCI);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private TimeSpan GetQuitGracePeriod() =>
		_options.QuitGracePeriod != TimeSpan.Zero
			? _options.QuitGracePeriod
			: TimeSpan.FromMilliseconds(_options.QuitGracePeriodMs);

	private void CleanupExitedProcess()
	{
		if (_process is not { HasExited: true }) return;

		try
		{
			ProcessHelper.SafeDisposeProcess(_process);
		}
		catch { }
		finally
		{
			_process = null;
		}
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private void CreateChannels()
	{
		_lines    = ChannelFactory.CreateLinesChannel(_options.ChannelCapacity, _options.SingleReader);
		_outgoing = ChannelFactory.CreateOutgoingChannel(_options.ChannelCapacity, _options.OutgoingSingleWriter);
	}

	/// <summary>
	/// Fires the Exited event exactly once, even if called multiple times.
	/// Handles exceptions from user event handlers to prevent them from propagating.
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private void FireExitedOnce(int? exitCode, string? error)
	{
		if (!_stateManager.TryMarkExitedRaised()) return;

		var exited = Exited;
		if (exited != null)
			try
			{
				exited(exitCode, error);
			}
			catch (Exception handlerEx)
			{
				// Log and report event handler exceptions, but don't let them propagate
				// to avoid disrupting the transport's internal cleanup logic
				ReportError(handlerEx, $"Exited event handler threw exception: {handlerEx.Message}");
			}
	}

	private void InitializeStreams(Process process)
	{
		var streams = StreamInitializer.InitializeStreams(
			process,
			_options.StdinEncoding,
			_options.StdoutEncoding,
			_options.StderrEncoding,
			_options.RedirectStandardError,
			_options.NewLine,
			false);

		_streams.Stdin  = streams.Stdin;
		_streams.Stdout = streams.Stdout;
		_streams.Stderr = streams.Stderr;
	}

	private void PrepareForStart()
	{
		_stateManager.ResetExitedRaised();
		_stateManager.SetStatus(TransportStatus.Starting);
		Logger.LogInfo("Starting UCI engine process.", category: LogCategory.UCI);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private void ReportError(Exception ex, string message)
	{
		try
		{
			Logger.LogException($"{message} ex={ex}", category: LogCategory.UCI);
		}
		catch { }

		try
		{
			Error?.Invoke(ex);
		}
		catch { }
	}

	private void SignalStartFailure(TaskCompletionSource<object?> startingSignal, Exception ex, CancellationToken ct)
	{
		try
		{
			if (ex is OperationCanceledException) startingSignal.TrySetCanceled(ct);
			else startingSignal.TrySetException(ex);
		}
		catch { }
	}

	private void StartBackgroundLoops(Process process)
	{
		if (_streams.Stdout != null && _lines != null)
			_loopManager.StartReadLoop(_streams.Stdout, _lines.Writer);

		if (_outgoing != null && _streams.Stdin != null)
			_loopManager.StartWriteLoop(_outgoing.Reader, _streams.Stdin);

		if (_streams.Stderr != null)
			_loopManager.StartStderrLoopIfNeeded(_streams.Stderr);

		_loopManager.StartExitNotification(
			process,
			(exitCode, error) => FireExitedOnce(exitCode, error),
			ex => ReportError(ex, "Error while waiting for process exit."),
			_stateManager);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private void ValidateTryWritePreconditions(string line, TimeSpan timeout)
	{
		_stateManager.ThrowIfProcessNotStarted();
		_stateManager.ThrowIfDisposed(nameof(ProcessUciTransport));
		_stateManager.ThrowIfProcessNotAlive(_process);
		if (line is null) throw new ArgumentNullException(nameof(line));
		if (timeout < TimeSpan.Zero && timeout != Timeout.InfiniteTimeSpan)
			throw new ArgumentOutOfRangeException(nameof(timeout));
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private void ValidateWritePreconditions(string line, CancellationToken ct)
	{
		_stateManager.ThrowIfProcessNotStarted();
		_stateManager.ThrowIfDisposed(nameof(ProcessUciTransport));
		_stateManager.ThrowIfProcessNotAlive(_process);
		if (line is null) throw new ArgumentNullException(nameof(line));

		if (_options.ValidateCommands) ProcessUciTransportValidator.ValidateCommandLine(line);
		ct.ThrowIfCancellationRequested();
	}

	/// <summary>
	/// Validates and creates an immutable list of arguments, ensuring no null values.
	/// </summary>
	private static IReadOnlyList<string>? ValidateAndCreateArgsList(IEnumerable<string> args)
	{
		if (args is null) return null;

		// Validate and materialize in a single pass to avoid double enumeration
		var list = new List<string>();
		foreach (var arg in args)
		{
			if (arg is null)
				throw new ArgumentException("Argument list cannot contain null values.", nameof(args));

			list.Add(arg);
		}

		return list;
	}
}
