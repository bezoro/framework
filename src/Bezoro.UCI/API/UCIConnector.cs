using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
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
		private readonly Process       _engineProcess;
		private readonly SemaphoreSlim _commandSemaphore = new(1, 1);

		private volatile bool _isDisposed;

		private StreamReader? _processOutput;
		private StreamWriter? _processInput;

		/// <summary>
		///     Fires whenever the engine sends real-time analysis information.
		///     Subscribe to this event to get updates on the engine's search progress.
		/// </summary>
		public event EventHandler<SearchResult>? InfoReceived;

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
		///     Simplified search for infinite analysis mode.
		/// </summary>
		/// <param name="onAnalysisUpdate">Callback invoked for each analysis update.</param>
		/// <param name="ct">Token to stop the analysis.</param>
		public async Task AnalyzeAsync(
			Action<EngineAnalysisEventArgs> onAnalysisUpdate, CancellationToken ct = default)
		{
			var parameters = new SearchParameters { Infinite = true };
			await SendCommandAsync(SearchHelper.BuildGoCommand(parameters), true, ct);

			await foreach (var output in ReadSearchOutputAsync(parameters, ct))
			{
				if (output.Type == EngineOutputType.Info && output.InfoData != null)
				{
					onAnalysisUpdate(output.InfoData);
				}
			}
		}

		/// <summary>
		///     Sets a UCI option on the engine.
		/// </summary>
		/// <param name="uciConnector"></param>
		/// <param name="name">The name of the option to set.</param>
		/// <param name="value">The value to set for the option.</param>
		/// <param name="cancellationToken">A token to cancel the operation.</param>
		public async Task SetOptionAsync(string name, string value, CancellationToken cancellationToken = default)
		{
			await SendCommandAsync($"{UCIConstants.SetOptionCommand} {name} value {value}", true, cancellationToken);
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

			await SendCommandAsync(command, false, cancellationToken);
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

			await SendCommandAsync(UCIConstants.UCICommand,        true, cancellationToken);
			await SendCommandAsync(UCIConstants.UCINewGameCommand, true, cancellationToken);
		}

		public async Task StartSearchAsync(CancellationToken cancellationToken = default)
		{
			ThrowIfEngineHasInvalidState();
			await SendCommandAsync(UCIConstants.GoCommand, true, cancellationToken);
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
				ThrowIfInputStreamIsNull();
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
			ThrowIfEngineHasInvalidState();
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
			await SendCommandAsync(UCIConstants.UCINewGameCommand, true, cancellationToken);
		}

		/// Waits for the engine to finish processing all previous commands and be ready to accept new ones.
		/// This method sends the "isready" command and waits for the "readyok" response.
		/// </summary>
		/// <param name="cancellationToken">A token to cancel the operation.</param>
		public async Task WaitForEngineToBeReadyAsync(CancellationToken cancellationToken = default)
		{
			await SendCommandAsync(UCIConstants.IsReadyCommand, true, cancellationToken);
		}

		public Task<bool> IsEngineReadyAsync(CancellationToken ct = default)
		{
			Task<bool> task = Task.FromResult(_engineProcess           != null &&
											  _engineProcess.StartTime != null &&
											  _engineProcess.StartTime != DateTime.MinValue);

			return task;
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
			string   originalFen = await GetCurrentFENAsync(cancellationToken);
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

			string       currentFen = await GetCurrentFENAsync(cancellationToken);
			List<string> legalMoves = await GetLegalMovesAsync(cancellationToken, false);
			var          boardState = BoardStateParser.ParseFen(currentFen);
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
		/// <param name="ct">A token to cancel the operation.</param>
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
			CancellationToken ct = default, bool acquireSemaphore = true)
		{
			ThrowIfDisposed();
			ThrowIfInputStreamIsNull();

			var moves = new List<string>();

			await SendCommandAsync(UCIConstants.GoPerftDepth1Command, false, ct);

			while (true)
			{
				string? line = await ReadProcessOutputAsync(ct);
				if (line == null || line.Contains("Nodes searched"))
				{
					break;
				}

				var match = UCIConstants.MoveRegex.Match(line);

				if (match.Success)
				{
					moves.Add(match.Groups[1].Value);
				}
			}

			return moves;
		}

		// Add these methods to your UCIConnector class:

		/// <summary>
		///     Performs a unified search operation with comprehensive result tracking.
		///     This is the recommended method for all search operations.
		/// </summary>
		/// <param name="parameters">Search parameters defining time, depth, nodes, etc.</param>
		/// <param name="ct">Token to cancel the search operation.</param>
		/// <returns>A comprehensive SearchResult containing best move and all analysis data.</returns>
		public async Task<SearchResult> SearchAsync(SearchParameters parameters, CancellationToken ct = default)
		{
			ThrowIfEngineHasInvalidState();
			var result    = new SearchResult();
			var stopwatch = Stopwatch.StartNew();

			try
			{
				string goCommand = SearchHelper.BuildGoCommand(parameters);
				await SendCommandAsync(goCommand, false, ct);

				// Process all output until we get bestmove
				await foreach (var output in ReadSearchOutputAsync(parameters, ct))
				{
					switch (output.Type)
					{
						case EngineOutputType.Info:
							if (output.InfoData != null)
							{
								result.AnalysisInfo.Add(output.InfoData);

								if (output.InfoData.ScoreCp.HasValue)
								{
									result.FinalScore = output.InfoData.ScoreCp.Value;
								}

								if (output.InfoData.Depth.HasValue)
								{
									result.Depth = output.InfoData.Depth.Value;
								}
							}

							break;
						case EngineOutputType.BestMove:
							result.BestMove   = output.BestMove;
							result.PonderMove = output.PonderMove;
							stopwatch.Stop();
							result.SearchTimeMs = stopwatch.ElapsedMilliseconds;
							return result;
					}
				}
			}
			catch (OperationCanceledException)
			{
				// Search was cancelled - try to stop gracefully and get best move so far
				result.WasStoppedEarly = true;
				await StopSearchAsync();

				// Wait a bit for bestmove response after stop
				try
				{
					await foreach (var output in ReadSearchOutputAsync(parameters, ct).
									   WithCancellation(new CancellationTokenSource(1000).Token))
					{
						if (output.Type == EngineOutputType.BestMove)
						{
							result.BestMove   = output.BestMove;
							result.PonderMove = output.PonderMove;
							break;
						}
					}
				}
				catch
				{
					/* Ignore timeout after stop */
				}
			}

			stopwatch.Stop();
			result.SearchTimeMs = stopwatch.ElapsedMilliseconds;
			return result;
		}

		/// <summary>
		///     Gets the best move using the unified search API.
		///     Legacy method maintained for backward compatibility.
		/// </summary>
		public async Task<string?> GetBestMoveAsync(SearchParameters parameters, CancellationToken ct = default)
		{
			var result = await SearchAsync(parameters, ct);
			return result.BestMove;
		}

		/// <summary>
		///     Gets the best move with a fixed thinking time.
		///     Legacy method maintained for backward compatibility.
		/// </summary>
		public async Task<string?> GetBestMoveAsync(int thinkingTimeInMS = 1000, CancellationToken ct = default)
		{
			var parameters = new SearchParameters { MoveTimeMs = thinkingTimeInMS };
			var result     = await SearchAsync(parameters, ct);
			return result.BestMove;
		}

		public async Task<string?> ReadProcessOutputAsync(CancellationToken ct, int timeoutMs = 5000)
		{
			ThrowIfEngineHasInvalidState();
			Task<string?> readTask      = _processOutput.ReadLineAsync();
			var           completedTask = await Task.WhenAny(readTask, Task.Delay(timeoutMs, ct));
			if (completedTask != readTask)
			{
				throw new TimeoutException("The engine response timed out.");
			}

			Logger.LogInfo($"<<UCI>>{readTask.Result}");
			return await readTask;
		}

		public async Task<string> GetCurrentFENAsync(CancellationToken ct)
		{
			ThrowIfEngineHasInvalidState();
			// The 'd' command doesn't have a clear "readyok" end signal, so we read until we find the FEN
			await _processInput.WriteLineAsync(UCIConstants.DisplayBoardCommand);

			while (true)
			{
				string? line = await ReadProcessOutputAsync(ct);
				if (line is null)
				{
					// End of stream reached before finding the FEN.
					break;
				}

				if (line.StartsWith(UCIConstants.FenResponsePrefix, StringComparison.Ordinal))
				{
					return line.Substring(UCIConstants.FenResponsePrefix.Length).Trim();
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

			_processInput.Dispose();
			_processOutput.Dispose();
			_engineProcess.Dispose();
			GC.SuppressFinalize(this);
		}

		/// <summary>
		///     Processes generic, unsolicited output from the engine, like "info" strings.
		/// </summary>
		/// <param name="uciConnector"></param>
		/// <param name="line">The line of output from the engine.</param>
		public void ProcessGenericEngineOutput(string line)
		{
			ThrowIfEngineHasInvalidState();
			if (line.StartsWith(UCIConstants.InfoCommand, StringComparison.OrdinalIgnoreCase))
			{
				var analysisArgs = InfoParser.Parse(line);
				if (analysisArgs != null)
				{
					InfoReceived?.Invoke(this,
						new SearchResult { AnalysisInfo = { analysisArgs } });
				}
			}
		}

		/// <summary>
		///     Parses a single line of engine output into a structured format.
		/// </summary>
		private EngineOutput ParseEngineOutput(string line)
		{
			var output = new EngineOutput { RawLine = line };

			if (line.StartsWith("info", StringComparison.OrdinalIgnoreCase))
			{
				output.Type     = EngineOutputType.Info;
				output.InfoData = InfoParser.Parse(line);

				// Also fire the event for backward compatibility
				if (output.InfoData != null)
				{
					InfoReceived?.Invoke(this, new SearchResult { AnalysisInfo = { output.InfoData } });
				}
			}
			else if (line.StartsWith(UCIConstants.BestMoveResponsePrefix, StringComparison.OrdinalIgnoreCase))
			{
				output.Type = EngineOutputType.BestMove;

				// Parse bestmove and ponder from line like "bestmove e2e4 ponder e7e5"
				string[]? parts = line.Split(' ');
				if (parts.Length >= 2)
				{
					output.BestMove = parts[1];
				}

				// Check for ponder move
				for (var i = 2 ; i < parts.Length - 1 ; i++)
				{
					if (parts[i].Equals("ponder", StringComparison.OrdinalIgnoreCase))
					{
						output.PonderMove = parts[i + 1];
						break;
					}
				}
			}
			else
			{
				output.Type = EngineOutputType.Unknown;
			}

			return output;
		}

		/// <summary>
		///     Reads and parses engine output during a search operation.
		/// </summary>
		private async IAsyncEnumerable<EngineOutput> ReadSearchOutputAsync(
			SearchParameters parameters, [EnumeratorCancellation] CancellationToken cancellationToken = default)
		{
			// Create timeout CTS if needed based on search parameters
			using var timeoutCts = SearchHelper.CreateTimeoutCtsForSearch(parameters);
			using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
				cancellationToken, timeoutCts?.Token ?? CancellationToken.None);

			while (!linkedCts.Token.IsCancellationRequested)
			{
				string? line;
				try
				{
					line = await ReadProcessOutputAsync(linkedCts.Token);
				}
				catch (OperationCanceledException) when (timeoutCts?.Token.IsCancellationRequested == true)
				{
					throw new TimeoutException(
						$"Search timed out after {parameters.MoveTimeMs}ms");
				}

				if (line == null)
				{
					throw new UCIException("Engine process exited unexpectedly during search.");
				}

				var output = ParseEngineOutput(line);
				yield return output;

				if (output.Type == EngineOutputType.BestMove)
				{
					yield break;
				}
			}
		}

		private async Task SendCommandAsync(
			string command, bool waitForReady = false, CancellationToken cancellationToken = default)
		{
			ThrowIfEngineHasInvalidState();
			await _commandSemaphore.WaitAsync(cancellationToken);
			try
			{
				Logger.LogInfo($"<<UCI>>[{command}] Started.");
				await _processInput.WriteLineAsync(command);

				if (waitForReady)
				{
					await WaitUntilReadyResponseAsync(cancellationToken);
				}
			}
			finally
			{
				_commandSemaphore.Release();
			}

			Logger.LogInfo($"<<UCI>>[{command}] Sent.");
		}

		private async Task WaitUntilReadyResponseAsync(CancellationToken cancellationToken)
		{
			ThrowIfEngineHasInvalidState();
			await _processInput.WriteLineAsync(UCIConstants.IsReadyCommand);
			while (true)
			{
				string? line = await ReadProcessOutputAsync(cancellationToken);
				if (line == null)
				{
					throw new UCIException("Engine disconnected while waiting for readyok");
				}

				if (line.Equals("readyok", StringComparison.OrdinalIgnoreCase))
				{
					return;
				}

				ProcessGenericEngineOutput(line);
			}
		}

		private async Task<List<string>> ReadAllProcessOutputAsync(CancellationToken ct)
		{
			ThrowIfEngineHasInvalidState();
			var lines = new List<string>();

			// The `is { } line` pattern matching elegantly handles the case where 
			// ReadProcessOutputAsync returns null (i.e., the stream has ended),
			// preventing potential infinite loops or nulls in the list.
			while (await ReadProcessOutputAsync(ct) is { } line)
			{
				if (string.Equals(line, "readyok", StringComparison.OrdinalIgnoreCase))
				{
					break; // Exit when the "readyok" signal is received.
				}

				lines.Add(line);
			}

			return lines;
		}

		private void ThrowIfDisposed()
		{
			if (_isDisposed)
			{
				throw new ObjectDisposedException(nameof(UCIConnector));
			}
		}

		private void ThrowIfEngineHasExited()
		{
			if (_engineProcess.HasExited)
			{
				throw new UCIException("Engine process has exited.");
			}
		}

		private void ThrowIfEngineHasInvalidState()
		{
			ThrowIfInputStreamIsNull();
			ThrowIfProcessOutputStreamIsNull();
			ThrowIfEngineHasExited();
			ThrowIfDisposed();
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
