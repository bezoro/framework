using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace Bezoro.UCI.Domain;

internal sealed class ProcessUciTransport : IUciTransport
{
	private readonly IReadOnlyList<string>? _args;

	private readonly ProcessUciTransportOptions _options;

	private readonly string  _path;
	private readonly string? _workingDirectory;

	private CancellationTokenSource? _readLoopCts;
	private CancellationTokenSource? _writeLoopCts;

	private Channel<string>? _lines;
	private Channel<string>? _outgoing;

	// 0 = false, 1 = true (cross-thread visibility)
	private int _disposed;
	private int _isStarted; // 0 = false, 1 = true (cross-thread visibility)
	private int _readerActive;
	private int _startGate; // prevents concurrent StartAsync calls
	private int _status;    // TransportStatus stored as int for interlocked/volatile
	private int _stopGate;  // prevents concurrent StopAsync calls

	private long _backpressureEvents;

	// Lightweight counters for diagnostics/tuning (thread-safe reads via Interlocked).
	private long _linesRead;
	private long _linesWritten;

	private Process? _process;

	private StreamReader? _stderr;
	private StreamReader? _stdout;
	private StreamWriter? _stdin;

	private Task? _exitNotifyTask;
	private Task? _readLoopTask;
	private Task? _stderrLoopTask;
	private Task? _writeLoopTask;

	private TaskCompletionSource<object?>? _startingTcs;
	private TaskCompletionSource<object?>? _stoppingTcs;

	/// <summary>
	///     Raised when an internal error occurs.
	///     Threading: invoked on a ThreadPool thread; exceptions thrown by handlers are swallowed.
	/// </summary>
	public event Action<Exception>? Error;

	/// <summary>
	///     Raised when the engine process exits.
	///     Threading: invoked on a ThreadPool thread; exceptions thrown by handlers are swallowed.
	/// </summary>
	public event Action<int?, string?>? Exited;

	/// <summary>
	///     Raised when a line is received on stderr (if redirected).
	///     Threading: invoked inline on the stderr read loop (ThreadPool thread); slow handlers can throttle stderr
	///     consumption. Exceptions thrown by handlers are swallowed.
	/// </summary>
	public event Action<string>? StderrReceived;

	public ProcessUciTransport(string path, IEnumerable<string>? args = null, string? workingDirectory = null)
		: this(path, args, workingDirectory, null) { }

	public ProcessUciTransport(
		string                      path,
		IEnumerable<string>?        args,
		string?                     workingDirectory,
		ProcessUciTransportOptions? options)
	{
		if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("Engine path must be provided.", nameof(path));

		_path             = path;
		_workingDirectory = workingDirectory;
		_args             = args is null ? null : new List<string>(args);
		_options          = options ?? new ProcessUciTransportOptions();

		ValidateOptions(_options);
	}

	/// <summary>
	///     Best-effort health indicator.
	///     True when started, the underlying process hasn't exited, and background loops are still running.
	/// </summary>
	public bool IsHealthy =>
		IsStarted &&
		_process is { HasExited: false } &&
		(_readLoopTask is null || !_readLoopTask.IsCompleted) &&
		(_writeLoopTask is null || !_writeLoopTask.IsCompleted) &&
		(_stderr is null || _stderrLoopTask is null || !_stderrLoopTask.IsCompleted) &&
		(_exitNotifyTask is null || !_exitNotifyTask.IsCompleted);

	public bool IsStarted          => Volatile.Read(ref _isStarted) == 1;
	public long BackpressureEvents => Interlocked.Read(ref _backpressureEvents);

	// Lightweight metrics for tuning; zero overhead when not read.
	public long LinesRead    => Interlocked.Read(ref _linesRead);
	public long LinesWritten => Interlocked.Read(ref _linesWritten);

	/// <summary>
	///     Current transport status. This reflects internal lifecycle transitions.
	/// </summary>

	public TransportStatus Status => (TransportStatus)Volatile.Read(ref _status);

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public async IAsyncEnumerable<string> ReadLinesAsync([EnumeratorCancellation] CancellationToken ct = default)
	{
		ThrowIfDisposed();

		var reader = _lines?.Reader ?? throw new InvalidOperationException("Transport not started.");

		if (_options.SingleReader)
			if (Interlocked.CompareExchange(ref _readerActive, 1, 0) != 0)
				throw new InvalidOperationException("Only a single reader is supported for this transport.");

		try
		{
			await foreach (string? line in reader.ReadAllAsync(ct).ConfigureAwait(false)) yield return line;
		}
		finally
		{
			if (_options.SingleReader) Volatile.Write(ref _readerActive, 0);
		}
	}

	public async Task StartAsync(CancellationToken ct = default)
	{
		ThrowIfDisposed();

		// Fast path: already started and process alive
		if (Volatile.Read(ref _isStarted) == 1 && _process is { HasExited: false })
			return;

		// If another Start is in progress, await it
		var existingStart = _startingTcs;
		if (existingStart != null)
		{
			await AwaitWithCancellation(existingStart.Task, ct).ConfigureAwait(false);
			return;
		}

		// Ensure valid state to start
		int currentStatus = Volatile.Read(ref _status);
		if (currentStatus is not (int)TransportStatus.Created and not (int)TransportStatus.Stopped)
			throw new InvalidOperationException("Transport cannot be started in its current state.");

		// Prevent concurrent starts
		if (Interlocked.CompareExchange(ref _startGate, 1, 0) != 0)
		{
			// Another Start acquired the gate; await its TCS if present
			existingStart = _startingTcs;
			if (existingStart == null) throw new InvalidOperationException("Transport is already starting.");

			await AwaitWithCancellation(existingStart.Task, ct).ConfigureAwait(false);
			return;
		}

		// We won the start gate: publish starting TCS so others can await
		var localStartTcs = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
		Volatile.Write(ref _startingTcs, localStartTcs);

		// Transition to Starting
		Interlocked.Exchange(ref _status, (int)TransportStatus.Starting);
		_options.Logger?.LogInfo("Starting UCI engine process.");

		var startInfo = CreateProcessStartInfo();

		var process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };

		try
		{
			ct.ThrowIfCancellationRequested();

			if (!process.Start())
				throw new InvalidOperationException("Failed to start UCI engine process.");

			_process = process;

			InitializeStreams(process);
			CreateChannels();
			InitializeCancellationSources();
			StartBackgroundLoops(process);

			Volatile.Write(ref _isStarted, 1);
			Volatile.Write(ref _status,    (int)TransportStatus.Started);
			if (_options.Logger != null)
			{
				var pid = process.Id.ToString();
				_options.Logger.LogInfo("UCI engine started. PID=" + pid);
			}

			// Signal successful start
			localStartTcs.TrySetResult(null);
		}
		catch (Exception ex)
		{
			// Signal failure to any awaiters with the original exception or cancellation
			try
			{
				if (ex is OperationCanceledException)
					localStartTcs.TrySetCanceled(ct);
				else
					localStartTcs.TrySetException(ex);
			}
			catch
			{
				/* best-effort */
			}

			// If startup fails or is cancelled, tear down any partially started resources without disposing the transport.
			try
			{
				await CleanupAfterFailedStartAsync().ConfigureAwait(false);
			}
			catch (Exception exception)
			{
				Error?.Invoke(exception);
			}

			if (Volatile.Read(ref _disposed) == 0)
				Volatile.Write(ref _status, (int)TransportStatus.Stopped);

			_options.Logger?.LogError(ex, "UCI engine failed to start.");

			throw;
		}
		finally
		{
			// Always release the start gate and clear the TCS
			Interlocked.Exchange(ref _startGate, 0);
			Volatile.Write(ref _startingTcs, null);
		}
	}

	public async Task StopAsync(CancellationToken ct = default)
	{
		ThrowIfDisposed();
		ct.ThrowIfCancellationRequested();

		// Fast-exit: if not started, ensure state is Stopped and return
		if (Volatile.Read(ref _isStarted) == 0 && _process is null)
		{
			Volatile.Write(ref _status, (int)TransportStatus.Stopped);
			_options.Logger?.LogDebug("StopAsync: transport not started; no-op.");
			return;
		}

		// If another Stop is in progress, await it
		var existingStop = _stoppingTcs;
		if (existingStop != null)
		{
			await AwaitWithCancellation(existingStop.Task, ct).ConfigureAwait(false);
			return;
		}

		// Prevent concurrent stops
		if (Interlocked.CompareExchange(ref _stopGate, 1, 0) != 0)
		{
			existingStop = _stoppingTcs;
			if (existingStop != null)
			{
				await AwaitWithCancellation(existingStop.Task, ct).ConfigureAwait(false);
				return;
			}

			throw new InvalidOperationException("Transport is already stopping.");
		}

		// Publish stopping TCS
		var localStopTcs = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
		Volatile.Write(ref _stoppingTcs, localStopTcs);

		try
		{
			Interlocked.Exchange(ref _status, (int)TransportStatus.Stopping);
			_options.Logger?.LogInfo("Stopping UCI transport.");

			await TearDownCoreAsync(
				_options.SendQuitOnStop,
				TransportStatus.Stopped,
				"Stopped UCI transport.").ConfigureAwait(false);

			localStopTcs.TrySetResult(null);
		}
		catch (Exception ex)
		{
			try
			{
				localStopTcs.TrySetException(ex);
			}
			catch
			{
				/* best-effort */
			}

			throw;
		}
		finally
		{
			// Always release the stop gate and clear TCS
			Interlocked.Exchange(ref _stopGate, 0);
			Volatile.Write(ref _stoppingTcs, null);
		}
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public async Task WriteLineAsync(string line, CancellationToken ct = default)
	{
		ThrowIfDisposed();
		if (_process is { HasExited: true }) throw new InvalidOperationException("Engine process has exited.");
		if (line is null) throw new ArgumentNullException(nameof(line));

		if (_options.ValidateCommands)
			ValidateCommandLine(line);

		ct.ThrowIfCancellationRequested();

		var writer = GetOutgoingWriterOrThrow();

		// Fast path: try non-blocking write to avoid async state machines on the hot path.
		if (writer.TryWrite(line)) return;

		// Slow path: channel is full, record backpressure and await space.
		Interlocked.Increment(ref _backpressureEvents);

		try
		{
			await writer.WriteAsync(line, ct).ConfigureAwait(false);
		}
		catch (ChannelClosedException)
		{
			throw new InvalidOperationException("Transport is stopping or stopped; cannot write.");
		}
	}

	public async Task<bool> TryWriteLineAsync(string line, TimeSpan timeout, CancellationToken ct = default)
	{
		ThrowIfDisposed();
		if (_process is { HasExited: true }) throw new InvalidOperationException("Engine process has exited.");
		if (line is null) throw new ArgumentNullException(nameof(line));
		if (timeout < TimeSpan.Zero && timeout != Timeout.InfiniteTimeSpan)
			throw new ArgumentOutOfRangeException(nameof(timeout));

		if (_options.ValidateCommands)
			ValidateCommandLine(line);

		var writer = GetOutgoingWriterOrThrow();

		// Fast path: try non-blocking
		if (writer.TryWrite(line)) return true;

		// Slow path: channel is full; record backpressure and proceed to spin/await strategies.
		Interlocked.Increment(ref _backpressureEvents);

		// Zero-timeout: do not allocate CTS; fail fast.
		if (timeout == TimeSpan.Zero) return false;

		// Tiny bounded spin for very small timeouts to avoid CTS allocations when the channel is about to free.
		if (timeout > TimeSpan.Zero && timeout <= TimeSpan.FromMilliseconds(1))
		{
			var spinner = new SpinWait();
			int spins   = _options.SmallTimeoutSpinIterations;
			for (var i = 0; i < spins; i++)
			{
				if (writer.TryWrite(line)) return true;

				if (ct.IsCancellationRequested) throw new OperationCanceledException(ct);

				spinner.SpinOnce();
			}
		}

		try
		{
			// Infinite timeout path: avoid linked CTS when possible
			if (timeout == Timeout.InfiniteTimeSpan)
			{
				if (!ct.CanBeCanceled)
					await writer.WriteAsync(line, CancellationToken.None).ConfigureAwait(false);
				else
					await writer.WriteAsync(line, ct).ConfigureAwait(false);

				return true;
			}

			// Finite timeout path
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
			// If caller requested cancellation, propagate
			if (ct.IsCancellationRequested) throw;

			// Otherwise, it was the timeout
			return false;
		}
		catch (ChannelClosedException)
		{
			throw new InvalidOperationException("Transport is stopping or stopped; cannot write.");
		}
	}

	public async ValueTask DisposeAsync()
	{
		if (Interlocked.Exchange(ref _disposed, 1) != 0) return;

		// Transition to Stopping
		Interlocked.Exchange(ref _status, (int)TransportStatus.Stopping);
		_options.Logger?.LogInfo("Disposing UCI transport.");

		await TearDownCoreAsync(
			_options.SendQuitOnDispose,
			TransportStatus.Disposed,
			"Disposed UCI transport.").ConfigureAwait(false);
	}

	public void Dispose()
	{
		DisposeAsync().AsTask().GetAwaiter().GetResult();
	}

	private static async Task AwaitWithCancellation(Task task, CancellationToken ct)
	{
		if (!ct.CanBeCanceled)
		{
			await task.ConfigureAwait(false);
			return;
		}

		// Fast path if already completed
		if (task.IsCompleted)
		{
			await task.ConfigureAwait(false);
			return;
		}

		// If the caller's token is already canceled, avoid allocations and throw immediately.
		ct.ThrowIfCancellationRequested();

		var       tcs = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
		using var reg = ct.Register(static state => ((TaskCompletionSource<object?>)state!).TrySetResult(null), tcs);

		var completed = await Task.WhenAny(task, tcs.Task).ConfigureAwait(false);
		if (completed == tcs.Task)
			throw new OperationCanceledException(ct);

		await task.ConfigureAwait(false);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static async Task SafeDisposeAsync(IAsyncDisposable? d)
	{
		if (d is null) return;

		try
		{
			await d.DisposeAsync().ConfigureAwait(false);
		}
		catch
		{
			/* best-effort */
		}
	}

	private static Task WaitForProcessExitAsync(Process process, CancellationToken ct)
	{
		// Fast path: if already exited, avoid allocations.
		try
		{
			if (process.HasExited)
				return Task.CompletedTask;

			if (!process.EnableRaisingEvents)
				process.EnableRaisingEvents = true;
		}
		catch (ObjectDisposedException)
		{
			// If the Process is already disposed, consider it "exited" for our purposes.
			return Task.CompletedTask;
		}

		var tcs = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);

		void Handler(object? _, EventArgs __)
		{
			tcs.TrySetResult(null);
		}

		try
		{
			process.Exited += Handler;

			// If the process already exited after subscribing, complete immediately.
			if (process.HasExited)
			{
				process.Exited -= Handler;
				return Task.CompletedTask;
			}
		}
		catch (ObjectDisposedException)
		{
			// If the Process is already disposed, consider it "exited" for our purposes.
			return Task.CompletedTask;
		}

		CancellationTokenRegistration reg = default;
		if (ct.CanBeCanceled)
			reg = ct.Register(static state => ((TaskCompletionSource<object?>)state!).TrySetCanceled(), tcs);

		return tcs.Task.ContinueWith(
			t =>
			{
				try
				{
					process.Exited -= Handler;
				}
				catch
				{
					/* best-effort */
				}

				reg.Dispose();
				return t;
			},
			CancellationToken.None,
			TaskContinuationOptions.ExecuteSynchronously,
			TaskScheduler.Default).Unwrap();
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static void SafeCancel(CancellationTokenSource? cts)
	{
		if (cts is null) return;

		try
		{
			cts.Cancel();
		}
		catch
		{
			/* best-effort */
		}
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static void SafeDispose(IDisposable? d)
	{
		if (d is null) return;

		try
		{
			d.Dispose();
		}
		catch
		{
			/* best-effort */
		}
	}

	// === Small safety/utility helpers to reduce duplication and improve clarity ===
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static void TryComplete(ChannelWriter<string>? writer, Exception? error = null)
	{
		if (writer is null) return;

		try
		{
			if (error is null) writer.TryComplete();
			else writer.TryComplete(error);
		}
		catch
		{
			/* best-effort */
		}
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static void ValidateCommandLine(string line)
	{
		// UCI is line-oriented; ensure no CR/LF in payload
		if (line.AsSpan().IndexOfAny('\r', '\n') >= 0)
			throw new ArgumentException("Line must not contain CR or LF characters.", nameof(line));

		// Also forbid empty/whitespace-only commands
		if (string.IsNullOrWhiteSpace(line))
			throw new ArgumentException("Command line must not be empty or whitespace.", nameof(line));
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static void ValidateOptions(ProcessUciTransportOptions options)
	{
		// Channel capacity must be positive
		if (options.ChannelCapacity <= 0)
			throw new ArgumentOutOfRangeException(
				nameof(options.ChannelCapacity),
				"ChannelCapacity must be greater than 0.");

		// NewLine must be non-empty (allow either \n or \r\n)
		if (string.IsNullOrEmpty(options.NewLine))
			throw new ArgumentException("NewLine must be non-empty.", nameof(options.NewLine));

		// Quit grace period cannot be negative; default(TimeSpan) is allowed (falls back to QuitGracePeriodMs)
		if (options.QuitGracePeriod < TimeSpan.Zero)
			throw new ArgumentOutOfRangeException(
				nameof(options.QuitGracePeriod),
				"QuitGracePeriod cannot be negative.");

		// Legacy millisecond fallback must be non-negative
		if (options.QuitGracePeriod == default && options.QuitGracePeriodMs < 0)
			throw new ArgumentOutOfRangeException(
				nameof(options.QuitGracePeriodMs),
				"QuitGracePeriodMs cannot be negative.");
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private ChannelWriter<string> GetOutgoingWriterOrThrow()
	{
		if (_outgoing is null)
			throw new InvalidOperationException("Transport not started or already stopping/stopped.");

		return _outgoing.Writer;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private ProcessStartInfo CreateProcessStartInfo()
	{
		var startInfo = new ProcessStartInfo
		{
			FileName               = _path,
			UseShellExecute        = false,
			RedirectStandardInput  = true,
			RedirectStandardOutput = true,
			RedirectStandardError  = _options.RedirectStandardError,
			StandardOutputEncoding = _options.StdoutEncoding,
			StandardErrorEncoding  = _options.StderrEncoding,
			CreateNoWindow         = true,
			WorkingDirectory = string.IsNullOrWhiteSpace(_workingDirectory)
								   ? Environment.CurrentDirectory
								   : _workingDirectory
		};

		if (_args is not { Count: > 0 }) return startInfo;

		foreach (string? a in _args)
		{
			if (a is null) continue;

			startInfo.ArgumentList.Add(a);
		}

		return startInfo;
	}

	private async Task CleanupAfterFailedStartAsync()
	{
		// Do not set _disposed here; keep the transport reusable.
		var cts = _readLoopCts;
		_readLoopCts = null;
		SafeCancel(cts);

		SafeDispose(_stdout);
		_stdout = null;

		// Dispose stderr early as well to unblock any pending ReadLineAsync in the stderr loop (failed-start cleanup)
		SafeDispose(_stderr);
		_stderr = null;

		// Stop writer loop and complete outgoing channel (failed-start cleanup)
		var wcts = _writeLoopCts;
		_writeLoopCts = null;
		SafeCancel(wcts);

		TryComplete(_outgoing?.Writer);
		var writeLoop = _writeLoopTask;
		_writeLoopTask = null;
		try
		{
			if (writeLoop != null) await writeLoop.ConfigureAwait(false);
		}
		catch (Exception ex)
		{
			Error?.Invoke(ex);
		}
		finally
		{
			SafeDispose(wcts);
		}

		_outgoing = null;

		if (_stdin != null)
		{
			await SafeDisposeAsync(_stdin).ConfigureAwait(false);
			_stdin = null;
		}

		var readLoop = _readLoopTask;
		_readLoopTask = null;
		try
		{
			if (readLoop != null) await readLoop.ConfigureAwait(false);
		}
		catch (Exception ex)
		{
			Error?.Invoke(ex);
		}

		if (_stderrLoopTask != null)
		{
			try
			{
				await _stderrLoopTask.ConfigureAwait(false);
			}
			catch (Exception ex)
			{
				Error?.Invoke(ex);
			}

			_stderrLoopTask = null;
		}

		TryComplete(_lines?.Writer);
		_lines = null;

		var p = _process;
		_process = null;
		try
		{
			if (p != null)
			{
				try
				{
					if (!p.HasExited)
					{
						if (_options.Logger != null)
							_options.Logger.LogInfo(
								"Killing UCI engine process during failed start cleanup (tree=" +
								_options.KillEntireProcessTree +
								").");
#if NET5_0_OR_GREATER
						p.Kill(_options.KillEntireProcessTree);
#else
						p.Kill();
#endif
					}
				}
				catch (Exception ex)
				{
					_options.Logger?.LogError(ex, "Failed to kill process during failed start cleanup.");
				}

				var exitNotify = _exitNotifyTask;
				_exitNotifyTask = null;
				if (exitNotify != null)
					try
					{
						await exitNotify.ConfigureAwait(false);
					}
					catch (Exception ex)
					{
						Error?.Invoke(ex);
					}

				SafeDispose(p);
			}
		}
		catch (Exception ex)
		{
			Error?.Invoke(ex);
		}

		Volatile.Write(ref _isStarted, 0);
		if (Volatile.Read(ref _disposed) == 0)
			Volatile.Write(ref _status, (int)TransportStatus.Stopped);
	}

	private async Task TearDownCoreAsync(bool sendQuit, TransportStatus finalStatus, string finalLog)
	{
		var p = _process;
		_process = null;

		// Cancel read/stderr loops up-front
		var rcts = _readLoopCts;
		_readLoopCts = null;
		SafeCancel(rcts);

		// Complete channel early so any pending readers unblock immediately
		TryComplete(_lines?.Writer);

		// Dispose stdout early to unblock any pending ReadLineAsync
		SafeDispose(_stdout);
		_stdout = null;

		// Dispose stderr early as well to unblock any pending ReadLineAsync in the stderr loop
		SafeDispose(_stderr);
		_stderr = null;

		// Stop writer loop and complete outgoing channel
		var wcts = _writeLoopCts;
		_writeLoopCts = null;
		SafeCancel(wcts);

		TryComplete(_outgoing?.Writer);
		var writeLoop = _writeLoopTask;
		_writeLoopTask = null;
		try
		{
			if (writeLoop != null) await writeLoop.ConfigureAwait(false);
		}
		catch (Exception ex)
		{
			Error?.Invoke(ex);
		}
		finally
		{
			SafeDispose(wcts);
		}

		_outgoing = null;

		// Politely request engine shutdown and signal EOF
		try
		{
			if (_stdin != null)
			{
				try
				{
					if (sendQuit)
					{
						await _stdin.WriteLineAsync("quit").ConfigureAwait(false);
						await _stdin.FlushAsync().ConfigureAwait(false);
					}
				}
				catch (Exception ex)
				{
					Error?.Invoke(ex);
				}

				await SafeDisposeAsync(_stdin).ConfigureAwait(false);
				_stdin = null;
			}
		}
		catch (Exception ex)
		{
			Error?.Invoke(ex);
		}

		// Await background read loop and dispose its CTS
		var readLoop = _readLoopTask;
		_readLoopTask = null;
		try
		{
			if (readLoop != null) await readLoop.ConfigureAwait(false);
		}
		catch (Exception ex)
		{
			Error?.Invoke(ex);
		}
		finally
		{
			SafeDispose(rcts);
		}

		// Await stderr loop
		if (_stderrLoopTask != null)
		{
			try
			{
				await _stderrLoopTask.ConfigureAwait(false);
			}
			catch (Exception ex)
			{
				Error?.Invoke(ex);
			}

			_stderrLoopTask = null;
		}

		// Complete channel so any consumers finish
		TryComplete(_lines?.Writer);
		_lines = null;

		// Give the process a brief chance to exit cleanly
		try
		{
			if (p is { HasExited: false })
			{
				var grace = GetQuitGracePeriod();

				using var timeoutCts = new CancellationTokenSource(grace);
				try
				{
					await WaitForProcessExitAsync(p, timeoutCts.Token).ConfigureAwait(false);
				}
				catch (OperationCanceledException)
				{
					/* timed out */
				}
			}
		}
		catch (Exception ex)
		{
			Error?.Invoke(ex);
		}

		var skipAwaitExit = false;

		try
		{
			if (p is { HasExited: false })
			{
				if (_options.Logger != null)
					_options.Logger.LogInfo(
						"Killing UCI engine process (tree=" + _options.KillEntireProcessTree + ").");
#if NET5_0_OR_GREATER
				p.Kill(_options.KillEntireProcessTree);
#else
				p.Kill();
#endif
			}
		}
		catch (Exception ex)
		{
			// If Kill fails (permissions/lifecycle constraints), do not await exit notification.
			Error?.Invoke(ex);
			skipAwaitExit = true;
		}
		finally
		{
			// Ensure exit notification completes (reads ExitCode) before disposing the process,
			// unless we explicitly skip after a failed Kill().
			var exitNotify = _exitNotifyTask;
			_exitNotifyTask = null;
			if (!skipAwaitExit && exitNotify != null)
				try
				{
					await exitNotify.ConfigureAwait(false);
				}
				catch (Exception ex)
				{
					Error?.Invoke(ex);
				}

			SafeDispose(p);

			Volatile.Write(ref _isStarted, 0);
			Volatile.Write(ref _status,    (int)finalStatus);
			_options.Logger?.LogInfo(finalLog);
		}
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private TimeSpan GetQuitGracePeriod() =>
		_options.QuitGracePeriod != default
			? _options.QuitGracePeriod
			: TimeSpan.FromMilliseconds(_options.QuitGracePeriodMs);

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private void CreateChannels()
	{
		_lines = Channel.CreateBounded<string>(
			new BoundedChannelOptions(_options.ChannelCapacity)
			{
				SingleWriter = true,
				SingleReader = _options.SingleReader,
				FullMode     = BoundedChannelFullMode.Wait
			});

		_outgoing = Channel.CreateBounded<string>(
			new BoundedChannelOptions(_options.ChannelCapacity)
			{
				SingleWriter = _options.OutgoingSingleWriter,
				SingleReader = true,
				FullMode     = BoundedChannelFullMode.Wait
			});
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private void InitializeCancellationSources()
	{
		_readLoopCts  = new();
		_writeLoopCts = new();
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private void InitializeStreams(Process process)
	{
		var stdinEncoding  = _options.StdinEncoding ?? process.StandardInput.Encoding;
		var stdoutEncoding = _options.StdoutEncoding ?? process.StandardOutput.CurrentEncoding;
		var stderrEncoding = _options.StderrEncoding ??
							 (_options.RedirectStandardError
								  ? process.StandardError.CurrentEncoding
								  : Encoding.UTF8);

		// Wrap base streams with larger buffers and keep the underlying streams open for process lifecycle management.
		_stdin = new(process.StandardInput.BaseStream, stdinEncoding, 64 * 1024, true)
		{
			NewLine   = _options.NewLine,
			AutoFlush = false
		};

		_stdout = new(
			process.StandardOutput.BaseStream,
			stdoutEncoding,
			false,
			64 * 1024,
			true);

		_stderr = _options.RedirectStandardError
					  ? new StreamReader(
						  process.StandardError.BaseStream,
						  stderrEncoding,
						  false,
						  32 * 1024,
						  true)
					  : null;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private void Observe(Task task)
	{
		task.ContinueWith(
			t =>
			{
				try
				{
					// Surface any unobserved exceptions from background tasks
					var agg        = t.Exception!.Flatten();
					var exToReport = agg.InnerExceptions.Count == 1 ? agg.InnerExceptions[0] : agg;
					ReportError(exToReport, "Background task faulted.");
				}
				catch
				{
					/* best-effort */
				}
			},
			CancellationToken.None,
			TaskContinuationOptions.ExecuteSynchronously | TaskContinuationOptions.OnlyOnFaulted,
			TaskScheduler.Default);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private void ReportError(Exception ex, string message)
	{
		try
		{
			_options.Logger?.LogError(ex, message);
		}
		catch
		{
			/* swallow */
		}

		try
		{
			Error?.Invoke(ex);
		}
		catch
		{
			/* swallow */
		}
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private void StartBackgroundLoops(Process process)
	{
		StartReadLoop();
		StartWriteLoop();
		StartStderrLoopIfNeeded();
		StartExitNotification(process);
	}

	private void StartExitNotification(Process process)
	{
		_exitNotifyTask = Task.Run(
			async () =>
			{
				try
				{
					await WaitForProcessExitAsync(process, CancellationToken.None).ConfigureAwait(false);
					int exitCode = process.ExitCode;
					if (_options.Logger != null)
						_options.Logger.LogInfo("UCI engine process exited with code " + exitCode + ".");

					try
					{
						Exited?.Invoke(exitCode, null);
					}
					catch (Exception handlerEx)
					{
						_options.Logger?.LogError(handlerEx, "Exited event handler threw.");
						try
						{
							Error?.Invoke(handlerEx);
						}
						catch
						{
							/* swallow */
						}
					}
				}
				catch (Exception ex)
				{
					_options.Logger?.LogError(ex, "Error while waiting for process exit.");
					try
					{
						Error?.Invoke(ex);
					}
					catch
					{
						/* swallow */
					}

					try
					{
						Exited?.Invoke(null, ex.Message);
					}
					catch (Exception handlerEx)
					{
						_options.Logger?.LogError(handlerEx, "Exited event handler threw.");
						try
						{
							Error?.Invoke(handlerEx);
						}
						catch
						{
							/* swallow */
						}
					}
				}
			},
			CancellationToken.None);

		Observe(_exitNotifyTask);
	}

	private void StartReadLoop()
	{
		var localStdout = _stdout!;
		var writer      = _lines!.Writer;
		var readToken   = _readLoopCts!.Token;

		_readLoopTask = Task.Factory.StartNew(
			() =>
			{
				try
				{
					while (true)
					{
						string? line;
						try
						{
							// Blocking read on dedicated thread; disposal will unblock.
							line = localStdout.ReadLine();
						}
						catch (ObjectDisposedException)
						{
							break; // stdout disposed -> end gracefully
						}

						if (line is null) break; // EOF

						if (line.Length == 0) continue; // skip empty lines

						// If disposal/cancellation has been requested, stop without enqueuing further lines.
						if (readToken.IsCancellationRequested) break;

						Interlocked.Increment(ref _linesRead);

						// Fast path: try non-blocking write to channel
						if (!writer.TryWrite(line))
						{
							Interlocked.Increment(ref _backpressureEvents);
							// Backpressure path: wait synchronously until we can write or channel completes/cancels
							while (true)
							{
								if (readToken.IsCancellationRequested) throw new OperationCanceledException(readToken);

								var vt = writer.WaitToWriteAsync(readToken);
								bool canWrite = vt.IsCompletedSuccessfully
													? vt.Result
													: vt.AsTask().GetAwaiter().GetResult();

								if (!canWrite) break; // channel completed

								if (writer.TryWrite(line)) break;
							}
						}
					}

					writer.TryComplete();
				}
				catch (OperationCanceledException)
				{
					writer.TryComplete();
				}
				catch (Exception ex)
				{
					Error?.Invoke(ex);
					writer.TryComplete(ex);
				}
			},
			CancellationToken.None,
			TaskCreationOptions.LongRunning,
			TaskScheduler.Default);

		Observe(_readLoopTask);
	}

	private void StartStderrLoopIfNeeded()
	{
		if (_stderr == null) return;

		var localStderr = _stderr;

		_stderrLoopTask = Task.Factory.StartNew(
			() =>
			{
				try
				{
					while (true)
					{
						string? line;
						try
						{
							line = localStderr.ReadLine();
						}
						catch (ObjectDisposedException)
						{
							break;
						}

						if (line is null) break;

						if (line.Length == 0) continue;

						try
						{
							StderrReceived?.Invoke(line);
						}
						catch
						{
							/* best-effort */
						}
					}
				}
				catch (Exception ex)
				{
					Error?.Invoke(ex);
				}
			},
			CancellationToken.None,
			TaskCreationOptions.LongRunning,
			TaskScheduler.Default);

		Observe(_stderrLoopTask);
	}

	private void StartWriteLoop()
	{
		var outgoingReader = _outgoing!.Reader;
		var localStdin     = _stdin!;
		var writeToken     = _writeLoopCts!.Token;

		_writeLoopTask = Task.Factory.StartNew(
			() =>
			{
				try
				{
					var writesSinceFlush = 0;
					int flushBatchSize   = _options.FlushBatchSize > 0 ? _options.FlushBatchSize : 8;

					while (true)
					{
						// Drain everything available
						while (outgoingReader.TryRead(out string? cmd))
						{
							try
							{
								localStdin.WriteLine(cmd);
								Interlocked.Increment(ref _linesWritten);
								if (++writesSinceFlush >= flushBatchSize)
								{
									localStdin.Flush();
									writesSinceFlush = 0;
								}
							}
							catch (ObjectDisposedException)
							{
								return; // stdin disposed -> end gracefully
							}
						}

						// Flush whatever is pending before possibly blocking
						if (writesSinceFlush > 0)
						{
							try
							{
								localStdin.Flush();
							}
							catch
							{
								/* best-effort */
							}

							writesSinceFlush = 0;
						}

						// Await more work or completion synchronously
						var  vt      = outgoingReader.WaitToReadAsync(writeToken);
						bool hasMore = vt.IsCompletedSuccessfully ? vt.Result : vt.AsTask().GetAwaiter().GetResult();
						if (!hasMore) break; // channel completed
					}

					// Final flush on exit
					if (writesSinceFlush > 0)
					{
						try
						{
							localStdin.Flush();
						}
						catch
						{
							/* best-effort */
						}
					}
				}
				catch (OperationCanceledException)
				{
					// graceful cancellation
				}
				catch (Exception ex)
				{
					Error?.Invoke(ex);
				}
			},
			CancellationToken.None,
			TaskCreationOptions.LongRunning,
			TaskScheduler.Default);

		Observe(_writeLoopTask);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private void ThrowIfDisposed()
	{
		if (Volatile.Read(ref _disposed) == 1) throw new ObjectDisposedException(nameof(ProcessUciTransport));
	}

	internal enum TransportStatus
	{
		Created  = 0,
		Starting = 1,
		Started  = 2,
		Stopping = 3,
		Stopped  = 4,
		Disposed = 5
	}
}

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

	public string NewLine { get; init; } = "\n";

	// Preferred time-based grace period; when default (zero), QuitGracePeriodMs is used.
	public TimeSpan QuitGracePeriod { get; init; } = TimeSpan.Zero;
}
