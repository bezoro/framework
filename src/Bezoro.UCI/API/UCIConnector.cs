using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Bezoro.Core.Common.Extensions;
using Bezoro.UCI.API.Types;
using Bezoro.UCI.Domain.Constants;
using Bezoro.UCI.Domain.Exceptions;
using Bezoro.UCI.Domain.Helpers;

namespace Bezoro.UCI.API
{
	/// <summary>
	///     Represents a connection to a UCI-compliant chess engine.
	///     This class handles process management, command serialization, and asynchronous communication.
	///     It is designed to be thread-safe and robust.
	/// </summary>
	public sealed class UCIConnector : IAsyncDisposable
	{
		private readonly ConcurrentQueue<string> _incomingLines = new();
		private readonly Process                 _engineProcess;
		private readonly SemaphoreSlim           _lineSignal = new(0);
		private readonly SemaphoreSlim           _streamLock = new(1, 1);
		private readonly string                  _enginePath;
		private volatile bool                    _isDisposed;
		private          bool                    _started;
		private          StreamReader            _output;
		private          StreamWriter            _input;
		private          Task?                   _readerTask;

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

			Logger.LogSuccess($"Engine Process Created", this, LogCategory.UCI);
		}

		public async Task SendCommandAsync(string command)
		{
			ThrowIfDisposed();
			Logger.LogInfo($"Sending Command: {command.Bold()}", this, LogCategory.UCI);
			if (_isDisposed)
			{
				throw new ObjectDisposedException(nameof(UCIConnector));
			}

			// Ensure only one write operation at a time
			await _streamLock.WaitAsync().ConfigureAwait(false);
			try
			{
				await _input.WriteLineAsync(command).ConfigureAwait(false);
				await _input.FlushAsync().ConfigureAwait(false);
				Logger.LogSuccess($"Command {command.Bold()} Sent", this, LogCategory.UCI);
			}
			finally
			{
				_streamLock.Release();
			}
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

			if (!_engineProcess.Start())
			{
				throw new InvalidOperationException("Failed to start the UCI engine process.");
			}

			_input  = _engineProcess.StandardInput;
			_output = _engineProcess.StandardOutput;

			// 1) Start the manual reader loop
			_readerTask = Task.Run(PumpOutputAsync);

			// 2) Perform UCI handshake via the queue
			await SendCommandAsync("uci");
			await WaitForToken("uciok");

			await SendCommandAsync("isready");
			await WaitForToken("readyok");

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
			await SendCommandAsync($"go depth {depth}");
			string line = await WaitForToken("bestmove");

			// Split on spaces, ignore any extra whitespace
			string[]? tokens = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
			// tokens[0] == "bestmove"
			string best = tokens.Length > 1 ? tokens[1] : string.Empty;
			// tokens[2] == "ponder", tokens[3] == the ponder move (if present)
			string ponder = tokens.Length > 3 ? tokens[3] : string.Empty;

			return (best, ponder);
		}

		public async Task<List<MoveClassification>> GetAllLegalMovesWithDetailsAsync(
			CancellationToken cancellationToken = default)
		{
			Logger.LogInfo("GettingAllLegalMoves...", this, LogCategory.UCI);
			string       currentFen = await GetCurrentFENAsync();
			List<string> legalMoves = await GetLegalMovesAsync(cancellationToken);
			var          boardState = BoardStateParser.ParseFen(currentFen);
			List<MoveClassification> classifiedMoves =
				legalMoves.Select(move => MoveClassifier.ClassifyMove(move, boardState)).ToList();

			Logger.LogInfo($"Legal Moves -> {classifiedMoves}", this, LogCategory.UCI);
			Logger.LogInfo("GettingAllLegalMoves...Done",       this, LogCategory.UCI);

			return classifiedMoves;
		}

		public async Task<List<MoveClassification>> GetLegalMovesForSquareWithDetailsAsync(
			string square, CancellationToken cancellationToken = default)
		{
			ThrowIfDisposed();

			if (string.IsNullOrWhiteSpace(square))
			{
				throw new ArgumentException("Square cannot be null or whitespace.", nameof(square));
			}

			if (!UCIHelper.IsValidAlgebraicNotation(square))
			{
				throw new ArgumentException($"Square '{square}' is not in valid algebraic notation (e.g. 'e2').",
					nameof(square));
			}

			List<MoveClassification> allMovesWithDetails =
				await GetAllLegalMovesWithDetailsAsync(cancellationToken);

			List<MoveClassification> movesForSquare = allMovesWithDetails.
													  Where(m => m.Move.StartsWith(square,
														  StringComparison.OrdinalIgnoreCase)).ToList();

			return movesForSquare;
		}

		public async Task<List<string>> GetLegalMovesAsync(CancellationToken ct = default)
		{
			var moves = new List<string>();

			// fire off the perft command
			await SendCommandAsync(UCIConstants.GoPerftDepth1Command);

			while (true)
			{
				// wait for the next line from the engine
				string line = await ReadNextOutputLineAsync(ct);

				// once we hit the summary line, stop
				if (line.Contains("Nodes searched", StringComparison.OrdinalIgnoreCase))
				{
					break;
				}

				// otherwise try to parse a move out of it
				var match = UCIConstants.MoveRegex.Match(line);
				if (match.Success)
				{
					moves.Add(match.Groups[1].Value);
				}
			}

			return moves;
		}

		public async Task<string> GetCurrentFENAsync()
		{
			Logger.LogInfo("Getting Current FEN...", this, LogCategory.UCI);
			ThrowIfDisposed();

			// Request the engine to dump the current position (Stockfish and many UCI engines respond to "d" with a "Fen: ..." line)
			await SendCommandAsync("d").ConfigureAwait(false);

			// Wait for the line that contains the FEN
			string? fenLine = await WaitForToken("Fen:").ConfigureAwait(false);

			const string prefix = "Fen: ";
			if (fenLine.StartsWith(prefix))
			{
				string fen = fenLine.Substring(prefix.Length).Trim();
				Logger.LogSuccess($"Current FEN: {fen}");
				return fen;
			}

			throw new UCIException($"FEN line not found in response: {fenLine}");
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

			// shut down your reader loop if needed…
			_input.Close();
			_output.Close();
			_engineProcess.Dispose();

			// now tear down your semaphores
			_streamLock.Dispose();
			_lineSignal.Dispose();
			Logger.LogSuccess($"Engine Process Disposed", this, LogCategory.UCI);
		}

		private async Task PumpOutputAsync()
		{
			while (!_isDisposed && !_engineProcess.HasExited)
			{
				string? line = await _output.ReadLineAsync().ConfigureAwait(false);
				if (line == null)
				{
					break; // stream closed
				}

				_incomingLines.Enqueue(line);
				_lineSignal.Release();
			}
		}

		private async Task WaitForStreamReadyAsync()
		{
			if (_isDisposed)
			{
				throw new ObjectDisposedException(nameof(UCIConnector));
			}

			Logger.LogInfo("Waiting for streams to be ready", this, LogCategory.UCI);

			// Poll until the engine process streams are initialized and usable
			while (true)
			{
				if (_isDisposed)
				{
					throw new ObjectDisposedException(nameof(UCIConnector));
				}

				if (_engineProcess.HasExited)
				{
					throw new InvalidOperationException("Engine process exited before streams were ready.");
				}

				if (_input != null && _output != null && _input.BaseStream.CanWrite && _output.BaseStream.CanRead)
				{
					Logger.LogSuccess("Streams are ready", this, LogCategory.UCI);
					return;
				}

				await Task.Delay(50).ConfigureAwait(false);
			}
		}

		private async Task<bool> WaitForEngineReadyAsync()
		{
			if (_isDisposed)
			{
				throw new ObjectDisposedException(nameof(UCIConnector));
			}

			// Ask engine to report readiness
			Logger.LogInfo("Sending isready to engine", this, LogCategory.UCI);
			await SendCommandAsync("isready").ConfigureAwait(false);

			// Read lines until we get "readyok"
			while (true)
			{
				await _streamLock.WaitAsync().ConfigureAwait(false);
				string? line;
				try
				{
					line = await _output.ReadLineAsync().ConfigureAwait(false);
					if (line == null)
					{
						// Stream closed or engine exited before readiness
						return false;
					}

					Logger.LogInfo($"Engine response: {line}", this, LogCategory.UCI);
				}
				finally
				{
					_streamLock.Release();
				}

				if (line.Trim().Equals("readyok", StringComparison.OrdinalIgnoreCase))
				{
					Logger.LogSuccess("Engine is ready", this, LogCategory.UCI);
					return true;
				}
			}
		}

		// you’ll need a helper that pulls one line out of your _incomingLines queue
		private async Task<string> ReadNextOutputLineAsync(CancellationToken ct)
		{
			await _lineSignal.WaitAsync(ct).ConfigureAwait(false);
			if (_incomingLines.TryDequeue(out string? line))
			{
				return line;
			}

			// in theory we never get here, but just in case:
			return string.Empty;
		}

		private async Task<string> WaitForToken(string token)
		{
			Logger.LogInfo($"Waiting for token: {token.Bold()}", this, LogCategory.UCI);
			while (true)
			{
				await _lineSignal.WaitAsync().ConfigureAwait(false);
				if (_incomingLines.TryDequeue(out string? line))
				{
					if (line.StartsWith("info "))
					{
						Logger.LogInfo($"{line}", this, LogCategory.UCI);
						string info = line;
						InfoReceived?.Invoke(this, info);
						continue;
					}

					if (line.Contains(token))
					{
						Logger.LogSuccess($"Received {token.Bold()} -> {line}", this, LogCategory.UCI);
						return line;
					}
				}
			}
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
