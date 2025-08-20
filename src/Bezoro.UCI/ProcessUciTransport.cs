using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace Bezoro.UCI;

internal sealed class ProcessUciTransport : IUciTransport
{
	private readonly IReadOnlyList<string>?     _args;
	private readonly ProcessUciTransportOptions _options;

	private readonly string  _path;
	private readonly string? _workingDirectory;

	// 0 = false, 1 = true (cross-thread visibility)
	private int                      _disposed;
	private CancellationTokenSource? _readLoopCts;
	private CancellationTokenSource? _writeLoopCts;

	private Channel<string>? _lines;
	private Channel<string>? _outgoing;
	private int              _isStarted; // 0 = false, 1 = true (cross-thread visibility)
	private int              _readerActive;
	private int              _startGate; // prevents concurrent StartAsync calls
	private int              _stopGate;  // prevents concurrent StopAsync calls

	internal enum TransportStatus
	{
		Created  = 0,
		Starting = 1,
		Started  = 2,
		Stopping = 3,
		Stopped  = 4,
		Disposed = 5
	}

	private int _status; // TransportStatus stored as int for interlocked/volatile

	private Process?      _process;
	private StreamReader? _stderr;
	private StreamReader? _stdout;
	private StreamWriter? _stdin;
	private Task?         _exitNotifyTask;
	private Task?         _readLoopTask;
	private Task?         _stderrLoopTask;
	private Task?         _writeLoopTask;

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
	///     Threading: invoked on a ThreadPool thread; exceptions thrown by handlers are swallowed.
	/// </summary>
	public event Action<string>? StderrReceived;

	public ProcessUciTransport(string path, IEnumerable<string>? args = null, string? workingDirectory = null)
		: this(path, args, workingDirectory, null) { }

	public ProcessUciTransport(
		string path,
		IEnumerable<string>? args,
		string? workingDirectory,
		ProcessUciTransportOptions? options)
	{
		if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("Engine path must be provided.", nameof(path));

		_path             = path;
		_workingDirectory = workingDirectory;
		_args             = args is null ? null : new List<string>(args);
		_options          = options ?? new ProcessUciTransportOptions();
	}

	public bool IsStarted => Volatile.Read(ref _isStarted) == 1;

	/// <summary>
	///     Current transport status. This reflects internal lifecycle transitions.
	/// </summary>
	public TransportStatus Status => (TransportStatus)Volatile.Read(ref _status);

	/// <summary>
	///     Best-effort health indicator.
	///     True when started, the underlying process hasn't exited, and background loops are still running.
	/// </summary>
	public bool IsHealthy =>
		IsStarted &&
		_process is { HasExited: false } &&
		(_readLoopTask is null || !_readLoopTask.IsCompleted) &&
		(_writeLoopTask is null || !_writeLoopTask.IsCompleted);

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

		// Ensure valid state to start
		int currentStatus = Volatile.Read(ref _status);
		if (currentStatus is not (int)TransportStatus.Created and not (int)TransportStatus.Stopped)
			throw new InvalidOperationException("Transport cannot be started in its current state.");

		// Prevent concurrent starts
		if (Interlocked.CompareExchange(ref _startGate, 1, 0) != 0)
			throw new InvalidOperationException("Transport is already starting.");

		if (_process != null) throw new InvalidOperationException("Transport already started.");

		// Transition to Starting
		Interlocked.Exchange(ref _status, (int)TransportStatus.Starting);
		_options.Logger?.LogInfo("Starting UCI engine process.");

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

		if (_args is { Count: > 0 })
			// Avoid quoting issues by using ArgumentList
			foreach (string? a in _args)
			{
				if (a is null) continue; // defensively skip nulls

				startInfo.ArgumentList.Add(a);
			}

		var process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };

		try
		{
			ct.ThrowIfCancellationRequested();

			if (!process.Start())
				throw new InvalidOperationException("Failed to start UCI engine process.");

			_process = process;

			// Initialize stdin with optional custom encoding
			if (_options.StdinEncoding is null)
			{
				_stdin = process.StandardInput;
			}
			else
			{
				_stdin = new(process.StandardInput.BaseStream, _options.StdinEncoding);
			}
			_stdin.NewLine   = _options.NewLine;
			_stdin.AutoFlush = true;

			_stdout = process.StandardOutput;
			_stderr = _options.RedirectStandardError ? process.StandardError : null;

			// Prepare incoming channel and background read loop
			_lines = Channel.CreateBounded<string>(
				new BoundedChannelOptions(_options.ChannelCapacity)
				{
					SingleWriter = true,
					SingleReader = _options.SingleReader,
					FullMode     = BoundedChannelFullMode.Wait
				});

			// Prepare outgoing channel and background write loop
			_outgoing = Channel.CreateBounded<string>(
				new BoundedChannelOptions(_options.ChannelCapacity)
				{
					SingleWriter = false, // multiple producers allowed
					SingleReader = true,
					FullMode     = BoundedChannelFullMode.Wait
				});

			_readLoopCts  = new();
			_writeLoopCts = new();

			var localStdout = _stdout!;
			var writer      = _lines.Writer;
			var readToken   = _readLoopCts!.Token;

			_readLoopTask = Task.Run(
				async () =>
				{
					try
					{
						while (true)
						{
							string? line;
							try
							{
								// StreamReader.ReadLineAsync in netstandard2.1 has no CancellationToken; disposal will unblock.
								line = await localStdout.ReadLineAsync().ConfigureAwait(false);
							}
							catch (ObjectDisposedException)
							{
								break; // stdout disposed -> end gracefully
							}

							if (line is null) break; // EOF

							if (line.Length == 0) continue; // skip empty lines

							await writer.WriteAsync(line, readToken).ConfigureAwait(false);
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
				CancellationToken.None);

			Observe(_readLoopTask);

			// Start background writer loop
			var outgoingReader = _outgoing!.Reader;
			var localStdin     = _stdin!;
			var writeToken     = _writeLoopCts!.Token;

			_writeLoopTask = Task.Run(
				async () =>
				{
					try
					{
						await foreach (string cmd in outgoingReader.ReadAllAsync(writeToken).ConfigureAwait(false))
						{
							try
							{
								await localStdin.WriteLineAsync(cmd).ConfigureAwait(false);
							}
							catch (ObjectDisposedException)
							{
								break; // stdin disposed -> end gracefully
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
				CancellationToken.None);

			Observe(_writeLoopTask);

			if (_stderr != null)
			{
				var localStderr = _stderr;

				_stderrLoopTask = Task.Run(
					async () =>
					{
						try
						{
							while (true)
							{
								string? line;
								try
								{
									line = await localStderr.ReadLineAsync().ConfigureAwait(false);
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
					CancellationToken.None);

				Observe(_stderrLoopTask);
			}

			_exitNotifyTask = Task.Run(
				async () =>
				{
					try
					{
						await WaitForProcessExitAsync(process, CancellationToken.None).ConfigureAwait(false);
						int exitCode = process.ExitCode;
						_options.Logger?.LogInfo($"UCI engine process exited with code {exitCode}.");
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

			Volatile.Write(ref _isStarted, 1);
			Volatile.Write(ref _status,    (int)TransportStatus.Started);
			_options.Logger?.LogInfo($"UCI engine started. PID={process?.Id.ToString() ?? "n/a"}");
		}
		catch
		{
			// If startup fails or is cancelled, tear down any partially started resources without disposing the transport.
			try
			{
				await CleanupAfterFailedStartAsync().ConfigureAwait(false);
			}
			catch (Exception ex)
			{
				Error?.Invoke(ex);
			}

			if (Volatile.Read(ref _disposed) == 0)
				Volatile.Write(ref _status, (int)TransportStatus.Stopped);

			_options.Logger?.LogError(
				new InvalidOperationException("Engine start failed."),
				"UCI engine failed to start.");
			throw;
		}
		finally
		{
			// Always release the start gate
			Interlocked.Exchange(ref _startGate, 0);
		}
	}

	public async Task WriteLineAsync(string line, CancellationToken ct = default)
	{
		ThrowIfDisposed();
		if (_process is { HasExited: true }) throw new InvalidOperationException("Engine process has exited.");
		if (line is null) throw new ArgumentNullException(nameof(line));

		// UCI is line-oriented; ensure no CR/LF in payload
		if (line.AsSpan().IndexOfAny('\r', '\n') >= 0)
			throw new ArgumentException("Line must not contain CR or LF characters.", nameof(line));

		// Also forbid empty/whitespace-only commands
		if (string.IsNullOrWhiteSpace(line))
			throw new ArgumentException("Command line must not be empty or whitespace.", nameof(line));

		var writer = _outgoing?.Writer ?? throw new InvalidOperationException("Transport not started.");
		await writer.WriteAsync(line, ct).ConfigureAwait(false);
	}

	public async Task<bool> TryWriteLineAsync(string line, TimeSpan timeout, CancellationToken ct = default)
	{
		ThrowIfDisposed();
		if (_process is { HasExited: true }) throw new InvalidOperationException("Engine process has exited.");
		if (line is null) throw new ArgumentNullException(nameof(line));
		if (timeout < TimeSpan.Zero && timeout != Timeout.InfiniteTimeSpan)
			throw new ArgumentOutOfRangeException(nameof(timeout));

		// UCI is line-oriented; ensure no CR/LF in payload
		if (line.AsSpan().IndexOfAny('\r', '\n') >= 0)
			throw new ArgumentException("Line must not contain CR or LF characters.", nameof(line));

		// Also forbid empty/whitespace-only commands
		if (string.IsNullOrWhiteSpace(line))
			throw new ArgumentException("Command line must not be empty or whitespace.", nameof(line));

		var writer = _outgoing?.Writer ?? throw new InvalidOperationException("Transport not started.");

		// Fast path: try non-blocking
		if (writer.TryWrite(line)) return true;

		// Bounded wait path with timeout and cancellation
		using var timeoutCts = timeout == Timeout.InfiniteTimeSpan ? null : new CancellationTokenSource(timeout);
		using var linked = timeoutCts is null
							   ? CancellationTokenSource.CreateLinkedTokenSource(ct)
							   : CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);
		try
		{
			await writer.WriteAsync(line, linked.Token).ConfigureAwait(false);
			return true;
		}
		catch (OperationCanceledException)
		{
			// If caller requested cancellation, propagate
			if (ct.IsCancellationRequested) throw;
			// Otherwise, it was the timeout
			return false;
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

		// Prevent concurrent stops
		if (Interlocked.CompareExchange(ref _stopGate, 1, 0) != 0)
			throw new InvalidOperationException("Transport is already stopping.");

		try
		{
			Interlocked.Exchange(ref _status, (int)TransportStatus.Stopping);
			_options.Logger?.LogInfo("Stopping UCI transport.");

			var p = _process;
			_process = null;

			// Cancel read/stderr loops up-front
			var rcts = _readLoopCts;
			_readLoopCts = null;
			try
			{
				rcts?.Cancel();
			}
			catch (Exception ex)
			{
				ReportError(ex, "Failed to cancel read loop CTS during StopAsync.");
			}

			// Dispose stdout early to unblock any pending ReadLineAsync
			try
			{
				_stdout?.Dispose();
			}
			catch (Exception ex)
			{
				ReportError(ex, "Failed to dispose stdout during StopAsync.");
			}

			_stdout = null;

			// Dispose stderr early as well to unblock any pending ReadLineAsync in the stderr loop
			try
			{
				_stderr?.Dispose();
			}
			catch (Exception ex)
			{
				ReportError(ex, "Failed to dispose stderr during StopAsync.");
			}

			_stderr = null;

			// Stop writer loop and complete outgoing channel
			var wcts = _writeLoopCts;
			_writeLoopCts = null;
			try
			{
				wcts?.Cancel();
			}
			catch (Exception ex)
			{
				ReportError(ex, "Failed to cancel write loop CTS during StopAsync.");
			}

			try
			{
				_outgoing?.Writer.TryComplete();
			}
			catch (Exception ex)
			{
				ReportError(ex, "Failed to complete outgoing channel during StopAsync.");
			}

			var writeLoop = _writeLoopTask;
			_writeLoopTask = null;
			try
			{
				if (writeLoop != null) await writeLoop.ConfigureAwait(false);
			}
			catch (Exception ex)
			{
				ReportError(ex, "Writer loop failed during StopAsync.");
			}

			try
			{
				wcts?.Dispose();
			}
			catch (Exception ex)
			{
				ReportError(ex, "Failed to dispose write loop CTS during StopAsync.");
			}

			_outgoing = null;

			// Politely request engine shutdown and signal EOF
			try
			{
				if (_stdin != null)
				{
					try
					{
						if (_options.SendQuitOnStop)
						{
							await _stdin.WriteLineAsync("quit").ConfigureAwait(false);
							await _stdin.FlushAsync().ConfigureAwait(false);
						}
					}
					catch (Exception ex)
					{
						ReportError(ex, "Failed to write quit during StopAsync.");
					}

					try
					{
						await _stdin.DisposeAsync().ConfigureAwait(false);
					}
					catch (Exception ex)
					{
						ReportError(ex, "Failed to dispose stdin during StopAsync.");
					}

					_stdin = null;
				}
			}
			catch (Exception ex)
			{
				ReportError(ex, "Unexpected error while finalizing stdin during StopAsync.");
			}

			// Await background loops
			var readLoop = _readLoopTask;
			_readLoopTask = null;
			try
			{
				if (readLoop != null) await readLoop.ConfigureAwait(false);
			}
			catch (Exception ex)
			{
				ReportError(ex, "Read loop failed during StopAsync.");
			}

			try
			{
				rcts?.Dispose();
			}
			catch (Exception ex)
			{
				ReportError(ex, "Failed to dispose read loop CTS during StopAsync.");
			}

			if (_stderrLoopTask != null)
			{
				try
				{
					await _stderrLoopTask.ConfigureAwait(false);
				}
				catch (Exception ex)
				{
					ReportError(ex, "Stderr loop failed during StopAsync.");
				}

				_stderrLoopTask = null;
			}

			// Complete channel so any consumers finish
			try
			{
				_lines?.Writer.TryComplete();
			}
			catch (Exception ex)
			{
				ReportError(ex, "Failed to complete incoming channel during StopAsync.");
			}

			_lines = null;

			// Give the process a brief chance to exit cleanly
			try
			{
				if (p is { HasExited: false })
				{
					var grace = _options.QuitGracePeriod != default
									? _options.QuitGracePeriod
									: TimeSpan.FromMilliseconds(_options.QuitGracePeriodMs);

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
				ReportError(ex, "Error while waiting for process to exit during StopAsync.");
			}

			var skipAwaitExit = false;

			try
			{
				if (p is { HasExited: false })
					// Final fallback if engine ignored quit/EOF
					try
					{
						_options.Logger?.LogInfo(
							$"Killing UCI engine process from StopAsync (tree={_options.KillEntireProcessTree}).");
#if NET5_0_OR_GREATER
						p.Kill(_options.KillEntireProcessTree);
#else
						p.Kill();
#endif
					}
					catch (Exception ex)
					{
						ReportError(ex, "Failed to kill process during StopAsync.");
						// If Kill fails (permissions/lifecycle constraints), do not await exit notification.
						skipAwaitExit = true;
					}
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
						ReportError(ex, "Exit notification task failed during StopAsync.");
					}

				try
				{
					p?.Dispose();
				}
				catch (Exception ex)
				{
					ReportError(ex, "Failed to dispose Process during StopAsync.");
				}

				Volatile.Write(ref _isStarted, 0);
				Volatile.Write(ref _status,    (int)TransportStatus.Stopped);
				_options.Logger?.LogInfo("Stopped UCI transport.");
			}
		}
		finally
		{
			// Always release the stop gate
			Interlocked.Exchange(ref _stopGate, 0);
		}
	}

	public async ValueTask DisposeAsync()
	{
		if (Interlocked.Exchange(ref _disposed, 1) != 0) return;

		// Transition to Stopping
		Interlocked.Exchange(ref _status, (int)TransportStatus.Stopping);
		_options.Logger?.LogInfo("Disposing UCI transport.");

		var p = _process;
		_process = null;

		// Cancel read/stderr loops up-front
		var cts = _readLoopCts;
		_readLoopCts = null;
		try
		{
			cts?.Cancel();
		}
		catch (Exception ex)
		{
			Error?.Invoke(ex);
		}

		// Dispose stdout early to unblock any pending ReadLineAsync
		try
		{
			_stdout?.Dispose();
		}
		catch (Exception ex)
		{
			Error?.Invoke(ex);
		}

		_stdout = null;

		// Dispose stderr early as well to unblock any pending ReadLineAsync in the stderr loop
		try
		{
			_stderr?.Dispose();
		}
		catch (Exception ex)
		{
			Error?.Invoke(ex);
		}

		_stderr = null;

		// Stop writer loop and complete outgoing channel
		var wcts = _writeLoopCts;
		_writeLoopCts = null;
		try
		{
			wcts?.Cancel();
		}
		catch (Exception ex)
		{
			Error?.Invoke(ex);
		}

		try
		{
			_outgoing?.Writer.TryComplete();
		}
		catch (Exception ex)
		{
			Error?.Invoke(ex);
		}

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
			try
			{
				wcts?.Dispose();
			}
			catch (Exception ex)
			{
				Error?.Invoke(ex);
			}
		}

		_outgoing = null;

		// Politely request engine shutdown and signal EOF
		try
		{
			if (_stdin != null)
			{
				try
				{
					if (_options.SendQuitOnDispose)
					{
						await _stdin.WriteLineAsync("quit").ConfigureAwait(false);
						await _stdin.FlushAsync().ConfigureAwait(false);
					}
				}
				catch (Exception ex)
				{
					Error?.Invoke(ex);
				}

				try
				{
					await _stdin.DisposeAsync().ConfigureAwait(false); // signals EOF
				}
				catch (Exception ex)
				{
					Error?.Invoke(ex);
				}

				_stdin = null;
			}
		}
		catch (Exception ex)
		{
			Error?.Invoke(ex);
		}

		// Await background loops
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
			try
			{
				cts?.Dispose();
			}
			catch (Exception ex)
			{
				Error?.Invoke(ex);
			}
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

		// Complete channel so any consumers finish
		try
		{
			_lines?.Writer.TryComplete();
		}
		catch (Exception ex)
		{
			Error?.Invoke(ex);
		}

		_lines = null;

		// Give the process a brief chance to exit cleanly
		try
		{
			if (p is { HasExited: false })
			{
				var grace = _options.QuitGracePeriod != default
								? _options.QuitGracePeriod
								: TimeSpan.FromMilliseconds(_options.QuitGracePeriodMs);

				using var timeoutCts = new CancellationTokenSource(grace);
				try
				{
					await WaitForProcessExitAsync(p, timeoutCts.Token).ConfigureAwait(false);
				}
				catch (OperationCanceledException)
				{
					// timed out
				}
			}
		}
		catch (Exception ex)
		{
			Error?.Invoke(ex);
		}

		if (p is null)
		{
			Volatile.Write(ref _isStarted, 0);
			Volatile.Write(ref _status,    (int)TransportStatus.Disposed);
			return;
		}

		var skipAwaitExit = false;

		try
		{
			if (!p.HasExited)
				// Final fallback if engine ignored quit/EOF
				try
				{
					_options.Logger?.LogInfo($"Killing UCI engine process (tree={_options.KillEntireProcessTree}).");
#if NET5_0_OR_GREATER
					p.Kill(_options.KillEntireProcessTree);
#else
					p.Kill();
#endif
				}
				catch (Exception ex)
				{
					_options.Logger?.LogError(ex, "Failed to kill UCI engine process.");
					// If Kill fails (permissions/lifecycle constraints), do not await exit notification.
					skipAwaitExit = true;
				}
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

			try
			{
				// Disposing Process synchronously; no need to offload to thread pool.
				p.Dispose();
			}
			catch (Exception ex)
			{
				Error?.Invoke(ex);
			}

			Volatile.Write(ref _isStarted, 0);
			Volatile.Write(ref _status,    (int)TransportStatus.Disposed);
			_options.Logger?.LogInfo("Disposed UCI transport.");
		}
	}

	private static Task WaitForProcessExitAsync(Process process, CancellationToken ct)
	{
		var tcs = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);

		void Handler(object? _, EventArgs __)
		{
			tcs.TrySetResult(null);
		}

		if (!process.EnableRaisingEvents) process.EnableRaisingEvents = true;
		process.Exited += Handler;

		// If the process already exited after subscribing, complete immediately.
		if (process.HasExited)
		{
			process.Exited -= Handler;
			return Task.CompletedTask;
		}

		CancellationTokenRegistration reg = default;
		if (ct.CanBeCanceled) reg         = ct.Register(() => tcs.TrySetCanceled(ct));

		return tcs.Task.ContinueWith(
			t =>
			{
				process.Exited -= Handler;
				reg.Dispose();
				return t;
			},
			CancellationToken.None,
			TaskContinuationOptions.ExecuteSynchronously,
			TaskScheduler.Default).Unwrap();
	}

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

	private async Task CleanupAfterFailedStartAsync()
	{
		// Do not set _disposed here; keep the transport reusable.
		var cts = _readLoopCts;
		_readLoopCts = null;
		try
		{
			cts?.Cancel();
		}
		catch (Exception ex)
		{
			Error?.Invoke(ex);
		}

		try
		{
			_stdout?.Dispose();
		}
		catch (Exception ex)
		{
			Error?.Invoke(ex);
		}

		_stdout = null;

		// Dispose stderr early as well to unblock any pending ReadLineAsync in the stderr loop (failed-start cleanup)
		try
		{
			_stderr?.Dispose();
		}
		catch (Exception ex)
		{
			Error?.Invoke(ex);
		}

		_stderr = null;

		// Stop writer loop and complete outgoing channel (failed-start cleanup)
		var wcts = _writeLoopCts;
		_writeLoopCts = null;
		try
		{
			wcts?.Cancel();
		}
		catch (Exception ex)
		{
			Error?.Invoke(ex);
		}

		try
		{
			_outgoing?.Writer.TryComplete();
		}
		catch (Exception ex)
		{
			Error?.Invoke(ex);
		}

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
			try
			{
				wcts?.Dispose();
			}
			catch (Exception ex)
			{
				Error?.Invoke(ex);
			}
		}

		_outgoing = null;

		if (_stdin != null)
		{
			try
			{
				await _stdin.DisposeAsync().ConfigureAwait(false);
			}
			catch (Exception ex)
			{
				Error?.Invoke(ex);
			}

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

		try
		{
			_lines?.Writer.TryComplete();
		}
		catch (Exception ex)
		{
			Error?.Invoke(ex);
		}

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
						_options.Logger?.LogInfo(
							$"Killing UCI engine process during failed start cleanup (tree={_options.KillEntireProcessTree}).");
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

				try
				{
					// Disposing Process synchronously; no need to offload to thread pool.
					p.Dispose();
				}
				catch (Exception ex)
				{
					Error?.Invoke(ex);
				}
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

	private void ThrowIfDisposed()
	{
		if (Volatile.Read(ref _disposed) == 1) throw new ObjectDisposedException(nameof(ProcessUciTransport));
	}

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
}

internal sealed class ProcessUciTransportOptions
{
	public bool RedirectStandardError { get; init; } = true;

	public bool SendQuitOnDispose { get; init; } = true;

	public bool SendQuitOnStop { get; init; } = true;

	public bool SingleReader { get; init; } = true;

	public int ChannelCapacity { get; init; } = 1024;

	public int QuitGracePeriodMs { get; init; } = 500;

	// Preferred time-based grace period; when default (zero), QuitGracePeriodMs is used.
	public TimeSpan QuitGracePeriod { get; init; } = default;

	// When true (and supported by the runtime), attempts to kill the entire process tree on forced termination.
	public bool KillEntireProcessTree { get; init; } = false;

	public string NewLine { get; init; } = "\n";

	// Optional encodings; when null, platform defaults are used.
	public Encoding? StdoutEncoding { get; init; }
	public Encoding? StderrEncoding { get; init; }

	public Encoding? StdinEncoding { get; init; }

	// Optional logger for lifecycle and error events.
	public IUciTransportLogger? Logger { get; init; }
}
