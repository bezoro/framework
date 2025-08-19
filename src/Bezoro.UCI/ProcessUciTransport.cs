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
	private readonly IReadOnlyList<string>? _args;

	private readonly SemaphoreSlim _writeLock = new(1, 1);

	private readonly string  _path;
	private readonly string? _workingDirectory;

	private bool                     _disposed;
	private CancellationTokenSource? _readLoopCts;

	private Channel<string>? _lines;

	private Process?      _process;
	private StreamReader? _stdout;
	private StreamWriter? _stdin;
	private Task?         _readLoopTask;

	public event Action<int?, string?>? Exited;

	public ProcessUciTransport(string path, IEnumerable<string>? args = null, string? workingDirectory = null)
	{
		if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("Engine path must be provided.", nameof(path));

		_path             = path;
		_workingDirectory = workingDirectory;
		_args             = args is null ? null : new List<string>(args);
	}


	public bool IsStarted { get; private set; }

	public async IAsyncEnumerable<string> ReadLinesAsync([EnumeratorCancellation] CancellationToken ct = default)
	{
		ThrowIfDisposed();

		var reader = _lines?.Reader ?? throw new InvalidOperationException("Transport not started.");

		await foreach (var line in reader.ReadAllAsync(ct).ConfigureAwait(false))
		{
			yield return line;
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
			RedirectStandardError  = false, // keep minimal; stderr often noisy. Flip if you need it.
			CreateNoWindow         = true,
			WorkingDirectory = string.IsNullOrWhiteSpace(_workingDirectory)
								   ? Environment.CurrentDirectory
								   : _workingDirectory
		};

		if (_args is { Count: > 0 })
		{
			// Avoid quoting issues by using ArgumentList
			foreach (var a in _args)
			{
				if (a is null) continue; // defensively skip nulls

				startInfo.ArgumentList.Add(a);
			}
		}

		var process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };

		if (!process.Start())
			throw new InvalidOperationException("Failed to start UCI engine process.");

		_process = process;

		_stdin           = process.StandardInput;
		_stdin.NewLine   = "\n";
		_stdin.AutoFlush = true;

		_stdout = process.StandardOutput;

		// Prepare channel and background read loop
		_lines = Channel.CreateBounded<string>(
			new BoundedChannelOptions(1024)
			{
				SingleWriter = true,
				SingleReader = true,
				FullMode     = BoundedChannelFullMode.Wait
			});

		_readLoopCts = new();

		var localStdout = _stdout;
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
							line = await localStdout.ReadLineAsync().ConfigureAwait(false);
						}
						catch (ObjectDisposedException)
						{
							break; // stdout disposed -> end gracefully
						}

						if (line is null) break; // EOF

						if (line.Length == 0) continue; // skip empty lines

						// Fail fast on protocol error
						if (line.IndexOf("unknown command", StringComparison.OrdinalIgnoreCase) >= 0)
							throw new InvalidOperationException($"Engine reported unknown command: '{line}'");

						await writer.WriteAsync(line, _readLoopCts.Token).ConfigureAwait(false);
					}

					writer.TryComplete();
				}
				catch (OperationCanceledException)
				{
					// Cancellation during write/backpressure -> complete without error
					writer.TryComplete();
				}
				catch (Exception ex)
				{
					// Propagate errors to consumers
					writer.TryComplete(ex);
				}
			},
			CancellationToken.None);

		_ = Task.Run(
			async () =>
			{
				try
				{
					await WaitForProcessExitAsync(process, CancellationToken.None).ConfigureAwait(false);
					Exited?.Invoke(process.ExitCode, null);
				}
				catch (Exception ex)
				{
					Exited?.Invoke(null, ex.Message);
				}
			},
			CancellationToken.None);


		// Small readiness wait to ensure streams are available; optional but harmless.
		await Task.Yield();
		ct.ThrowIfCancellationRequested();
		IsStarted = true;
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

		// Also forbid empty/whitespace-only commands; engines often reply with "unknown command"
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
		GC.SuppressFinalize(this);

		var p = _process;
		_process = null;

		// Cancel and await background read loop before tearing down streams
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

		// Break any pending ReadLineAsync by disposing stdout BEFORE awaiting the read loop
		try
		{
			_stdout?.Dispose();
		}
		catch
		{
			/* ignore */
		}

		_stdout = null;

		var readLoop = _readLoopTask;
		_readLoopTask = null;
		try
		{
			if (readLoop != null) await readLoop.ConfigureAwait(false);
		}
		catch
		{
			/* ignore */
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

		try
		{
			if (_stdin != null) await _stdin.DisposeAsync();
		}
		catch
		{
			/* ignore */
		}

		_stdin = null;

		// Complete channel so any consumers finish
		try
		{
			_lines?.Writer.TryComplete();
		}
		catch
		{
			/* ignore */
		}

		_lines = null;

		try
		{
			_writeLock.Dispose();
		}
		catch
		{
			/* ignore */
		}

		if (p is null)
		{
			IsStarted = false;
			return;
		}

		try
		{
			if (!p.HasExited)
			{
				// Be conservative: give it a brief chance to exit if already terminating.
				if (!p.WaitForExit(50))
				{
					try
					{
						p.Kill();
					}
					catch
					{
						/* ignore */
					}
				}
			}
		}
		finally
		{
			try
			{
				await Task.Run(() => p.Dispose()).ConfigureAwait(false);
			}
			catch
			{
				/* ignore */
			}

			IsStarted = false;
		}
	}

	private static Task WaitForProcessExitAsync(Process process, CancellationToken ct)
	{
		if (process.HasExited) return Task.CompletedTask;

		var tcs = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);

		process.EnableRaisingEvents =  true;
		process.Exited              += Handler;

		CancellationTokenRegistration reg = default;
		if (ct.CanBeCanceled)
		{
			reg = ct.Register(() => tcs.TrySetCanceled(ct));
		}

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

		void Handler(object? _, EventArgs __)
		{
			tcs.TrySetResult(null);
		}
	}

	private void ThrowIfDisposed()
	{
		if (_disposed) throw new ObjectDisposedException(nameof(ProcessUciTransport));
	}
}
