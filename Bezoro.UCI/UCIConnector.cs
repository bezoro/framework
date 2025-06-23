using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Bezoro.UCI
{
	/// <summary>
	///     Represents a connection to a UCI-compliant chess engine.
	///     This class handles process management, command serialization, and asynchronous communication.
	///     It is designed to be thread-safe and robust.
	/// </summary>
	public sealed class UCIConnector : IAsyncDisposable
	{
		private readonly List<string>  _engineInfo = new();
		private readonly Process       _engineProcess;
		private readonly SemaphoreSlim _commandSemaphore = new(1, 1);
		private volatile bool          _isDisposed;
		private          StreamReader  _processOutput;
		private          StreamWriter  _processInput;

		/// <summary>
		///     Fires whenever the engine sends real-time analysis information.
		///     Subscribe to this event to get updates on the engine's search progress.
		/// </summary>
		public event EventHandler<EngineAnalysisEventArgs> InfoReceived;

		/// <summary>
		///     Initializes a new instance of the <see cref="UCIConnector" /> class.
		/// </summary>
		/// <param name="enginePath">The file path to the UCI engine executable.</param>
		public UCIConnector(string enginePath)
		{
			if (string.IsNullOrWhiteSpace(enginePath))
			{
				throw new ArgumentException("Engine path cannot be null or whitespace.", nameof(enginePath));
			}

			_engineProcess = new Process
			{
				StartInfo = new ProcessStartInfo
				{
					FileName               = enginePath,
					UseShellExecute        = false,
					RedirectStandardInput  = true,
					RedirectStandardOutput = true,
					CreateNoWindow         = true
				},
				EnableRaisingEvents = true
			};
		}

		/// <summary>
		///     Provides information about the connected engine (e.g., name, author).
		/// </summary>
		public IReadOnlyList<string> EngineInfo => _engineInfo.AsReadOnly();

		/// <summary>
		///     Sets a UCI option on the engine.
		/// </summary>
		/// <param name="name">The name of the option to set.</param>
		/// <param name="value">The value to set for the option.</param>
		/// <param name="cancellationToken">A token to cancel the operation.</param>
		public async Task SetOptionAsync(string name, string value, CancellationToken cancellationToken = default)
		{
			await SendCommandAndWaitForReadyAsync($"setoption name {name} value {value}", cancellationToken);
		}

		/// <summary>
		///     Sets the board position using a FEN string and, optionally, a sequence of moves.
		/// </summary>
		/// <param name="fen">The FEN string for the position. Use "startpos" for the starting position.</param>
		/// <param name="moves">An optional sequence of moves to apply to the position.</param>
		/// <param name="cancellationToken">A token to cancel the operation.</param>
		public async Task SetPositionAsync(
			string fen = "startpos", IEnumerable<string> moves = null, CancellationToken cancellationToken = default)
		{
			string command = "position " + (fen.ToLower() == "startpos" ? "startpos" : $"fen {fen}");
			if (moves?.Any() == true)
			{
				command += " moves " + string.Join(" ", moves);
			}

			await SendCommandAndWaitForReadyAsync(command, cancellationToken);
		}

		/// <summary>
		///     Starts the engine process and initializes UCI communication.
		/// </summary>
		/// <param name="cancellationToken">A token to cancel the operation.</param>
		public async Task StartEngineAsync(CancellationToken cancellationToken = default)
		{
			if (_isDisposed)
			{
				throw new ObjectDisposedException(nameof(UCIConnector));
			}

			_engineProcess.Start();
			_processInput  = _engineProcess.StandardInput;
			_processOutput = _engineProcess.StandardOutput;

			await _processInput.WriteLineAsync("uci");

			var uciOkReceived = false;

			while (true)
			{
				string line = await ReadLineAsync(cancellationToken, 10000); // 10-second timeout

				if (line.StartsWith("id "))
				{
					_engineInfo.Add(line);
				}
				else if (line == "uciok")
				{
					uciOkReceived = true;
					break;
				}
			}

			if (!uciOkReceived)
			{
				throw new UCIException(
					"Engine did not respond with 'uciok'. Ensure the path points to a valid UCI engine.");
			}

			await WaitUntilReadyAsync(cancellationToken);
		}

		/// <summary>
		///     Stops the engine gracefully.
		/// </summary>
		public async Task StopEngineAsync(CancellationToken cancellationToken = default)
		{
			if (_isDisposed || _engineProcess.HasExited)
			{
				return;
			}

			try
			{
				await _processInput.WriteLineAsync("quit");

				// Asynchronously wait for the process to exit with a timeout.
				using var cts       = new CancellationTokenSource(TimeSpan.FromSeconds(5));
				using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, cts.Token);
				await _engineProcess.WaitForExitAsync(linkedCts.Token);
			}
			catch (Exception)
			{
				// If graceful shutdown fails, kill the process.
				if (!_engineProcess.HasExited)
				{
					_engineProcess.Kill();
				}
			}
		}

		/// <summary>
		///     Checks if a single move is legal in the current position.
		/// </summary>
		/// <param name="move">The move to check, in UCI format (e.g., "e2e4").</param>
		/// <param name="cancellationToken">A token to cancel the operation.</param>
		/// <returns>True if the move is legal, otherwise false.</returns>
		public async Task<bool> IsMoveLegalAsync(string move, CancellationToken cancellationToken = default)
		{
			List<string> legalMoves = await GetLegalMovesAsync(cancellationToken);
			return legalMoves.Contains(move);
		}

		/// <summary>
		///     Retrieves a list of all legal moves in the current position.
		///     Note: This uses the 'go perft 1' command, which is a common but non-standard way to get legal moves.
		/// </summary>
		/// <param name="cancellationToken">A token to cancel the operation.</param>
		/// <returns>A list of legal moves in UCI format.</returns>
		public async Task<List<string>> GetLegalMovesAsync(CancellationToken cancellationToken = default)
		{
			if (_isDisposed)
			{
				throw new ObjectDisposedException(nameof(UCIConnector));
			}

			var moves     = new List<string>();
			var moveRegex = new Regex(@"^([a-h][1-8][a-h][1-8][qrbn]?)\s*:\s*\d+");

			await _commandSemaphore.WaitAsync(cancellationToken);
			try
			{
				await _processInput.WriteLineAsync("go perft 1");

				using var cts       = new CancellationTokenSource(TimeSpan.FromSeconds(5));
				using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, cts.Token);

				while (true)
				{
					string? line = await ReadLineAsync(linkedCts.Token);
					if (line == null || line.Contains("Nodes searched"))
					{
						break;
					}

					Match match = moveRegex.Match(line);
					if (match.Success)
					{
						moves.Add(match.Groups[1].Value);
					}
				}
			}
			catch (OperationCanceledException)
			{
				// Timeout is an acceptable way to exit if the engine is stuck.
			}
			finally
			{
				_commandSemaphore.Release();
			}

			return moves;
		}

		/// <summary>
		///     Asks the engine to find the best move for the current position using a fixed amount of time.
		/// </summary>
		/// <param name="thinkingTime">The maximum time the engine should think.</param>
		/// <param name="cancellationToken">A token to cancel the operation.</param>
		/// <returns>The best move found by the engine in UCI format (e.g., "e2e4").</returns>
		public async Task<string> GetBestMoveAsync(TimeSpan thinkingTime, CancellationToken cancellationToken = default)
		{
			var searchParameters = new SearchParameters { MoveTimeMs = (int)thinkingTime.TotalMilliseconds };
			return await GetBestMoveAsync(searchParameters, cancellationToken);
		}

		/// <summary>
		///     Asks the engine to find the best move for the current position using a flexible set of search parameters.
		/// </summary>
		/// <param name="parameters">The parameters that define the search (e.g., time controls, depth).</param>
		/// <param name="cancellationToken">A token to cancel the operation.</param>
		/// <returns>The best move found by the engine in UCI format (e.g., "e2e4").</returns>
		public async Task<string> GetBestMoveAsync(
			SearchParameters parameters, CancellationToken cancellationToken = default)
		{
			if (_isDisposed)
			{
				throw new ObjectDisposedException(nameof(UCIConnector));
			}

			await _commandSemaphore.WaitAsync(cancellationToken);
			try
			{
				string goCommand = BuildGoCommand(parameters);
				await _processInput.WriteLineAsync(goCommand);

				// Create an appropriate timeout for the read operation.
				// If the search is infinite, we don't time out here; we wait for a 'stop' or cancellation.
				CancellationTokenSource cts = null;
				if (!parameters.Infinite)
				{
					// Add a 5-second buffer to the longest possible thinking time.
					int timeout = (parameters.MoveTimeMs ?? 0) + (parameters.WhiteTimeMs ?? 0) + 5000;
					cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(timeout));
				}

				using var linkedCts =
					CancellationTokenSource.CreateLinkedTokenSource(cancellationToken,
						cts?.Token ?? CancellationToken.None);

				while (true)
				{
					string? line = await ReadLineAsync(linkedCts.Token);
					if (line == null)
					{
						throw new UCIException("Engine process exited unexpectedly while waiting for bestmove.");
					}

					if (line.StartsWith("info"))
					{
						InfoReceived?.Invoke(this, InfoParser.Parse(line));
					}
					else if (line.StartsWith("bestmove"))
					{
						string[]? parts = line.Split(' ');
						return parts.Length > 1 ? parts[1] : null;
					}
				}
			}
			finally
			{
				_commandSemaphore.Release();
			}
		}

		/// <summary>
		///     Disposes the connector and stops the engine.
		/// </summary>
		public async ValueTask DisposeAsync()
		{
			if (_isDisposed)
			{
				return;
			}

			_isDisposed = true;

			await StopEngineAsync();

			_processInput?.Dispose();
			_processOutput?.Dispose();
			_engineProcess?.Dispose();
			_commandSemaphore?.Dispose();
			GC.SuppressFinalize(this);
		}

		internal static string BuildGoCommand(SearchParameters parameters)
		{
			var commandBuilder = new StringBuilder("go");

			if (parameters.SearchMoves?.Any() == true)
			{
				commandBuilder.Append(" searchmoves ").Append(string.Join(" ", parameters.SearchMoves));
			}

			if (parameters.WhiteTimeMs.HasValue)
			{
				commandBuilder.Append(" wtime ").Append(parameters.WhiteTimeMs.Value);
			}

			if (parameters.BlackTimeMs.HasValue)
			{
				commandBuilder.Append(" btime ").Append(parameters.BlackTimeMs.Value);
			}

			if (parameters.WhiteIncrementMs.HasValue)
			{
				commandBuilder.Append(" winc ").Append(parameters.WhiteIncrementMs.Value);
			}

			if (parameters.BlackIncrementMs.HasValue)
			{
				commandBuilder.Append(" binc ").Append(parameters.BlackIncrementMs.Value);
			}

			if (parameters.Depth.HasValue)
			{
				commandBuilder.Append(" depth ").Append(parameters.Depth.Value);
			}

			if (parameters.Nodes.HasValue)
			{
				commandBuilder.Append(" nodes ").Append(parameters.Nodes.Value);
			}

			if (parameters.Mate.HasValue)
			{
				commandBuilder.Append(" mate ").Append(parameters.Mate.Value);
			}

			if (parameters.MoveTimeMs.HasValue)
			{
				commandBuilder.Append(" movetime ").Append(parameters.MoveTimeMs.Value);
			}

			if (parameters.Infinite)
			{
				commandBuilder.Append(" infinite");
			}

			return commandBuilder.ToString();
		}

		private async Task SendCommandAndWaitForReadyAsync(string command, CancellationToken cancellationToken)
		{
			if (_isDisposed)
			{
				throw new ObjectDisposedException(nameof(UCIConnector));
			}

			await _commandSemaphore.WaitAsync(cancellationToken);
			try
			{
				await _processInput.WriteLineAsync(command);
				await WaitUntilReadyAsync(cancellationToken);
			}
			finally
			{
				_commandSemaphore.Release();
			}
		}

		private async Task WaitUntilReadyAsync(CancellationToken cancellationToken)
		{
			await _processInput.WriteLineAsync("isready");

			while (true)
			{
				string? line = await ReadLineAsync(cancellationToken, 10000); // 10-second timeout
				if (line == null)
				{
					throw new UCIException("Engine process exited unexpectedly while waiting for readyok.");
				}

				if (line == "readyok")
				{
					break;
				}
			}
		}

		private async Task<string> ReadLineAsync(CancellationToken cancellationToken, int timeoutMilliseconds = -1)
		{
			Task<string>? readTask = _processOutput.ReadLineAsync();
			if (timeoutMilliseconds < 0)
			{
				// This custom implementation makes ReadLineAsync cancellable.
				var tcs = new TaskCompletionSource<string>();
				using ( cancellationToken.Register(() => tcs.TrySetCanceled()) )
				{
					// Awaiting Task.WhenAny returns the task that completed. We then await that task to get its result or exception.
					Task<string>? completedTask = await Task.WhenAny(readTask, tcs.Task);
					return await completedTask;
				}
			}

			using var timeoutCts = new CancellationTokenSource(timeoutMilliseconds);
			using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);
			var       tcsWithTimeout = new TaskCompletionSource<string>();
			using ( linkedCts.Token.Register(() => tcsWithTimeout.TrySetCanceled()) )
			{
				Task<string>? completedTask = await Task.WhenAny(readTask, tcsWithTimeout.Task);
				if (completedTask != readTask)
				{
					throw new TimeoutException("The engine response timed out.");
				}

				return await readTask;
			}
		}
	}

	/// <summary>
	///     A custom exception for errors related to the UCI protocol or engine communication.
	/// </summary>
	public class UCIException : Exception
	{
		public UCIException(string message) : base(message) { }
		public UCIException(string message, Exception innerException) : base(message, innerException) { }
	}

	internal static class ProcessExtensions
	{
		/// <summary>
		///     Asynchronously waits for the process to exit.
		/// </summary>
		/// <param name="process">The process to wait for.</param>
		/// <param name="cancellationToken">A token to cancel the wait operation.</param>
		/// <returns>A task that completes when the process exits.</returns>
		public static Task WaitForExitAsync(this Process process, CancellationToken cancellationToken = default)
		{
			if (process.HasExited)
			{
				return Task.CompletedTask;
			}

			var tcs = new TaskCompletionSource<object>();
			process.Exited += (sender, args) => tcs.TrySetResult(null);
			cancellationToken.Register(() => tcs.TrySetCanceled());

			// A final check in case the process exited after the initial check but before the event handler was attached.
			if (process.HasExited)
			{
				return Task.CompletedTask;
			}

			return tcs.Task;
		}
	}
}
