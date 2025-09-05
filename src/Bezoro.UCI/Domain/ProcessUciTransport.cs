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
	private readonly IReadOnlyList<string>?     _args;
	private readonly ProcessUciTransportOptions _options;
	private readonly string                     _path;
	private readonly string?                    _workingDirectory;

	private CancellationTokenSource? _readLoopCts;
	private CancellationTokenSource? _writeLoopCts;

	private Channel<string>? _lines;
	private Channel<string>? _outgoing;

	private int _disposed;
	private int _exitedRaised;
	private int _processAlive;
	private int _readerActive;
	private int _startGate;
	private int _status;
	private int _stopGate;

	private long _backpressureEvents;
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
		if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("Engine path must be provided.", nameof(path));

		_path             = path;
		_workingDirectory = workingDirectory;
		_args             = args is null ? null : new List<string>(args);
		_options          = options ?? new ProcessUciTransportOptions();

		ValidateOptions(_options);
	}

	public bool            IsHealthy          => IsStarted && ProcessIsAlive() && LoopsAreHealthy();
	public bool            IsStarted          => Volatile.Read(ref _status) == (int)TransportStatus.Started;
	public long            BackpressureEvents => Interlocked.Read(ref _backpressureEvents);
	public long            LinesRead          => Interlocked.Read(ref _linesRead);
	public long            LinesWritten       => Interlocked.Read(ref _linesWritten);
	public TransportStatus Status             => (TransportStatus)Volatile.Read(ref _status);

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public async IAsyncEnumerable<string> ReadLinesAsync([EnumeratorCancellation] CancellationToken ct = default)
	{
		ThrowIfDisposed();

		var reader = _lines?.Reader ?? throw new InvalidOperationException("Transport not started.");

		EnsureSingleReader();

		try
		{
			await foreach (string? line in reader.ReadAllAsync(ct).ConfigureAwait(false))
				yield return line;
		}
		finally
		{
			ReleaseReaderIfSingle();
		}
	}

	public async Task StartAsync(CancellationToken ct = default)
	{
		ThrowIfDisposed();

		if (IsStartedAndAlive()) return;

		var existingStart = _startingTcs;
		if (existingStart is { })
		{
			await AwaitExistingStartAsync(existingStart, ct).ConfigureAwait(false);
			return;
		}

		EnsureStartable();

		bool acquired = await AcquireStartGateAsync(ct).ConfigureAwait(false);
		if (!acquired) return;

		var startingSignal = PublishStartingSignal();

		try
		{
			PrepareForStart();
			ct.ThrowIfCancellationRequested();

			string workingDir = ResolveWorkingDirectory();
			ValidateFileSystem(workingDir);

			CleanupExitedProcess();

			await CreateProcessAndRunPipelinesAsync(workingDir, startingSignal, ct).ConfigureAwait(false);
		}
		catch (Exception ex)
		{
			SignalStartFailure(startingSignal, ex, ct);
			await CleanupAfterFailedStartSafeAsync().ConfigureAwait(false);
			ResetStatusIfNeeded();
			Logger.LogException($"UCI engine failed to start. ex={ex}", category: LogCategory.UCI);
			throw;
		}
		finally
		{
			ReleaseStartGate();
		}
	}

	public async Task StopAsync(CancellationToken ct = default)
	{
		ThrowIfDisposed();
		ct.ThrowIfCancellationRequested();

		await AwaitStartIfInProgressAsync(ct).ConfigureAwait(false);

		if (!IsStarted && _process is null)
		{
			Volatile.Write(ref _status, (int)TransportStatus.Stopped);
			Logger.LogInfo("StopAsync: transport not started; no-op.", category: LogCategory.UCI);
			return;
		}

		if (await AwaitExistingStopIfAnyAsync(ct).ConfigureAwait(false)) return;

		var stopTcs = PublishStoppingSignal();

		try
		{
			Interlocked.Exchange(ref _status, (int)TransportStatus.Stopping);
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
			ReleaseStopGate();
		}
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public async Task WriteLineAsync(string line, CancellationToken ct = default)
	{
		ValidateWritePreconditions(line, ct);

		var writer = GetOutgoingWriterOrThrow();

		if (writer.TryWrite(line)) return;

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
		ValidateTryWritePreconditions(line, timeout);

		var writer = GetOutgoingWriterOrThrow();

		if (writer.TryWrite(line)) return true;

		Interlocked.Increment(ref _backpressureEvents);

		if (timeout == TimeSpan.Zero) return false;

		if (ShouldSpinForSmallTimeout(timeout))
			if (SpinUntilWriteOrCancel(writer, line, ct))
				return true;

		try
		{
			if (timeout == Timeout.InfiniteTimeSpan)
			{
				await WriteWithCallerCancellationAsync(writer, line, ct).ConfigureAwait(false);
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
		if (Interlocked.Exchange(ref _disposed, 1) != 0) return;

		Interlocked.Exchange(ref _status, (int)TransportStatus.Stopping);
		Logger.LogInfo("Disposing UCI transport.", category: LogCategory.UCI);

		await TearDownCoreAsync(_options.SendQuitOnDispose, TransportStatus.Disposed, "Disposed UCI transport.")
			.ConfigureAwait(false);
	}

	public void Dispose() => DisposeAsync().AsTask().GetAwaiter().GetResult();

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static bool ShouldSpinForSmallTimeout(TimeSpan timeout) =>
		timeout > TimeSpan.Zero && timeout <= TimeSpan.FromMilliseconds(1);

	private static Task AwaitExistingStartAsync(TaskCompletionSource<object?>? tcs, CancellationToken token) =>
		tcs is null ? Task.CompletedTask : AwaitWithCancellation(tcs.Task, token);

	private static async Task AwaitWithCancellation(Task task, CancellationToken ct)
	{
		if (!ct.CanBeCanceled)
		{
			await task.ConfigureAwait(false);
			return;
		}

		if (task.IsCompleted)
		{
			await task.ConfigureAwait(false);
			return;
		}

		ct.ThrowIfCancellationRequested();

		var       tcs = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
		using var reg = ct.Register(static s => ((TaskCompletionSource<object?>)s!).TrySetResult(null), tcs);

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
		catch { }
	}

	private static Task WaitForProcessExitAsync(Process process, CancellationToken ct)
	{
		try
		{
			if (process.HasExited) return Task.CompletedTask;

			if (!process.EnableRaisingEvents) process.EnableRaisingEvents = true;
		}
		catch (ObjectDisposedException)
		{
			return Task.CompletedTask;
		}

		var tcs = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);

		try
		{
			process.Exited += Handler;
			if (process.HasExited)
			{
				process.Exited -= Handler;
				return Task.CompletedTask;
			}
		}
		catch (ObjectDisposedException)
		{
			return Task.CompletedTask;
		}

		CancellationTokenRegistration reg = default;
		if (ct.CanBeCanceled)
			reg = ct.Register(
				static s =>
				{
					var (src, token) = ((TaskCompletionSource<object?>, CancellationToken))s!;
					src.TrySetCanceled(token);
				},
				(tcs, ct));

		return tcs.Task.ContinueWith(
					  t =>
					  {
						  try
						  {
							  process.Exited -= Handler;
						  }
						  catch { }

						  reg.Dispose();
						  return t;
					  },
					  CancellationToken.None,
					  TaskContinuationOptions.ExecuteSynchronously,
					  TaskScheduler.Default)
				  .Unwrap();

		void Handler(object? _, EventArgs __) => tcs.TrySetResult(null);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static async Task WriteWithCallerCancellationAsync(
		ChannelWriter<string> writer,
		string                line,
		CancellationToken     ct)
	{
		if (!ct.CanBeCanceled) await writer.WriteAsync(line, CancellationToken.None).ConfigureAwait(false);
		else await writer.WriteAsync(line,                   ct).ConfigureAwait(false);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static void SafeCancel(CancellationTokenSource? cts)
	{
		if (cts is null) return;

		try
		{
			cts.Cancel();
		}
		catch { }
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static void SafeDispose(IDisposable? d)
	{
		if (d is null) return;

		try
		{
			d.Dispose();
		}
		catch { }
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static void TryComplete(ChannelWriter<string>? writer, Exception? error = null)
	{
		if (writer is null) return;

		try
		{
			if (error is null) writer.TryComplete();
			else writer.TryComplete(error);
		}
		catch { }
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static void ValidateCommandLine(string line)
	{
		if (line.AsSpan().IndexOfAny('\r', '\n') >= 0)
			throw new ArgumentException("Line must not contain CR or LF characters.", nameof(line));

		if (string.IsNullOrWhiteSpace(line))
			throw new ArgumentException("Command line must not be empty or whitespace.", nameof(line));
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static void ValidateOptions(ProcessUciTransportOptions options)
	{
		if (options.ChannelCapacity <= 0)
			throw new ArgumentOutOfRangeException(
				nameof(options.ChannelCapacity),
				"ChannelCapacity must be greater than 0.");

		if (string.IsNullOrEmpty(options.NewLine))
			throw new ArgumentException("NewLine must be non-empty.", nameof(options.NewLine));

		if (options.QuitGracePeriod < TimeSpan.Zero)
			throw new ArgumentOutOfRangeException(
				nameof(options.QuitGracePeriod),
				"QuitGracePeriod cannot be negative.");

		if (options.QuitGracePeriod == default && options.QuitGracePeriodMs < 0)
			throw new ArgumentOutOfRangeException(
				nameof(options.QuitGracePeriodMs),
				"QuitGracePeriodMs cannot be negative.");
	}

	private bool IsStartedAndAlive() =>
		Volatile.Read(ref _status) == (int)TransportStatus.Started && _process is { HasExited: false };

	private bool LoopsAreHealthy() =>
		(_readLoopTask is null ||
		 !_readLoopTask.IsCompleted && !_readLoopTask.IsFaulted && !_readLoopTask.IsCanceled) &&
		(_writeLoopTask is null ||
		 !_writeLoopTask.IsCompleted && !_writeLoopTask.IsFaulted && !_writeLoopTask.IsCanceled) &&
		(_stderr is null ||
		 _stderrLoopTask is null ||
		 !_stderrLoopTask.IsCompleted && !_stderrLoopTask.IsFaulted && !_stderrLoopTask.IsCanceled) &&
		(_exitNotifyTask is null ||
		 !_exitNotifyTask.IsCompleted && !_exitNotifyTask.IsFaulted && !_exitNotifyTask.IsCanceled);

	private bool ProcessIsAlive() => _process is { HasExited: false };

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private bool SpinUntilWriteOrCancel(ChannelWriter<string> writer, string line, CancellationToken ct)
	{
		var spinner = new SpinWait();
		int spins   = _options.SmallTimeoutSpinIterations;
		for (var i = 0; i < spins; i++)
		{
			if (writer.TryWrite(line)) return true;

			if (ct.IsCancellationRequested) throw new OperationCanceledException(ct);

			spinner.SpinOnce();
		}

		return false;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private ChannelWriter<string> GetOutgoingWriterOrThrow()
	{
		if (_outgoing is null) throw new InvalidOperationException("Transport is not started.");

		return _outgoing.Writer;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private ProcessStartInfo CreateProcessStartInfo(string workingDir)
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
			WorkingDirectory       = workingDir
		};

		if (_args is { Count: > 0 })
			foreach (string? a in _args)
			{
				if (a is { })
					startInfo.ArgumentList.Add(a);
			}

		return startInfo;
	}

	private string ResolveWorkingDirectory() =>
		string.IsNullOrWhiteSpace(_workingDirectory) ? Environment.CurrentDirectory : _workingDirectory;

	private async Task AwaitStartIfInProgressAsync(CancellationToken ct)
	{
		var existingStart = _startingTcs;
		if (existingStart != null) await AwaitWithCancellation(existingStart.Task, ct).ConfigureAwait(false);
	}

	private async Task CleanupAfterFailedStartAsync()
	{
		Volatile.Write(ref _processAlive, 0);

		var rcts = _readLoopCts;
		_readLoopCts = null;
		SafeCancel(rcts);
		SafeDispose(_stdout);
		_stdout = null;
		SafeDispose(_stderr);
		_stderr = null;

		var wcts = _writeLoopCts;
		_writeLoopCts = null;
		SafeCancel(wcts);

		TryComplete(_outgoing?.Writer);
		var writeLoop = _writeLoopTask;
		_writeLoopTask = null;
		try
		{
			if (writeLoop != null)
				await TryAwaitWithTimeout(writeLoop, "write loop during failed start cleanup").ConfigureAwait(false);
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
			ReportError(ex, "Failed to join read loop during failed start cleanup.");
		}

		if (_stderrLoopTask != null)
		{
			try
			{
				await _stderrLoopTask.ConfigureAwait(false);
			}
			catch (Exception ex)
			{
				ReportError(ex, "Failed to join stderr loop during teardown.");
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
						Logger.LogInfo(
							$"Killing UCI engine process during failed start cleanup (tree={_options.KillEntireProcessTree}).",
							category: LogCategory.UCI);
#if NET5_0_OR_GREATER
						p.Kill(_options.KillEntireProcessTree);
#else
						p.Kill();
#endif
					}
				}
				catch (Exception ex)
				{
					Logger.LogException(
						$"Failed to kill process during failed start cleanup. ex={ex}",
						category: LogCategory.UCI);
				}

				var exitNotify = _exitNotifyTask;
				_exitNotifyTask = null;
				if (exitNotify != null)
					try
					{
						await TryAwaitWithTimeout(exitNotify, "exit notification during failed start cleanup")
							.ConfigureAwait(false);
					}
					catch (Exception ex)
					{
						ReportError(ex, "Failed to await exit notification during failed start cleanup.");
					}

				SafeDispose(p);
			}
		}
		catch (Exception ex)
		{
			Error?.Invoke(ex);
		}

		if (Volatile.Read(ref _disposed) == 0 && Volatile.Read(ref _status) != (int)TransportStatus.Failed)
			Volatile.Write(ref _status, (int)TransportStatus.Stopped);
	}

	private async Task CleanupAfterFailedStartSafeAsync()
	{
		try
		{
			await CleanupAfterFailedStartAsync().ConfigureAwait(false);
		}
		catch (Exception cleanupEx)
		{
			Error?.Invoke(cleanupEx);
		}
	}

	private async Task CreateProcessAndRunPipelinesAsync(
		string                        workingDir,
		TaskCompletionSource<object?> startingSignal,
		CancellationToken             token)
	{
		var startInfo      = CreateProcessStartInfo(workingDir);
		var startedProcess = new Process { StartInfo = startInfo, EnableRaisingEvents = true };

		token.ThrowIfCancellationRequested();
		if (!startedProcess.Start()) throw new InvalidOperationException("Failed to start UCI engine process.");

		_process = startedProcess;
		Volatile.Write(ref _processAlive, 1);

		InitializeStreams(startedProcess);
		CreateChannels();
		InitializeCancellationSources();
		StartBackgroundLoops(startedProcess);

		Volatile.Write(ref _status, (int)TransportStatus.Started);

		Logger.LogInfo($"UCI engine started. PID={startedProcess.Id}", category: LogCategory.UCI);

		startingSignal.TrySetResult(null);
		await Task.CompletedTask;
	}

	private async Task TearDownCoreAsync(
		bool              sendQuit,
		TransportStatus   finalStatus,
		string            finalLog,
		CancellationToken ct = default)
	{
		var p = _process;
		_process = null;
		Volatile.Write(ref _processAlive, 0);

		var rcts = _readLoopCts;
		_readLoopCts = null;
		SafeCancel(rcts);
		TryComplete(_lines?.Writer);

		SafeDispose(_stdout);
		_stdout = null;
		SafeDispose(_stderr);
		_stderr = null;

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
			ReportError(ex, "Failed to join write loop during teardown.");
		}
		finally
		{
			SafeDispose(wcts);
		}

		_outgoing = null;

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
					ReportError(ex, "Failed to write quit to stdin during teardown.");
				}

				await SafeDisposeAsync(_stdin).ConfigureAwait(false);
				_stdin = null;
			}
		}
		catch (Exception ex)
		{
			Error?.Invoke(ex);
		}

		var readLoop = _readLoopTask;
		_readLoopTask = null;
		try
		{
			if (readLoop != null) await readLoop.ConfigureAwait(false);
		}
		catch (Exception ex)
		{
			ReportError(ex, "Failed to join read loop during failed start cleanup.");
		}
		finally
		{
			SafeDispose(rcts);
		}

		if (_stderrLoopTask != null)
		{
			try
			{
				await _stderrLoopTask.ConfigureAwait(false);
			}
			catch (Exception ex)
			{
				ReportError(ex, "Failed to join stderr loop during failed start cleanup.");
			}

			_stderrLoopTask = null;
		}

		TryComplete(_lines?.Writer);
		_lines = null;

		try
		{
			if (p is { HasExited: false })
			{
				using var timeoutCts = new CancellationTokenSource(GetQuitGracePeriod());
				try
				{
					await WaitForProcessExitAsync(p, timeoutCts.Token).ConfigureAwait(false);
				}
				catch (OperationCanceledException) { }
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
				Logger.LogInfo(
					$"Killing UCI engine process (tree={_options.KillEntireProcessTree}).",
					category: LogCategory.UCI);
#if NET5_0_OR_GREATER
				p.Kill(_options.KillEntireProcessTree);
#else
				p.Kill();
#endif
			}
		}
		catch (Exception ex)
		{
			Error?.Invoke(ex);
			skipAwaitExit = true;
		}
		finally
		{
			var exitNotify = _exitNotifyTask;
			_exitNotifyTask = null;
			if (!skipAwaitExit && exitNotify != null)
				try
				{
					await TryAwaitWithTimeout(exitNotify, "process exit notification during teardown")
						.ConfigureAwait(false);
				}
				catch (Exception ex)
				{
					ReportError(ex, "Failed to await process exit notification during teardown.");
				}

			SafeDispose(p);

			Volatile.Write(ref _status, (int)finalStatus);
			Logger.LogInfo(finalLog, category: LogCategory.UCI);
		}
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private async Task TryAwaitWithTimeout(Task task, string description)
	{
		if (task is null) return;

		var timeout = _options.TeardownTimeout;
		if (timeout <= TimeSpan.Zero)
		{
			await task.ConfigureAwait(false);
			return;
		}

		Task completed;
		try
		{
			completed = await Task.WhenAny(task, Task.Delay(timeout)).ConfigureAwait(false);
		}
		catch (Exception ex)
		{
			ReportError(ex, "Error while awaiting " + description + ".");
			return;
		}

		if (completed == task) await task.ConfigureAwait(false);
		else
			try
			{
				Logger.LogException(
					$"Timed out awaiting {description}. Timed out waiting for {description} after {timeout}.",
					category: LogCategory.UCI);
			}
			catch { }
	}

	private async Task<bool> AcquireStartGateAsync(CancellationToken token)
	{
		if (Interlocked.CompareExchange(ref _startGate, 1, 0) == 0)
			return true;

		while (true)
		{
			var published = Volatile.Read(ref _startingTcs);
			if (published is { })
			{
				await AwaitWithCancellation(published.Task, token).ConfigureAwait(false);
				return false;
			}

			if (Volatile.Read(ref _startGate) == 0 && Interlocked.CompareExchange(ref _startGate, 1, 0) == 0)
				return true;

			token.ThrowIfCancellationRequested();
			await Task.Yield();
		}
	}

	private async Task<bool> AwaitExistingStopIfAnyAsync(CancellationToken ct)
	{
		var existingStop = _stoppingTcs;
		if (existingStop != null)
		{
			await AwaitWithCancellation(existingStop.Task, ct).ConfigureAwait(false);
			return true;
		}

		if (Interlocked.CompareExchange(ref _stopGate, 1, 0) != 0)
			while (true)
			{
				existingStop = Volatile.Read(ref _stoppingTcs);
				if (existingStop != null)
				{
					await AwaitWithCancellation(existingStop.Task, ct).ConfigureAwait(false);
					return true;
				}

				if (Volatile.Read(ref _stopGate) == 0 && Interlocked.CompareExchange(ref _stopGate, 1, 0) == 0)
					break;

				ct.ThrowIfCancellationRequested();
				await Task.Yield();
			}

		return false;
	}

	private TaskCompletionSource<object?> PublishStartingSignal()
	{
		var tcs = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
		Volatile.Write(ref _startingTcs, tcs);
		return tcs;
	}

	private TaskCompletionSource<object?> PublishStoppingSignal()
	{
		var tcs = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
		Volatile.Write(ref _stoppingTcs, tcs);
		return tcs;
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
			SafeDispose(_process);
		}
		catch { }
		finally
		{
			_process        = null;
			_exitNotifyTask = null;
		}
	}

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

	private void EnsureSingleReader()
	{
		if (!_options.SingleReader) return;

		if (Interlocked.CompareExchange(ref _readerActive, 1, 0) != 0)
			throw new InvalidOperationException("Only a single reader is supported for this transport.");
	}

	private void EnsureStartable()
	{
		int status = Volatile.Read(ref _status);
		if (status is (int)TransportStatus.Created or (int)TransportStatus.Stopped) return;

		Interlocked.Exchange(ref _status, (int)TransportStatus.Failed);
		throw new InvalidOperationException("Transport cannot be started in its current state.");
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private void FireExitedOnce(int? exitCode, string? error)
	{
		if (Interlocked.Exchange(ref _exitedRaised, 1) != 0) return;

		var exited = Exited;
		if (exited != null)
			try
			{
				exited(exitCode, error);
			}
			catch (Exception handlerEx)
			{
				ReportError(handlerEx, "Exited event handler threw.");
			}
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
							 (_options.RedirectStandardError ? process.StandardError.CurrentEncoding : Encoding.UTF8);

		_stdin = new(process.StandardInput.BaseStream, stdinEncoding, 64 * 1024, true)
		{
			NewLine   = _options.NewLine,
			AutoFlush = false
		};

		_stdout = new(process.StandardOutput.BaseStream, stdoutEncoding, false, 64 * 1024, true);

		_stderr = _options.RedirectStandardError
					  ? new StreamReader(process.StandardError.BaseStream, stderrEncoding, false, 32 * 1024, true)
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
					var agg = t.Exception!.Flatten();
					var ex  = agg.InnerExceptions.Count == 1 ? agg.InnerExceptions[0] : agg;
					ReportError(ex, "Background task faulted.");
				}
				catch { }
			},
			CancellationToken.None,
			TaskContinuationOptions.ExecuteSynchronously | TaskContinuationOptions.OnlyOnFaulted,
			TaskScheduler.Default);
	}

	private void PrepareForStart()
	{
		Volatile.Write(ref _exitedRaised, 0);
		Interlocked.Exchange(ref _status, (int)TransportStatus.Starting);
		Logger.LogInfo("Starting UCI engine process.", category: LogCategory.UCI);
	}

	private void ReleaseReaderIfSingle()
	{
		if (_options.SingleReader) Volatile.Write(ref _readerActive, 0);
	}

	private void ReleaseStartGate()
	{
		Interlocked.Exchange(ref _startGate, 0);
		Volatile.Write(ref _startingTcs, null);
	}

	private void ReleaseStopGate()
	{
		Interlocked.Exchange(ref _stopGate, 0);
		Volatile.Write(ref _stoppingTcs, null);
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

	private void ResetStatusIfNeeded()
	{
		if (Volatile.Read(ref _disposed) == 0 && Volatile.Read(ref _status) != (int)TransportStatus.Failed)
			Volatile.Write(ref _status, (int)TransportStatus.Stopped);
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
					Volatile.Write(ref _processAlive, 0);
					int exitCode = process.ExitCode;
					Logger.LogInfo($"UCI engine process exited with code {exitCode}.", category: LogCategory.UCI);
					FireExitedOnce(exitCode, null);
				}
				catch (Exception ex)
				{
					ReportError(ex, "Error while waiting for process exit.");
					FireExitedOnce(null, ex.Message);
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
							line = localStdout.ReadLine();
						}
						catch (ObjectDisposedException)
						{
							break;
						}

						if (line is null) break;

						if (line.Length == 0) continue;

						if (readToken.IsCancellationRequested) break;

						Interlocked.Increment(ref _linesRead);

						if (!writer.TryWrite(line))
						{
							Interlocked.Increment(ref _backpressureEvents);
							while (true)
							{
								if (readToken.IsCancellationRequested) throw new OperationCanceledException(readToken);

								var vt = writer.WaitToWriteAsync(readToken);
								bool canWrite = vt.IsCompletedSuccessfully
													? vt.Result
													: vt.AsTask().GetAwaiter().GetResult();

								if (!canWrite) break;
								if (writer.TryWrite(line)) break;
							}
						}
					}

					TryComplete(writer);
				}
				catch (OperationCanceledException)
				{
					TryComplete(writer);
				}
				catch (Exception ex)
				{
					ReportError(ex, "Read loop faulted.");
					TryComplete(writer, ex);
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
							var handler = StderrReceived;
							if (handler != null)
								Task.Run(() =>
								{
									try
									{
										handler(line);
									}
									catch { }
								});
						}
						catch { }
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
		if (_options.DisableWriteLoop) return;

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
								return;
							}
						}

						if (writesSinceFlush > 0)
						{
							try
							{
								localStdin.Flush();
							}
							catch { }

							writesSinceFlush = 0;
						}

						var  vt      = outgoingReader.WaitToReadAsync(writeToken);
						bool hasMore = vt.IsCompletedSuccessfully ? vt.Result : vt.AsTask().GetAwaiter().GetResult();
						if (!hasMore) break;
					}

					if (writesSinceFlush > 0)
						try
						{
							localStdin.Flush();
						}
						catch { }
				}
				catch (OperationCanceledException) { }
				catch (Exception ex)
				{
					ReportError(ex, "Write loop faulted.");
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

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private void ThrowIfProcessNotAlive()
	{
		if (Volatile.Read(ref _processAlive) == 0 || _process is { HasExited: true })
			throw new InvalidOperationException("Engine process has exited.");
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private void ThrowIfProcessNotStarted()
	{
		if (Volatile.Read(ref _status) != (int)TransportStatus.Started)
			throw new InvalidOperationException("Transport is not started.");
	}

	private void ValidateFileSystem(string workingDir)
	{
		if (!File.Exists(_path))
		{
			Interlocked.Exchange(ref _status, (int)TransportStatus.Failed);
			throw new ArgumentException($"Engine executable not found at path: {_path}", nameof(_path));
		}

		if (Directory.Exists(workingDir)) return;

		Interlocked.Exchange(ref _status, (int)TransportStatus.Failed);
		throw new ArgumentException($"Working directory does not exist: {workingDir}", nameof(_workingDirectory));
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private void ValidateTryWritePreconditions(string line, TimeSpan timeout)
	{
		ThrowIfProcessNotStarted();
		ThrowIfDisposed();
		ThrowIfProcessNotAlive();
		if (line is null) throw new ArgumentNullException(nameof(line));
		if (timeout < TimeSpan.Zero && timeout != Timeout.InfiniteTimeSpan)
			throw new ArgumentOutOfRangeException(nameof(timeout));
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private void ValidateWritePreconditions(string line, CancellationToken ct)
	{
		ThrowIfProcessNotStarted();
		ThrowIfDisposed();
		ThrowIfProcessNotAlive();
		if (line is null) throw new ArgumentNullException(nameof(line));

		if (_options.ValidateCommands) ValidateCommandLine(line);
		ct.ThrowIfCancellationRequested();
	}

	internal enum TransportStatus
	{
		Created   = 0,
		Starting  = 1,
		Started   = 2,
		Stopping  = 3,
		Stopped   = 4,
		Disposing = 5,
		Disposed  = 6,
		Failed    = 7,
		Canceled  = 8
	}
}
