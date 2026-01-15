using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace Bezoro.UCI.Domain.Common.Helpers;

/// <summary>
///     Manages background loops for reading, writing, and monitoring process streams.
/// </summary>
internal sealed class BackgroundLoopManager
{
	private readonly Action<Exception, string>  _reportError;
	private readonly Action<string>?            _stderrReceived;
	private readonly BackgroundLoopMetrics      _metrics;
	private readonly ProcessUciTransportOptions _options;

	private CancellationTokenSource? _readLoopCts;
	private CancellationTokenSource? _writeLoopCts;

	private Task? _exitNotifyTask;
	private Task? _readLoopTask;
	private Task? _stderrLoopTask;
	private Task? _writeLoopTask;

	public BackgroundLoopManager(
		ProcessUciTransportOptions options,
		Action<Exception, string>  reportError,
		BackgroundLoopMetrics      metrics,
		Action<string>?            stderrReceived = null)
	{
		_options        = options;
		_reportError    = reportError;
		_stderrReceived = stderrReceived;
		_metrics        = metrics;
	}

	public long BackpressureEvents => _metrics.BackpressureEvents;
	public long LinesRead          => _metrics.LinesRead;
	public long LinesWritten       => _metrics.LinesWritten;

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
				_reportError(ex, "Failed to join read loop.");
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
				_reportError(ex, "Failed to join stderr loop.");
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
				_reportError(ex, "Failed to join write loop.");
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
		catch { }
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
		catch { }
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
		catch { }
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
		Action<Exception>     reportError,
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
					reportError(ex);
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
					while (true)
					{
						if (readToken.IsCancellationRequested) break;

						string? line;
						try
						{
							line = stdout.ReadLine();
						}
						catch (ObjectDisposedException)
						{
							break;
						}

						if (line is null) break;

						if (line.Length == 0) continue;

						_metrics.IncrementLinesRead();

						if (!writer.TryWrite(line))
						{
							_metrics.IncrementBackpressureEvents();
							while (true)
							{
								if (readToken.IsCancellationRequested)
									throw new OperationCanceledException(readToken);

								bool canWrite = await writer.WaitToWriteAsync(readToken).ConfigureAwait(false);

								if (!canWrite) break;
								if (writer.TryWrite(line)) break;
							}
						}
					}

					ChannelFactory.TryComplete(writer);
				}
				catch (OperationCanceledException)
				{
					ChannelFactory.TryComplete(writer);
				}
				catch (Exception ex)
				{
					_reportError(ex, "Read loop faulted.");
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
					while (true)
					{
						string? line;
						try
						{
							line = stderr.ReadLine();
						}
						catch (ObjectDisposedException)
						{
							break;
						}

						if (line is null) break;

						if (line.Length == 0) continue;

						try
						{
							var handler = _stderrReceived;
							if (handler != null)
								// Invoke handler on thread pool to avoid blocking the stderr loop.
								// Exceptions are swallowed as per interface contract.
								await Task.Run(() =>
								{
									try
									{
										handler(line);
									}
									catch { }
								}).ConfigureAwait(false);
						}
						catch { }
					}
				}
				catch (Exception ex)
				{
					_reportError(ex, "Stderr loop faulted.");
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
		if (_options.DisableWriteLoop) return;

		var writeToken = _writeLoopCts!.Token;

		_writeLoopTask = Task.Run(
			async () =>
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
								stdin.WriteLine(cmd);
								_metrics.IncrementLinesWritten();
								if (++writesSinceFlush >= flushBatchSize)
								{
									stdin.Flush();
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
								stdin.Flush();
							}
							catch { }

							writesSinceFlush = 0;
						}

						if (writeToken.IsCancellationRequested) break;

						bool hasMore = await outgoingReader.WaitToReadAsync(writeToken).ConfigureAwait(false);
						if (!hasMore) break;
					}

					if (writesSinceFlush > 0)
						try
						{
							stdin.Flush();
						}
						catch { }
				}
				catch (OperationCanceledException) { }
				catch (Exception ex)
				{
					_reportError(ex, "Write loop faulted.");
				}
			},
			CancellationToken.None);

		Observe(_writeLoopTask);
	}

	/// <summary>
	///     Tries to await a task with a timeout.
	/// </summary>
	private async Task TryAwaitWithTimeout(Task task, string description, TimeSpan timeout)
	{
		if (task is null) return;

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
			_reportError(ex, $"Error while awaiting {description}.");
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
					_reportError(ex, "Background task faulted.");
				}
				catch { }
			},
			CancellationToken.None,
			TaskContinuationOptions.ExecuteSynchronously | TaskContinuationOptions.OnlyOnFaulted,
			TaskScheduler.Default);
	}
}
