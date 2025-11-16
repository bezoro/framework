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

	public async Task<bool> TryWriteLineAsync(string line, TimeSpan timeout, CancellationToken ct = default)
	{
		ValidateTryWritePreconditions(line, timeout);

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

		_stateManager.SetStatus(TransportStatus.Stopping);
		Logger.LogInfo("Disposing UCI transport.", category: LogCategory.UCI);

		await TearDownCoreAsync(_options.SendQuitOnDispose, TransportStatus.Disposed, "Disposed UCI transport.")
			.ConfigureAwait(false);
	}

	/// <summary>
	/// Synchronous dispose implementation. Prefer <see cref="DisposeAsync"/> for async scenarios.
	/// </summary>
	public void Dispose() => DisposeAsync().AsTask().GetAwaiter().GetResult();

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

	private async Task CleanupAfterFailedStartAsync()
	{
		_stateManager.MarkProcessAlive(false);

		_loopManager.CancelReadLoop();
		StreamInitializer.SafeDispose(_streams.Stdout);
		_streams.Stdout = null;
		StreamInitializer.SafeDispose(_streams.Stderr);
		_streams.Stderr = null;

		_loopManager.CancelWriteLoop();

		ChannelFactory.TryComplete(_outgoing?.Writer);
		await _loopManager.AwaitWriteLoopAsync().ConfigureAwait(false);
		_loopManager.DisposeCancellationSources();

		_outgoing = null;

		if (_streams.Stdin != null)
		{
			await StreamInitializer.SafeDisposeAsync(_streams.Stdin).ConfigureAwait(false);
			_streams.Stdin = null;
		}

		await _loopManager.AwaitReadLoopAsync().ConfigureAwait(false);
		await _loopManager.AwaitStderrLoopAsync().ConfigureAwait(false);

		ChannelFactory.TryComplete(_lines?.Writer);
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

						ProcessHelper.SafeKillProcess(p, _options.KillEntireProcessTree);
					}
				}
				catch (Exception ex)
				{
					Logger.LogException(
						$"Failed to kill process during failed start cleanup. ex={ex}",
						category: LogCategory.UCI);
				}

				await _loopManager.AwaitExitNotificationAsync(_options.TeardownTimeout).ConfigureAwait(false);

				ProcessHelper.SafeDisposeProcess(p);
			}
		}
		catch (Exception ex)
		{
			Error?.Invoke(ex);
		}

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

	private async Task TearDownCoreAsync(
		bool              sendQuit,
		TransportStatus   finalStatus,
		string            finalLog,
		CancellationToken ct = default)
	{
		// Capture process reference before nulling the field to ensure we can safely
		// await operations on it even after _process is set to null.
		var p = _process;
		_process = null;
		_stateManager.MarkProcessAlive(false);

		_loopManager.CancelReadLoop();
		ChannelFactory.TryComplete(_lines?.Writer);

		StreamInitializer.SafeDispose(_streams.Stdout);
		_streams.Stdout = null;
		StreamInitializer.SafeDispose(_streams.Stderr);
		_streams.Stderr = null;

		_loopManager.CancelWriteLoop();

		ChannelFactory.TryComplete(_outgoing?.Writer);
		await _loopManager.AwaitWriteLoopAsync().ConfigureAwait(false);
		_loopManager.DisposeCancellationSources();

		_outgoing = null;

		try
		{
			if (_streams.Stdin != null)
			{
				try
				{
					if (sendQuit)
					{
						await _streams.Stdin.WriteLineAsync("quit").ConfigureAwait(false);
						await _streams.Stdin.FlushAsync().ConfigureAwait(false);
						_options.OnQuitSent?.Invoke();
					}
				}
				catch (Exception ex)
				{
					ReportError(ex, "Failed to write quit to stdin during teardown.");
				}

				await StreamInitializer.SafeDisposeAsync(_streams.Stdin).ConfigureAwait(false);
				_streams.Stdin = null;
			}
		}
		catch (Exception ex)
		{
			Error?.Invoke(ex);
		}

		await _loopManager.AwaitReadLoopAsync().ConfigureAwait(false);
		await _loopManager.AwaitStderrLoopAsync().ConfigureAwait(false);

		// Complete the lines channel after read loop has finished to ensure no more data is written.
		ChannelFactory.TryComplete(_lines?.Writer);
		_lines = null;

		var skipAwaitExit = false;

		try
		{
			if (p is { HasExited: false })
			{
				try
				{
					using var timeoutCts = new CancellationTokenSource(GetQuitGracePeriod());
					try
					{
						await ProcessHelper.WaitForProcessExitAsync(p, timeoutCts.Token).ConfigureAwait(false);
					}
					catch (OperationCanceledException) { }
				}
				catch (Exception ex)
				{
					Error?.Invoke(ex);
				}

				// Re-check after grace period - process may have exited
				if (p is { HasExited: false })
				{
					try
					{
						Logger.LogInfo(
							$"Killing UCI engine process (tree={_options.KillEntireProcessTree}).",
							category: LogCategory.UCI);

						ProcessHelper.SafeKillProcess(p, _options.KillEntireProcessTree);
					}
					catch (Exception ex)
					{
						Error?.Invoke(ex);
						skipAwaitExit = true;
					}
				}
			}
		}
		finally
		{
			if (!skipAwaitExit)
				await _loopManager.AwaitExitNotificationAsync(_options.TeardownTimeout).ConfigureAwait(false);

			ProcessHelper.SafeDisposeProcess(p);

			_stateManager.SetStatus(finalStatus);
			Logger.LogInfo(finalLog, category: LogCategory.UCI);
		}
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
				ReportError(handlerEx, "Exited event handler threw.");
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

		// Use LINQ to validate and materialize in one pass for better performance with large collections.
		var list = args.ToList();
		if (list.Any(arg => arg is null))
			throw new ArgumentException("Argument list cannot contain null values.", nameof(args));

		return list;
	}
}
