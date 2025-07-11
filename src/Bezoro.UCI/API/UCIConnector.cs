using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Bezoro.UCI.API.Commands;
using Bezoro.UCI.API.Types;
using Bezoro.UCI.Domain.Constants;

namespace Bezoro.UCI.API
{
	/// <summary>
	///     Represents a connection to a UCI-compliant chess engine.
	///     This class handles process management, command serialization, and asynchronous communication.
	///     It is designed to be thread-safe and robust using the Command pattern.
	/// </summary>
	public sealed class UCIConnector : IAsyncDisposable
	{
		private readonly ConcurrentQueue<(IEngineCommand Command, TaskCompletionSource<object> ResultSource)>
			_commandQueue = new();
		private readonly SemaphoreSlim _commandSignal = new(0);
		private readonly string        _enginePath;
		private readonly UCIEngine     _engine;
		private readonly Process       _engineProcess;
		private volatile bool          _isDisposed;
		private          bool          _started;
		private          Task?         _commandProcessorTask;

		public event EventHandler<string>? InfoReceived;
		public event Action<string>        PositionSetSuccessfully;

		/// <summary>
		///     Initializes a new instance of the <see cref="UCIConnector" /> class.
		/// </summary>
		/// <param name="enginePath">The file path to the UCI engine executable.</param>
		public UCIConnector(string enginePath)
		{
			Logger.LogInfo($"Creating Engine Process", this, LogCategory.UCI);
			if (string.IsNullOrWhiteSpace(enginePath))
			{
				throw new ArgumentException("Engine path must be provided.", nameof(enginePath));
			}

			_enginePath = enginePath;
			_engineProcess = new Process
			{
				StartInfo = new ProcessStartInfo
				{
					FileName               = _enginePath,
					RedirectStandardInput  = true,
					RedirectStandardOutput = true,
					UseShellExecute        = false,
					CreateNoWindow         = true
				},
				EnableRaisingEvents = true
			};

			_engine              =  new UCIEngine(_engineProcess);
			_engine.InfoReceived += (sender, info) => InfoReceived?.Invoke(this, info);

			Logger.LogSuccess($"Engine Process Created", this, LogCategory.UCI);
		}

		/// <summary>
		///     Executes a command by adding it to the command queue and waiting for its result
		/// </summary>
		/// <typeparam name="T">The expected result type</typeparam>
		/// <param name="command">The command to execute</param>
		/// <returns>The command result</returns>
		private async Task<T> ExecuteCommandAsync<T>(IEngineCommand command)
		{
			ThrowIfDisposed();

			var resultSource = new TaskCompletionSource<object>();
			_commandQueue.Enqueue((command, resultSource));
			_commandSignal.Release();

			object? result = await resultSource.Task.ConfigureAwait(false);
			return (T)result;
		}

		/// <summary>
		///     Processes commands from the queue one by one
		/// </summary>
		private async Task ProcessCommandsAsync()
		{
			while (!_isDisposed)
			{
				await _commandSignal.WaitAsync().ConfigureAwait(false);

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

		public async Task SendCommandAsync(string command)
		{
			ThrowIfDisposed();
			await ExecuteCommandAsync<object>(new SendTextCommand(command));
		}

		public async Task SendNewGameCommandAsync()
		{
			await SendCommandAsync("ucinewgame");
		}

		public async Task SetDefaultPositionAsync()
		{
			await SetPositionAsync(UCIConstants.StandardFEN);
		}

		public async Task SetPositionAsync(string fen)
		{
			await SendCommandAsync($"position fen {fen}");
			PositionSetSuccessfully?.Invoke(fen);
		}

		public async Task SetPositionWithMovesAsync(string fen, IEnumerable<string> moves)
		{
			IList<string> moveList = moves as IList<string> ?? moves.ToList();
			// Build the UCI command: "position fen {fen} moves m1 m2 m3 ..."
			string movesArg = string.Join(" ", moveList);
			var    command  = $"position fen {fen} moves {movesArg}";

			// Send the command directly instead of using SetPositionAsync
			await SendCommandAsync(command);

			// Get the actual current FEN after the moves are applied
			string currentFen = await GetCurrentFENAsync();
			PositionSetSuccessfully?.Invoke(currentFen);
		}

		public async Task StartEngineAsync()
		{
			Logger.LogInfo($"Starting Engine Process", this, LogCategory.UCI);
			if (_isDisposed)
			{
				throw new ObjectDisposedException(nameof(UCIConnector));
			}

			// Start the engine
			await _engine.StartAsync();

			// Start the command processor
			_commandProcessorTask = Task.Run(ProcessCommandsAsync);

			// Perform UCI handshake
			await ExecuteCommandAsync<object>(new SendTextCommand("uci"));
			await ExecuteCommandAsync<string>(new WaitForTokenCommand("uciok"));

			await ExecuteCommandAsync<object>(new SendTextCommand("isready"));
			await ExecuteCommandAsync<string>(new WaitForTokenCommand("readyok"));

			_started = true;
			Logger.LogSuccess($"Engine Process Started", this, LogCategory.UCI);
		}

		public async Task StopEngineAsync()
		{
			Logger.LogInfo($"Stopping Engine Process", this, LogCategory.UCI);
			if (_isDisposed)
			{
				throw new ObjectDisposedException(nameof(UCIConnector));
			}

			await SendCommandAsync("quit");
			Logger.LogSuccess($"Engine Process Stopped", this, LogCategory.UCI);
		}

		public async Task<(string best, string ponder)> GetBestMoveAsync(int depth = 20)
		{
			ThrowIfDisposed();
			return await ExecuteCommandAsync<(string, string)>(new GetBestMoveCommand(depth));
		}

		public async Task<List<MoveClassification>> GetAllLegalMovesWithDetailsAsync(
			CancellationToken cancellationToken = default) =>
			await ExecuteCommandAsync<List<MoveClassification>>(new GetAllLegalMovesWithDetailsCommand(cancellationToken));

		public async Task<List<MoveClassification>> GetLegalMovesForSquareWithDetailsAsync(
			string square, CancellationToken cancellationToken = default)
		{
			ThrowIfDisposed();
			return await ExecuteCommandAsync<List<MoveClassification>>(
				new GetLegalMovesForSquareWithDetailsCommand(square, cancellationToken));
		}

		public async Task<List<string>> GetLegalMovesAsync(CancellationToken ct = default)
		{
			var result = await ExecuteCommandAsync<List<string>>(new GetLegalMovesCommand(ct));
			return result;
		}

		public async Task<string> GetCurrentFENAsync()
		{
			ThrowIfDisposed();
			return await ExecuteCommandAsync<string>(new GetCurrentFENCommand());
		}

		/// <summary>
		///     Disposes the connector and stops the engine.
		/// </summary>
		public async ValueTask DisposeAsync()
		{
			Logger.LogInfo($"Disposing Engine Process", this, LogCategory.UCI);
			if (_isDisposed)
			{
				return;
			}

			_isDisposed = true;

			// Dispose the engine
			await _engine.DisposeAsync();

			// Dispose semaphores
			_commandSignal.Dispose();

			Logger.LogSuccess($"Engine Process Disposed", this, LogCategory.UCI);
		}

		private void ThrowIfDisposed()
		{
			if (_isDisposed)
			{
				throw new ObjectDisposedException(nameof(UCIConnector),
					"Cannot use a disposed UCIConnector. Make sure you haven’t called DisposeAsync()");
			}
		}
	}
}
