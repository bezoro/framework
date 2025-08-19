using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace Bezoro.UCI;

internal sealed class ProcessUciTransport : IUciTransport
{
	private readonly IReadOnlyList<string>?     _args;
	private readonly ProcessUciTransportOptions _options;

	private readonly SemaphoreSlim _writeLock = new(1, 1);

	private readonly string  _path;
	private readonly string? _workingDirectory;

	private bool                     _disposed;
	private CancellationTokenSource? _readLoopCts;

	private Channel<string>? _lines;
	private int              _readerActive;

	private Process?      _process;
	private StreamReader? _stderr;
	private StreamReader? _stdout;
	private StreamWriter? _stdin;
	private Task?         _readLoopTask;
	private Task?         _stderrLoopTask;
	private Task?         _exitNotifyTask;

	public event Action<Exception>? Error;

	public event Action<int?, string?>? Exited;

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

	public bool IsStarted { get; private set; }

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

		if (_process != null) throw new InvalidOperationException("Transport already started.");

		var startInfo = new ProcessStartInfo
		{
			FileName               = _path,
			UseShellExecute        = false,
			RedirectStandardInput  = true,
			RedirectStandardOutput = true,
			RedirectStandardError  = _options.RedirectStandardError,
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

			_stdin           = process.StandardInput;
			_stdin.NewLine   = _options.NewLine;
			_stdin.AutoFlush = true;

			_stdout = process.StandardOutput;
			_stderr = _options.RedirectStandardError ? process.StandardError : null;

			// Prepare channel and background read loop
			_lines = Channel.CreateBounded<string>(
				new BoundedChannelOptions(_options.ChannelCapacity)
				{
					SingleWriter = true,
					SingleReader = _options.SingleReader,
					FullMode     = BoundedChannelFullMode.Wait
				});

			_readLoopCts = CancellationTokenSource.CreateLinkedTokenSource(ct);

			var localStdout = _stdout!;
			var writer      = _lines.Writer;

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

							await writer.WriteAsync(line, _readLoopCts.Token).ConfigureAwait(false);
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
			}

			_exitNotifyTask = Task.Run(
				async () =>
				{
					try
					{
						await WaitForProcessExitAsync(process, CancellationToken.None).ConfigureAwait(false);
						int exitCode = process.ExitCode;
						Exited?.Invoke(exitCode, null);
					}
					catch (Exception ex)
					{
						Error?.Invoke(ex);
						Exited?.Invoke(null, ex.Message);
					}
				},
				CancellationToken.None);

			IsStarted = true;
		}
		catch
		{
			// If startup fails or is cancelled, tear down any partially started resources.
			try
			{
				await DisposeAsync().ConfigureAwait(false);
			}
			catch
			{
				/* ignore */
			}

			throw;
		}
	}

	public async Task WriteLineAsync(string line, CancellationToken ct = default)
	{
		ThrowIfDisposed();
		if (_stdin is null) throw new InvalidOperationException("Transport not started.");
		if (_process is { HasExited: true }) throw new InvalidOperationException("Engine process has exited.");
		if (line is null) throw new ArgumentNullException(nameof(line));

		// UCI is line-oriented; ensure no CR/LF in payload
		if (line.AsSpan().IndexOfAny('\r', '\n') >= 0)
			throw new ArgumentException("Line must not contain CR or LF characters.", nameof(line));

		// Also forbid empty/whitespace-only commands
		if (string.IsNullOrWhiteSpace(line))
			throw new ArgumentException("Command line must not be empty or whitespace.", nameof(line));

		await _writeLock.WaitAsync(ct).ConfigureAwait(false);
		try
		{
			ct.ThrowIfCancellationRequested();
			await _stdin.WriteLineAsync(line).ConfigureAwait(false);
			ct.ThrowIfCancellationRequested();
		}
		finally
		{
			_writeLock.Release();
		}
	}

	public async ValueTask DisposeAsync()
	{
		if (_disposed) return;

		_disposed = true;

		var p = _process;
		_process = null;

		// Cancel read/stderr loops up-front
		var cts = _readLoopCts;
		_readLoopCts = null;
		try
		{
			cts?.Cancel();
		}
		catch
		{
			/* ignore */
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

		// Politely request engine shutdown and signal EOF
		try
		{
			if (_stdin != null)
			{
				try
				{
					await _stdin.WriteLineAsync("quit").ConfigureAwait(false);
					await _stdin.FlushAsync().ConfigureAwait(false);
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
			catch
			{
				/* ignore */
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

		try
		{
			_stderr?.Dispose();
		}
		catch (Exception ex)
		{
			Error?.Invoke(ex);
		}

		_stderr = null;

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

		try
		{
			_writeLock.Dispose();
		}
		catch (Exception ex)
		{
			Error?.Invoke(ex);
		}

		// Give the process a brief chance to exit cleanly
		try
		{
			if (p is { HasExited: false })
			{
				using var timeoutCts = new CancellationTokenSource(_options.QuitGracePeriodMs);
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
			IsStarted = false;
			return;
		}

		try
		{
			if (!p.HasExited)
				// Final fallback if engine ignored quit/EOF
				try
				{
					p.Kill();
				}
				catch (Exception ex)
				{
					Error?.Invoke(ex);
				}
		}
		finally
		{
			// Ensure exit notification completes (reads ExitCode) before disposing the process.
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
				await Task.Run(() => p.Dispose()).ConfigureAwait(false);
			}
			catch (Exception ex)
			{
				Error?.Invoke(ex);
			}

			IsStarted = false;
		}
	}

	private static Task WaitForProcessExitAsync(Process process, CancellationToken ct)
	{
		var tcs = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);

		void Handler(object? _, EventArgs __)
		{
			tcs.TrySetResult(null);
		}

		process.EnableRaisingEvents =  true;
		process.Exited              += Handler;

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

	private void ThrowIfDisposed()
	{
		if (_disposed) throw new ObjectDisposedException(nameof(ProcessUciTransport));
	}
}

internal sealed class ProcessUciTransportOptions
{
	public bool RedirectStandardError { get; init; } = true;

	public bool SingleReader { get; init; } = true;

	public int ChannelCapacity { get; init; } = 1024;

	public int QuitGracePeriodMs { get; init; } = 500;

	public string NewLine { get; init; } = "\n";
}
