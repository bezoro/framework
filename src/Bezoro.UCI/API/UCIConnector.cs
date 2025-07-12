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
		private readonly ICommandProcessor _commandProcessor;
		private readonly Process           _engineProcess;
		private readonly string            _enginePath;
		private readonly UCIEngine         _engine;
		private volatile bool              _isDisposed;
		private          bool              _started;

		public event Action<string> PositionSetSuccessfully;

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

			_engine           = new UCIEngine(_engineProcess);
			_commandProcessor = CommandProcessorFactory.Create(_engine);

			Logger.LogSuccess($"Engine Process Created", this, LogCategory.UCI);
		}

		public async Task SendCommandAsync(string command) =>
			await SendCommandAsync<object?>(command);

		public async Task SendCommandAsync<TResult>(string command)
		{
			ThrowIfDisposed();
			IEngineCommand<TResult?> sendCommand = _commandProcessor.CreateCommand().Send(command).Build<TResult?>();
			await _commandProcessor.ProcessCommandAsync(sendCommand);
		}

		public async Task SendNewGameCommandAsync()
		{
			await SendCommandAsync("ucinewgame");
		}

		public async Task SetDefaultPositionAsync()
		{
			await SetPositionAsync(UCIConstants.StandardFEN);
		}

		public async Task SetPositionAsync(string fen, IEnumerable<string>? moves = null)
		{
			var command = $"position fen {fen}";

			if (moves != null)
			{
				IList<string> moveList = moves as IList<string> ?? moves.ToList();
				// Build the UCI command: "position fen {fen} moves m1 m2 m3 ..."
				string movesArg = string.Join(" ", moveList);
				command += $" moves {movesArg}";
			}

			await SendCommandAsync(command);
			PositionSetSuccessfully?.Invoke(fen);
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
			await _commandProcessor.StartAsync();

			// Perform UCI handshake using a composite command
			IEngineCommand<string> startupCommand = _commandProcessor.
													CreateCommand().Send("uci").WaitFor("uciok").Send("isready").
													WaitFor("readyok").Build<string>();

			await _commandProcessor.ProcessCommandAsync(startupCommand);

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
			await _commandProcessor.StopAsync();
			Logger.LogSuccess($"Engine Process Stopped", this, LogCategory.UCI);
		}

		public async Task<(string best, string ponder)?> GetBestMoveAsync(int depth = 20)
		{
			ThrowIfDisposed();
			return await _commandProcessor.ProcessCommandWithResultAsync<(string, string)?>(
				new BestMoveCompositeCommand(depth));
		}

		public async Task<List<MoveClassification>?> GetAllLegalMovesWithDetailsAsync(
			CancellationToken cancellationToken = default) =>
			await _commandProcessor.ProcessCommandWithResultAsync<List<MoveClassification>>(
				new GetAllLegalMovesWithDetailsCommand(cancellationToken));

		public async Task<List<MoveClassification>?> GetLegalMovesForSquareWithDetailsAsync(
			string square, CancellationToken cancellationToken = default)
		{
			ThrowIfDisposed();
			return await _commandProcessor.ProcessCommandWithResultAsync<List<MoveClassification>>(
				new GetLegalMovesForSquareWithDetailsCommand(square, cancellationToken));
		}

		public async Task<List<string>?> GetLegalMovesAsync(CancellationToken ct = default)
		{
			var result =
				await _commandProcessor.ProcessCommandWithResultAsync<List<string>>(new GetLegalMovesCommand(ct));

			return result;
		}

		public async Task<string?> GetCurrentFENAsync()
		{
			ThrowIfDisposed();
			return await _commandProcessor.ProcessCommandWithResultAsync<string>(new GetCurrentFENCommand());
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

			// Dispose the command processor
			if (_commandProcessor is IAsyncDisposable disposableProcessor)
			{
				await disposableProcessor.DisposeAsync();
			}

			// Dispose the engine
			await _engine.DisposeAsync();

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
