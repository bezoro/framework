using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Bezoro.UCI.API.Constants;
using Bezoro.UCI.API.Exceptions;
using Bezoro.UCI.Extensions;
using Bezoro.UCI.Helpers;
using Bezoro.UCI.Types;

namespace Bezoro.UCI.API
{
	/// <summary>
	///     Represents a connection to a UCI-compliant chess engine.
	///     This class handles process management, command serialization, and asynchronous communication.
	///     It is designed to be thread-safe and robust.
	/// </summary>
	public sealed class UCIConnector : IAsyncDisposable
	{
		private const string FenResponsePrefix = "Fen: ";

		private readonly Process _engineProcess;

		private volatile bool _isDisposed;

		private StreamReader? _processOutput;
		private StreamWriter? _processInput;

		/// <summary>
		///     Fires whenever the engine sends real-time analysis information.
		///     Subscribe to this event to get updates on the engine's search progress.
		/// </summary>
		public event EventHandler<EngineAnalysisEventArgs>? InfoReceived;

		/// <summary>
		///     Initializes a new instance of the <see cref="UCIConnector" /> class.
		/// </summary>
		/// <param name="enginePath">The file path to the UCI engine executable.</param>
		public UCIConnector(string enginePath)
		{
			_processInput  = null;
			_processOutput = null;
			InfoReceived   = null;

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

			Logger.LogSuccess("UCI Connector Created.");
		}

		/// <summary>
		///     Sets a UCI option on the engine.
		/// </summary>
		/// <param name="name">The name of the option to set.</param>
		/// <param name="value">The value to set for the option.</param>
		/// <param name="cancellationToken">A token to cancel the operation.</param>
		public async Task SetOptionAsync(string name, string value, CancellationToken cancellationToken = default)
		{
			await SendCommandAndWaitForReadyAsync($"{UCIConstants.SetOptionCommand} {name} value {value}",
				cancellationToken);
		}

		/// <summary>
		///     Sets the board position using a FEN string and, optionally, a sequence of moves.
		/// </summary>
		/// <param name="fen">The FEN string for the position. Use "startpos" for the starting position.</param>
		/// <param name="moves">An optional sequence of moves to apply to the position.</param>
		/// <param name="cancellationToken">A token to cancel the operation.</param>
		public async Task SetPositionAsync(
			string fen = UCIConstants.StartPosCommand, IEnumerable<string> moves = null,
			CancellationToken cancellationToken = default)
		{
			string command = UCIConstants.PositionCommand +
							 " "                          +
							 (fen.ToLower() == UCIConstants.StartPosCommand
								 ? UCIConstants.StartPosCommand
								 : $"fen {fen}");

			if (moves?.Any() == true)
			{
				command += " moves " + string.Join(" ", moves);
			}

			await SendCommandAndWaitForReadyAsync(command, cancellationToken);
			Logger.LogSuccess($"Position Set Successfully: {command}");
		}

		/// <summary>
		///     Starts the engine process and initializes UCI communication.
		/// </summary>
		/// <param name="cancellationToken">A token to cancel the operation.</param>
		public async Task StartEngineAsync(CancellationToken cancellationToken = default)
		{
			ThrowIfDisposed();

			_engineProcess.Start();
			_processInput  = _engineProcess.StandardInput;
			_processOutput = _engineProcess.StandardOutput;

			await SendCommandAndWaitForReadyAsync(UCIConstants.UCICommand,        cancellationToken);
			await SendCommandAndWaitForReadyAsync(UCIConstants.UCINewGameCommand, cancellationToken);
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
				await _processInput.WriteLineAsync(UCIConstants.QuitCommand);

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
		///     Stops the engine's current calculation and asks for the best move found so far.
		/// </summary>
		public async Task StopSearchAsync()
		{
			if (_isDisposed)
			{
				throw new ObjectDisposedException(nameof(UCIConnector));
			}

			await _processInput.WriteLineAsync(UCIConstants.StopCommand);
		}

		/// <summary>
		///     Tells the engine that the next search will be for a new game.
		///     This is used to clear hash tables and other game-specific data.
		///     Must be followed by a SetPositionAsync call to actually reset the board to a starting state.
		/// </summary>
		/// <param name="cancellationToken">A token to cancel the operation.</param>
		public async Task UCINewGameAsync(CancellationToken cancellationToken = default)
		{
			await SendCommandAndWaitForReadyAsync(UCIConstants.UCINewGameCommand, cancellationToken);
		}

		/// Waits for the engine to finish processing all previous commands and be ready to accept new ones.
		/// This method sends the "isready" command and waits for the "readyok" response.
		/// </summary>
		/// <param name="cancellationToken">A token to cancel the operation.</param>
		public async Task WaitForEngineToBeReadyAsync(CancellationToken cancellationToken = default)
		{
			await SendCommandAndWaitForReadyAsync(UCIConstants.IsReadyCommand, cancellationToken);
		}

		/// <summary>
		///     Checks if a single move is legal in the current position.
		/// </summary>
		/// <param name="move">The move to check, in UCI format (e.g., "e2e4").</param>
		/// <param name="cancellationToken">A token to cancel the operation.</param>
		/// <returns>True if the move is legal, otherwise false.</returns>
		public async Task<bool> IsMoveLegalAsync(string move, CancellationToken cancellationToken = default)
		{
			if (string.IsNullOrWhiteSpace(move))
			{
				throw new ArgumentException("Move cannot be null or whitespace.", nameof(move));
			}

			if (!UCIHelper.IsValidUciMove(move))
			{
				throw new ArgumentException($"Move '{move}' is not in valid UCI format.", nameof(move));
			}

			List<string> legalMoves = await GetLegalMovesAsync(cancellationToken);
			return legalMoves.Contains(move, StringComparer.OrdinalIgnoreCase);
		}

		/// <summary>
		///     Checks if a given square on the board is being attacked by any pieces.
		///     This is achieved by temporarily setting the engine to a state where it's the specified player's
		///     turn to move, and then checking their legal moves. The original position is restored afterward.
		/// </summary>
		/// <param name="square">The square to check, in algebraic notation (e.g., "e4").</param>
		/// <param name="playerColor">
		///     The color to check attacks from ('w' for white, 'b' for black). If null, checks opponent of
		///     current player.
		/// </param>
		/// <param name="cancellationToken">A token to cancel the operation.</param>
		/// <returns>True if the square is attacked by any piece of the specified color, otherwise false.</returns>
		/// <exception cref="ArgumentException">Thrown if the square is not in valid algebraic notation.</exception>
		/// <exception cref="ObjectDisposedException">Thrown if the connector has been disposed.</exception>
		/// <exception cref="UCIException">Thrown if communication with the engine fails.</exception>
		public async Task<bool> IsSquareAttackedAsync(
			string square, char? playerColor = null, CancellationToken cancellationToken = default)
		{
			if (_isDisposed)
			{
				throw new ObjectDisposedException(nameof(UCIConnector));
			}

			if (string.IsNullOrWhiteSpace(square) || !UCIHelper.IsValidAlgebraicNotation(square))
			{
				throw new ArgumentException($"Square '{square}' is not in valid algebraic notation (e.g., 'e4').",
					nameof(square));
			}

			// 1. Get the original position's FEN to understand the current state.
			string   originalFen = await GetCurrentFenAsync(cancellationToken);
			string[] fenParts    = originalFen.Split(' ');
			if (fenParts.Length < 2)
			{
				throw new UCIException("Failed to parse FEN string from engine.");
			}

			char activeColor = fenParts[1][0];

			// 2. Determine which player's attack to check. Default to the OPPOSITE of the current active player.
			char colorToCheck = playerColor ?? activeColor;

			List<string> moves;

			// 3. Get legal moves for the specified player.
			if (colorToCheck == activeColor)
			{
				// If we're checking the current player, we don't need to change the engine's state.
				// We pass `false` to GetLegalMovesAsync to avoid re-acquiring the semaphore.
				moves = await GetLegalMovesAsync(cancellationToken, false);
			}
			else
			{
				// If checking the other player, we temporarily flip the turn in the FEN string.
				// The en-passant square is cleared as it's not valid after a turn flip.
				var tempFen = $"{fenParts[0]} {colorToCheck} {fenParts[2]} - {fenParts[4]} {fenParts[5]}";

				// Set the engine to the temporary position.
				await _processInput.WriteLineAsync($"{UCIConstants.PositionCommand} fen {tempFen}");
				await WaitUntilReadyResponseAsync(cancellationToken);

				// Get all legal moves for that player.
				moves = await GetLegalMovesAsync(cancellationToken, false);

				// IMPORTANT: Restore the engine to its original state to ensure consistency.
				await _processInput.WriteLineAsync($"{UCIConstants.PositionCommand} fen {originalFen}");
				await WaitUntilReadyResponseAsync(cancellationToken);
			}

			// 4. Check if any available move targets the given square.
			// A UCI move is "from-to" (e.g., "e2e4"), so we check the "to" part of the string.
			bool isAttacked = moves.Any(move =>
				move.Length >= 4 &&
				move.Substring(2, 2).Equals(square, StringComparison.OrdinalIgnoreCase)
			);

			return isAttacked;
		}

		/// <summary>
		///     Retrieves a list of all legal moves for the current position and classifies each one
		///     by its type (e.g., capture, castling). This is ideal for rich UI feedback.
		///     Note: This feature relies on the "d" command to get the current FEN from the engine,
		///     which is supported by many engines like Stockfish but is not part of the core UCI standard.
		/// </summary>
		/// <param name="cancellationToken">A token to cancel the operation.</param>
		/// <returns>A list of <see cref="MoveClassification" /> objects, one for each legal move.</returns>
		public async Task<List<MoveClassification>> GetAllLegalMovesWithDetailsAsync(
			CancellationToken cancellationToken = default)
		{
			ThrowIfDisposed();

			string       currentFen = await GetCurrentFenAsync(cancellationToken);
			List<string> legalMoves = await GetLegalMovesAsync(cancellationToken, false);
			BoardState   boardState = BoardStateParser.ParseFen(currentFen);
			return legalMoves.Select(move => MoveClassifier.ClassifyMove(move, boardState)).ToList();
		}

		/// <summary>
		///     Retrieves and classifies all legal moves that start from a specific square.
		///     This is ideal for UI scenarios where a user clicks on a piece and you want to show
		///     all possible destinations for that piece, color-coded by move type.
		/// </summary>
		/// <param name="square">The starting square in algebraic notation (e.g., "e2").</param>
		/// <param name="cancellationToken">A token to cancel the operation.</param>
		/// <returns>A list of classified legal moves originating from the given square.</returns>
		public async Task<List<MoveClassification>> GetLegalMovesForSquareWithDetailsAsync(
			string square, CancellationToken cancellationToken = default)
		{
			if (string.IsNullOrWhiteSpace(square))
			{
				throw new ArgumentException("Square cannot be null or whitespace.", nameof(square));
			}

			if (!UCIHelper.IsValidAlgebraicNotation(square))
			{
				throw new ArgumentException($"Square '{square}' is not in valid algebraic notation (e.g. 'e2').",
					nameof(square));
			}

			List<MoveClassification> allMovesWithDetails = await GetAllLegalMovesWithDetailsAsync(cancellationToken);

			List<MoveClassification> movesForSquare = allMovesWithDetails.
													  Where(m => m.Move.StartsWith(square,
														  StringComparison.OrdinalIgnoreCase)).ToList();

			return movesForSquare;
		}

		/// <summary>
		///     Retrieves a list of all legal moves available in the current position.
		///     Uses the engine's 'perft' command at depth 1 to get move information.
		/// </summary>
		/// <param name="cancellationToken">A token to cancel the operation.</param>
		/// <param name="acquireSemaphore">
		///     Whether to acquire the command semaphore. Set to false when called from methods that already hold the semaphore.
		/// </param>
		/// <returns>A list of legal moves in UCI format (e.g., "e2e4", "e1g1" for castling).</returns>
		/// <remarks>
		///     This method uses the 'perft' (performance test) command at depth 1, which is supported by most UCI engines.
		///     It includes a 5-second timeout to prevent hanging if the engine becomes unresponsive.
		///     The moves are parsed from the engine's output using a regular expression that matches UCI move format.
		/// </remarks>
		public async Task<List<string>> GetLegalMovesAsync(
			CancellationToken cancellationToken = default, bool acquireSemaphore = true)
		{
			ThrowIfDisposed();
			ThrowIfInputStreamIsNull();

			var moves = new List<string>();

			await SendCommandAndWaitForReadyAsync(UCIConstants.GoPerftDepth1Command, cancellationToken);

			while (true)
			{
				string? line = await ReadProcessOutputAsync(cancellationToken);
				if (line == null || line.Contains("Nodes searched"))
				{
					break;
				}

				Match match = UCIConstants.MoveRegex.Match(line);

				if (match.Success)
				{
					moves.Add(match.Groups[1].Value);
				}
			}

			return moves;
		}

		public async Task<string?> GetBestMoveAsync(
			SearchParameters parameters, CancellationToken cancellationToken = default)
		{
			ThrowIfDisposed();
			ThrowIfInputStreamIsNull();

			string goCommand = SearchHelper.BuildGoCommand(parameters);
			await SendCommandAndWaitForReadyAsync(goCommand, cancellationToken);
			return await WaitForBestMoveResponseAsync(parameters, cancellationToken);
		}

		/// <summary>
		///     Asks the engine to find the best move for the current position using a fixed amount of time.
		/// </summary>
		/// <param name="thinkingTimeInMS">The maximum time the engine should think.</param>
		/// <param name="cancellationToken">A token to cancel the operation.</param>
		/// <returns>The best move found by the engine in UCI format (e.g., "e2e4").</returns>
		public async Task<string?> GetBestMoveAsync(
			int thinkingTimeInMS = 1000, CancellationToken cancellationToken = default)
		{
			var searchParameters = new SearchParameters { MoveTimeMs = thinkingTimeInMS };
			return await GetBestMoveAsync(searchParameters, cancellationToken);
		}

		public async Task<string> GetCurrentFenAsync(CancellationToken cancellationToken)
		{
			EnsureEngineIsReady();

			// The 'd' command doesn't have a clear "readyok" end signal, so we read until we find the FEN
			await _processInput.WriteLineAsync(UCIConstants.DisplayBoardCommand);

			while (true)
			{
				string? line = await ReadProcessOutputAsync(cancellationToken);
				if (line is null)
				{
					// End of stream reached before finding the FEN.
					break;
				}

				if (line.StartsWith(FenResponsePrefix, StringComparison.Ordinal))
				{
					return line.Substring(FenResponsePrefix.Length).Trim();
				}
			}

			throw new UCIException("Engine did not return a FEN string. The 'd' command may not be supported.");
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
			GC.SuppressFinalize(this);
		}

		internal static string BuildGoCommand(SearchParameters parameters) => SearchHelper.BuildGoCommand(parameters);

		private async Task SendCommandAndWaitForReadyAsync(string command, CancellationToken cancellationToken)
		{
			ThrowIfDisposed();
			ThrowIfInputStreamIsNull();

			await _processInput.WriteLineAsync(command);
			await WaitUntilReadyResponseAsync(cancellationToken);
			Logger.LogInfo($"<<UCI>>[{command}] Successful.");
		}

		private async Task WaitUntilReadyResponseAsync(CancellationToken cancellationToken)
		{
			ThrowIfInputStreamIsNull();
			await _processInput.WriteLineAsync(UCIConstants.IsReadyCommand);
			string? line = await ReadLineAsync(cancellationToken);
			ProcessGenericEngineOutput(line);
		}

		private async Task<List<string>> ReadAllProcessOutputAsync(CancellationToken cancellationToken)
		{
			ThrowIfProcessOutputStreamIsNull();
			var lines = new List<string>();

			// The `is { } line` pattern matching elegantly handles the case where 
			// ReadProcessOutputAsync returns null (i.e., the stream has ended),
			// preventing potential infinite loops or nulls in the list.
			while (await ReadProcessOutputAsync(cancellationToken) is { } line)
			{
				if (string.Equals(line, "readyok", StringComparison.OrdinalIgnoreCase))
				{
					break; // Exit when the "readyok" signal is received.
				}

				lines.Add(line);
			}

			return lines;
		}

		private async Task<string?> ReadLineAsync(CancellationToken cancellationToken)
		{
			string? line = await ReadProcessOutputAsync(cancellationToken, 5000);
			if (line == null)
			{
				throw new UCIException("Engine process exited unexpectedly while waiting for readyok.");
			}

			return line;
		}

		private async Task<string?> ReadProcessOutputAsync(
			CancellationToken cancellationToken, int timeoutMilliseconds = -1)
		{
			ThrowIfProcessOutputStreamIsNull();
			Task<string?> readTask = _processOutput.ReadLineAsync();
			Task completedTask = await Task.WhenAny(readTask, Task.Delay(timeoutMilliseconds, cancellationToken));
			if (completedTask != readTask)
			{
				throw new TimeoutException("The engine response timed out.");
			}

			Logger.LogInfo($"<<UCI>>{readTask.Result}");
			return await readTask;
		}

		private async Task<string?> WaitForBestMoveResponseAsync(
			SearchParameters parameters, CancellationToken cancellationToken)
		{
			using CancellationTokenSource? timeoutCts = SearchHelper.CreateTimeoutCtsForSearch(parameters);
			using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
				cancellationToken, timeoutCts?.Token ?? CancellationToken.None);

			try
			{
				while (true)
				{
					string? line = await ReadProcessOutputAsync(linkedCts.Token);
					if (line == null)
					{
						throw new UCIException("Engine process exited unexpectedly while waiting for bestmove.");
					}

					if (line.StartsWith("info", StringComparison.OrdinalIgnoreCase))
					{
						InfoReceived?.Invoke(this, InfoParser.Parse(line));
					}
					else if (line.StartsWith(UCIConstants.BestMoveResponsePrefix, StringComparison.OrdinalIgnoreCase))
					{
						return SearchHelper.ParseBestMoveFromResponse(line);
					}
				}
			}
			catch (OperationCanceledException)
			{
				// Check if it was our timeout that triggered, not an external cancellation
				if (timeoutCts?.Token.IsCancellationRequested == true && !cancellationToken.IsCancellationRequested)
				{
					throw new TimeoutException("Timed out waiting for engine to respond with best move.");
				}

				// Otherwise, rethrow as it's an external cancellation
				throw;
			}
		}

		private void EnsureEngineIsReady()
		{
			if (_isDisposed)
			{
				throw new ObjectDisposedException(nameof(UCIConnector));
			}

			if (_engineProcess.HasExited)
			{
				throw new UCIException("Engine process has exited.");
			}

			if (_processInput is null)
			{
				throw new UCIException("Engine process input stream is null.");
			}
		}

		/// <summary>
		///     Processes generic, unsolicited output from the engine, like "info" strings.
		/// </summary>
		/// <param name="line">The line of output from the engine.</param>
		private void ProcessGenericEngineOutput(string line)
		{
			// We are primarily interested in "info" lines here, as other commands have their own dedicated response handlers.
			if (line.StartsWith(UCIConstants.InfoCommand, StringComparison.OrdinalIgnoreCase))
			{
				EngineAnalysisEventArgs? analysisArgs = InfoParser.Parse(line);
				if (analysisArgs != null)
				{
					InfoReceived?.Invoke(this, analysisArgs);
				}
			}
		}

		private void ThrowIfDisposed()
		{
			if (_isDisposed)
			{
				throw new ObjectDisposedException(nameof(UCIConnector));
			}
		}

		private void ThrowIfInputStreamIsNull()
		{
			if (_processInput == null)
			{
				throw new UCIException("Engine process input stream is null. Ensure the engine is started.");
			}
		}

		private void ThrowIfProcessOutputStreamIsNull()
		{
			if (_processOutput == null)
			{
				throw new UCIException("Engine process output stream is null.");
			}
		}
	}
}
