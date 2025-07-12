using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Bezoro.UCI.API.Commands;

namespace Bezoro.UCI.API
{
	/// <summary>
	///     Processes commands sequentially using a queue
	/// </summary>
	public sealed class CommandProcessor : ICommandProcessor, IAsyncDisposable
	{
		private readonly ConcurrentQueue<(IEngineCommand Command, TaskCompletionSource<object> ResultSource)>
			_commandQueue = new();
		private readonly SemaphoreSlim _commandSignal = new(0);
		private readonly UCIEngine     _engine;
		private volatile bool          _isDisposed;
		private          Task?         _processorTask;

		/// <summary>
		///     Initializes a new instance of the <see cref="CommandProcessor" /> class.
		/// </summary>
		/// <param name="engine">The UCI engine to process commands against</param>
		public CommandProcessor(UCIEngine engine)
		{
			_engine = engine;
		}

		/// <summary>
		///     Processes a command and returns its result
		/// </summary>
		/// <typeparam name="T">The expected result type</typeparam>
		/// <param name="command">The command to process</param>
		/// <returns>The command result</returns>
		public async Task<T> ProcessCommandAsync<T>(IEngineCommand command)
		{
			ThrowIfDisposed();

			var resultSource = new TaskCompletionSource<object>();
			_commandQueue.Enqueue((command, resultSource));
			_commandSignal.Release();

			object? result = await resultSource.Task.ConfigureAwait(false);
			return (T)result;
		}

		/// <summary>
		///     Starts the command processor
		/// </summary>
		public Task StartAsync()
		{
			ThrowIfDisposed();
			_processorTask = Task.Run(ProcessCommandsAsync);
			return Task.CompletedTask;
		}

		/// <summary>
		///     Stops the command processor
		/// </summary>
		public Task StopAsync()
		{
			ThrowIfDisposed();
			_isDisposed = true;
			_commandSignal.Release(); // Ensure the processor can exit its loop
			return Task.CompletedTask;
		}

		/// <summary>
		///     Disposes the command processor and its resources
		/// </summary>
		public async ValueTask DisposeAsync()
		{
			if (_isDisposed)
			{
				return;
			}

			_isDisposed = true;
			_commandSignal.Release(); // Ensure the processor can exit its loop

			// Wait for the processor task to complete
			if (_processorTask != null)
			{
				await _processorTask.ConfigureAwait(false);
			}

			_commandSignal.Dispose();
		}

		/// <summary>
		///     Processes commands from the queue one by one
		/// </summary>
		private async Task ProcessCommandsAsync()
		{
			while (!_isDisposed)
			{
				await _commandSignal.WaitAsync().ConfigureAwait(false);

				if (_isDisposed)
				{
					break;
				}

				if (_commandQueue.TryDequeue(
						out (IEngineCommand Command, TaskCompletionSource<object> ResultSource) commandItem))
				{
					(var command, TaskCompletionSource<object>? resultSource) = commandItem;

					try
					{
						object? result = await command.ExecuteAsync(_engine).ConfigureAwait(false);
						resultSource.SetResult(result ?? new object());
					}
					catch (Exception ex)
					{
						resultSource.SetException(ex);
					}
				}
			}
		}

		private void ThrowIfDisposed()
		{
			if (_isDisposed)
			{
				throw new ObjectDisposedException(nameof(CommandProcessor),
					"Cannot use a disposed CommandProcessor.");
			}
		}
	}
}
