using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Bezoro.Logging;

namespace Bezoro.UCI.Domain.Common.Helpers;

/// <summary>
///     Manages background loops for reading, writing, and monitoring process streams.
/// </summary>
internal sealed class BackgroundLoopManager(
	ProcessUciTransportOptions options,
	Action<Exception, string>  reportError,
	BackgroundLoopMetrics      metrics,
	Action<string>?            stderrReceived = null
)
{
	private CancellationTokenSource? _readLoopCts;
	private CancellationTokenSource? _writeLoopCts;

	private Task? _exitNotifyTask;
	private Task? _readLoopTask;
	private Task? _stderrLoopTask;
	private Task? _writeLoopTask;

	public long BackpressureEvents => metrics.BackpressureEvents;
	public long LinesRead          => metrics.LinesRead;
	public long LinesWritten       => metrics.LinesWritten;

	/// <summary>
	///     Checks if loops are healthy.
	/// </summary>
	public bool AreLoopsHealthy() =>
		(_readLoopTask is null ||
		 !_readLoopTask.IsCompleted && !_readLoopTask.IsFaulted && !_readLoopTask.IsCanceled) &&
		(_writeLoopTask is null ||
		 !_writeLoopTask.IsCompleted && !_writeLoopTask.IsFaulted && !_writeLoopTask.IsCanceled) &&
		(_stderrLoopTask is null ||
		 !_stderrLoopTask.IsCompleted && !_stderrLoopTask.IsFaulted && !_stderrLoopTask.IsCanceled) &&
		(_exitNotifyTask is null ||
		 !_exitNotifyTask.IsCompleted && !_exitNotifyTask.IsFaulted && !_exitNotifyTask.IsCanceled);

	/// <summary>
	///     Awaits exit notification task.
	/// </summary>
	public async Task AwaitExitNotificationAsync(TimeSpan timeout)
	{
		var task = _exitNotifyTask;
		_exitNotifyTask = null;
		if (task != null)
			await TryAwaitWithTimeout(task, "process exit notification", timeout).ConfigureAwait(false);
	}

	/// <summary>
	///     Awaits read loop task.
	/// </summary>
	public async Task AwaitReadLoopAsync()
	{
		var task = _readLoopTask;
		_readLoopTask = null;
		if (task != null)
			try
			{
				await task.ConfigureAwait(false);
			}
			catch (Exception ex)
			{
				reportError(ex, "Failed to join read loop.");
			}
	}

	/// <summary>
	///     Awaits stderr loop task.
	/// </summary>
	public async Task AwaitStderrLoopAsync()
	{
		var task = _stderrLoopTask;
		_stderrLoopTask = null;
		if (task != null)
			try
			{
				await task.ConfigureAwait(false);
			}
			catch (Exception ex)
			{
				reportError(ex, "Failed to join stderr loop.");
			}
	}

	/// <summary>
	///     Awaits write loop task.
	/// </summary>
	public async Task AwaitWriteLoopAsync()
	{
		var task = _writeLoopTask;
		_writeLoopTask = null;
		if (task != null)
			try
			{
				await task.ConfigureAwait(false);
			}
			catch (Exception ex)
			{
				reportError(ex, "Failed to join write loop.");
			}
	}

	/// <summary>
	///     Cancels read loop cancellation token.
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public void CancelReadLoop()
	{
		try
		{
			_readLoopCts?.Cancel();
		}
		catch
		{
			// ignored
		}
	}

	/// <summary>
	///     Cancels write loop cancellation token.
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public void CancelWriteLoop()
	{
		try
		{
			_writeLoopCts?.Cancel();
		}
		catch
		{
			// ignored
		}
	}

	/// <summary>
	///     Disposes cancellation token sources.
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public void DisposeCancellationSources()
	{
		try
		{
			_readLoopCts?.Dispose();
			_writeLoopCts?.Dispose();
		}
		catch
		{
			// ignored
		}
		finally
		{
			_readLoopCts  = null;
			_writeLoopCts = null;
		}
	}

	/// <summary>
	///     Initializes cancellation token sources for the loops.
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public void InitializeCancellationSources()
	{
		_readLoopCts  = new();
		_writeLoopCts = new();
	}

	/// <summary>
	///     Starts exit notification monitoring.
	/// </summary>
	public void StartExitNotification(
		Process               process,
		Action<int?, string?> onExited,
		Action<Exception>     reportExitError,
		TransportStateManager stateManager)
	{
		_exitNotifyTask = Task.Run(
			async () =>
			{
				try
				{
					await ProcessHelper.WaitForProcessExitAsync(process, CancellationToken.None)
									   .ConfigureAwait(false);

					stateManager.MarkProcessAlive(false);
					int exitCode = process.ExitCode;
					Logger.Log($"UCI engine process exited with code {exitCode}.", category: LogCategory.UCI);
					onExited(exitCode, null);
				}
				catch (Exception ex)
				{
					reportExitError(ex);
					onExited(null, ex.Message);
				}
			},
			CancellationToken.None);

		Observe(_exitNotifyTask);
	}

	/// <summary>
	///     Starts the read loop.
	/// </summary>
	public void StartReadLoop(StreamReader stdout, ChannelWriter<string> writer)
	{
		var readToken = _readLoopCts!.Token;

		_readLoopTask = Task.Run(
			async () =>
			{
				try
				{
					await RunReadLoopAsync(stdout, writer, readToken).ConfigureAwait(false);
					ChannelFactory.TryComplete(writer);
				}
				catch (OperationCanceledException)
				{
					ChannelFactory.TryComplete(writer);
				}
				catch (Exception ex)
				{
					reportError(ex, "Read loop faulted.");
					ChannelFactory.TryComplete(writer, ex);
				}
			},
			CancellationToken.None);

		Observe(_readLoopTask);
	}

	/// <summary>
	///     Starts the stderr loop if needed.
	/// </summary>
	public void StartStderrLoopIfNeeded(StreamReader? stderr)
	{
		if (stderr == null) return;

		_stderrLoopTask = Task.Run(
			async () =>
			{
				try
				{
					await RunStderrLoopAsync(stderr).ConfigureAwait(false);
				}
				catch (Exception ex)
				{
					reportError(ex, "Stderr loop faulted.");
				}
			},
			CancellationToken.None);

		Observe(_stderrLoopTask);
	}

	/// <summary>
	///     Starts the write loop.
	/// </summary>
	public void StartWriteLoop(ChannelReader<string> outgoingReader, StreamWriter stdin)
	{
		if (options.DisableWriteLoop) return;

		var writeToken = _writeLoopCts!.Token;

		_writeLoopTask = Task.Run(
			async () =>
			{
				try
				{
					await RunWriteLoopAsync(outgoingReader, stdin, writeToken).ConfigureAwait(false);
				}
				catch (OperationCanceledException)
				{
					// ignored
				}
				catch (Exception ex)
				{
					reportError(ex, "Write loop faulted.");
				}
			},
			CancellationToken.None);

		Observe(_writeLoopTask);
	}

	/// <summary>
	///     Attempts to write a line to the stream, returning false if the stream is disposed.
	/// </summary>
	private static bool TryWriteLine(StreamWriter writer, string line)
	{
		try
		{
			writer.WriteLine(line);
			return true;
		}
		catch (ObjectDisposedException)
		{
			return false;
		}
	}

	/// <summary>
	///     Attempts to read a line from the stream, returning null if the stream is closed or disposed.
	/// </summary>
	private static string? TryReadLine(StreamReader reader)
	{
		try
		{
			return reader.ReadLine();
		}
		catch (ObjectDisposedException)
		{
			return null;
		}
	}

	/// <summary>
	///     Attempts to flush the stream, swallowing any exceptions.
	/// </summary>
	private static void TryFlush(StreamWriter writer)
	{
		try
		{
			writer.Flush();
		}
		catch
		{
			// ignored
		}
	}

	/// <summary>
	///     Processes all available commands from the channel, writing them to stdin with batched flushing.
	///     Returns the number of writes since the last flush.
	/// </summary>
	private int ProcessAvailableCommands(
		ChannelReader<string> outgoingReader,
		StreamWriter          stdin,
		int                   flushBatchSize,
		CancellationToken     writeToken)
	{
		var writesSinceFlush = 0;

		while (outgoingReader.TryRead(out string? cmd))
		{
			if (writeToken.IsCancellationRequested || !TryWriteLine(stdin, cmd))
				return writesSinceFlush;

			metrics.IncrementLinesWritten();

			if (++writesSinceFlush < flushBatchSize) continue;

			TryFlush(stdin);
			writesSinceFlush = 0;
		}

		return writesSinceFlush;
	}

	/// <summary>
	///     Handles backpressure when the channel is full by waiting for space to become available.
	/// </summary>
	private async Task HandleBackpressureAsync(ChannelWriter<string> writer, string line, CancellationToken token)
	{
		metrics.IncrementBackpressureEvents();

		while (true)
		{
			if (token.IsCancellationRequested)
				throw new OperationCanceledException(token);

			bool canWrite = await writer.WaitToWriteAsync(token).ConfigureAwait(false);

			if (!canWrite) break;
			if (writer.TryWrite(line)) break;
		}
	}

	/// <summary>
	///     Invokes the stderr handler on a background thread, swallowing any exceptions.
	/// </summary>
	private async Task InvokeStderrHandlerAsync(string line)
	{
		try
		{
			var handler = stderrReceived;
			if (handler != null)
				// Invoke handler on thread pool to avoid blocking the stderr loop.
				// Exceptions are swallowed as per interface contract.
				await Task.Run(() =>
				{
					try
					{
						handler(line);
					}
					catch
					{
						// ignored
					}
				}).ConfigureAwait(false);
		}
		catch
		{
			// ignored
		}
	}

	/// <summary>
	///     Runs the read loop, reading lines from stdout and writing to the channel.
	/// </summary>
	private async Task RunReadLoopAsync(StreamReader stdout, ChannelWriter<string> writer, CancellationToken readToken)
	{
		while (true)
		{
			if (readToken.IsCancellationRequested) break;

			string? line = TryReadLine(stdout);
			if (line is null) break;

			if (line.Length == 0) continue;

			metrics.IncrementLinesRead();

			if (writer.TryWrite(line)) continue;

			await HandleBackpressureAsync(writer, line, readToken).ConfigureAwait(false);
		}
	}

	/// <summary>
	///     Runs the stderr loop, reading lines and invoking the handler.
	/// </summary>
	private async Task RunStderrLoopAsync(StreamReader stderr)
	{
		while (true)
		{
			string? line = TryReadLine(stderr);
			if (line is null) break;

			if (line.Length == 0) continue;

			await InvokeStderrHandlerAsync(line).ConfigureAwait(false);
		}
	}

	/// <summary>
	///     Runs the write loop, reading commands from the channel and writing to stdin.
	/// </summary>
	private async Task RunWriteLoopAsync(
		ChannelReader<string> outgoingReader,
		StreamWriter          stdin,
		CancellationToken     writeToken)
	{
		int flushBatchSize = options.FlushBatchSize > 0 ? options.FlushBatchSize : 8;

		while (true)
		{
			int writesSinceFlush = ProcessAvailableCommands(outgoingReader, stdin, flushBatchSize, writeToken);

			if (writesSinceFlush > 0)
				TryFlush(stdin);

			if (writeToken.IsCancellationRequested) break;

			bool hasMore = await outgoingReader.WaitToReadAsync(writeToken).ConfigureAwait(false);
			if (!hasMore) break;
		}
	}

	/// <summary>
	///     Tries to await a task with a timeout.
	/// </summary>
	private async Task TryAwaitWithTimeout(Task task, string description, TimeSpan timeout)
	{
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
			reportError(ex, $"Error while awaiting {description}.");
			return;
		}

		if (completed == task) await task.ConfigureAwait(false);
		else
			try
			{
				var timeoutException = new TimeoutException($"Timed out awaiting {description}.");
				Logger.Log(timeoutException, category: LogCategory.UCI);
				throw timeoutException;
			}
			catch { }
	}

	/// <summary>
	///     Observes a task for faults.
	/// </summary>
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
					reportError(ex, "Background task faulted.");
				}
				catch
				{
					// ignored
				}
			},
			CancellationToken.None,
			TaskContinuationOptions.ExecuteSynchronously | TaskContinuationOptions.OnlyOnFaulted,
			TaskScheduler.Default);
	}
}
