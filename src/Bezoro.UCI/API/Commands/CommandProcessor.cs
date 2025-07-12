using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Bezoro.UCI.API.Commands;

namespace Bezoro.UCI.API;

/// <summary>
///     Processes commands sequentially using a queue
/// </summary>
public sealed class CommandProcessor : ICommandProcessor, IAsyncDisposable
{
	private readonly CancellationTokenSource _shutdownCts = new();
	private readonly ConcurrentQueue<(IEngineCommand Command, TaskCompletionSource<object?> ResultSource)>
		_commandQueue = new();
	private readonly SemaphoreSlim _commandSignal = new(0);
	private readonly UCIEngine     _engine;
	// 0 for false, 1 for true. Used with Interlocked for thread-safe disposal.
	private int   _isDisposed;
	private Task? _processorTask;

	/// <summary>
	///     Initializes a new instance of the <see cref="CommandProcessor" /> class.
	/// </summary>
	/// <param name="engine">The UCI engine to process commands against</param>
	public CommandProcessor(UCIEngine engine)
	{
		_engine = engine;
	}

	/// <summary>
	///     Processes a command that does not return a value.
	/// </summary>
	/// <param name="command">The command to process.</param>
	public Task ProcessCommandAsync(IEngineCommand command) =>
		EnqueueCommandAsync(command);

	/// <summary>
	///     Creates a new command builder for constructing command sequences
	/// </summary>
	/// <returns>A command builder</returns>
	public CommandBuilder CreateCommand() => new();

	/// <summary>
	///     Starts the command processor
	/// </summary>
	public Task StartAsync()
	{
		ThrowIfDisposed();
		_processorTask = Task.Factory.StartNew(CommandProcessingLoopAsync, TaskCreationOptions.LongRunning).
							  Unwrap();

		return Task.CompletedTask;
	}

	/// <summary>
	///     Stops the command processor
	/// </summary>
	public async Task StopAsync()
	{
		ThrowIfDisposed();
		if (!_shutdownCts.IsCancellationRequested)
		{
			_shutdownCts.Cancel();
		}

		if (_processorTask != null)
		{
			await _processorTask.ConfigureAwait(false);
		}
	}

	/// <summary>
	///     Processes a command and returns its result
	/// </summary>
	/// <typeparam name="T">The expected result type</typeparam>
	/// <param name="command">The command to process</param>
	/// <returns>The command result</returns>
	public async Task<T> ProcessCommandWithResultAsync<T>(IEngineCommand command)
	{
		object? result = await EnqueueCommandAsync(command).ConfigureAwait(false);
		return (T)result!;
	}

	/// <summary>
	///     Disposes the command processor and its resources
	/// </summary>
	public async ValueTask DisposeAsync()
	{
		await StopAsync().ConfigureAwait(false);
		if (Interlocked.CompareExchange(ref _isDisposed, 1, 0) != 0)
		{
			return;
		}

		_commandSignal.Dispose();
		_shutdownCts.Dispose();
	}

	private async Task CommandProcessingLoopAsync()
	{
		try
		{
			while (!_shutdownCts.IsCancellationRequested)
			{
				await _commandSignal.WaitAsync(_shutdownCts.Token).ConfigureAwait(false);
				await TryProcessNextCommandAsync();
			}
		}
		catch (OperationCanceledException)
		{
			// Expected exception on shutdown.
		}
		finally
		{
			FailPendingCommands();
		}
	}

	private async Task TryProcessNextCommandAsync()
	{
		if (_commandQueue.TryDequeue(out (IEngineCommand Command, TaskCompletionSource<object?> ResultSource) item))
		{
			try
			{
				object? result = await item.Command.ExecuteAsync(_engine).ConfigureAwait(false);
				item.ResultSource.SetResult(result);
			}
			catch (Exception ex)
			{
				item.ResultSource.SetException(ex);
			}
		}
	}

	private Task<object?> EnqueueCommandAsync(IEngineCommand command)
	{
		ThrowIfDisposed();
		var resultSource = new TaskCompletionSource<object?>();
		_commandQueue.Enqueue((command, resultSource));
		_commandSignal.Release();
		return resultSource.Task;
	}

	private void FailPendingCommands()
	{
		var exception = new ObjectDisposedException(
			nameof(CommandProcessor), "The command processor is shutting down.");

		while (_commandQueue.TryDequeue(
				   out (IEngineCommand Command, TaskCompletionSource<object?> ResultSource) item))
		{
			item.ResultSource.TrySetException(exception);
		}
	}

	private void ThrowIfDisposed()
	{
		if (Volatile.Read(ref _isDisposed) != 0)
		{
			throw new ObjectDisposedException(nameof(CommandProcessor),
				"Cannot use a disposed CommandProcessor.");
		}
	}
}
