using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Bezoro.UCI.API.Constants;
using Bezoro.UCI.API.Exceptions;
using Bezoro.UCI.API.Types;
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
		private readonly List<string>    _engineInfo       = new();
		private readonly List<UCIOption> _supportedOptions = new();
		private readonly Process         _engineProcess;
		private readonly SemaphoreSlim   _commandSemaphore = new(1, 1);
		private volatile bool            _isDisposed;
		private          StreamReader?   _processOutput;
		private          StreamWriter?   _processInput;

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
		}

		/// <summary>
		///     Provides information about the connected engine (e.g., name, author).
		/// </summary>
		public IReadOnlyList<string> EngineInfo => _engineInfo.AsReadOnly();

		/// <summary>
		///     Provides a list of options supported by the engine.
		///     This list is populated after the engine has started.
		/// </summary>
		public IReadOnlyList<UCIOption> SupportedOptions => _supportedOptions.AsReadOnly();

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
		}

		/// <summary>
		///     Sets the engine's difficulty by adjusting its "Skill Level".
		///     Note: This is a common option for engines like Stockfish, but not all engines support it.
		///     The method will first check if the loaded engine supports this option.
		/// </summary>
		/// <param name="level">The skill level to set (e.g., 0-20 for Stockfish).</param>
		/// <param name="cancellationToken">A token to cancel the operation.</param>
		public async Task SetSkillLevelAsync(int level, CancellationToken cancellationToken = default)
		{
			if (SupportedOptions.Any(o => o.Name.Equals("Skill Level", StringComparison.OrdinalIgnoreCase)))
			{
				await SetOptionAsync("Skill Level", level.ToString(), cancellationToken);
			}
			else
			{
				// We can log a warning or simply do nothing if the option isn't supported.
				Console.WriteLine("Warning: 'Skill Level' option not supported by this engine.");
			}
		}

		/// <summary>
		///     Limits the engine's strength to a specific Elo rating.
		///     Note: This requires the engine to support the "UCI_LimitStrength" and "UCI_Elo" options.
		///     The method will first check if the loaded engine supports these options.
		/// </summary>
		/// <param name="elo">The Elo rating to target.</param>
		/// <param name="cancellationToken">A token to cancel the operation.</param>
		public async Task SetStrengthAsync(int elo, CancellationToken cancellationToken = default)
		{
			bool limitStrengthSupported =
				SupportedOptions.Any(o =>
					o.Name.Equals(UCIConstants.UCILimitstrength, StringComparison.OrdinalIgnoreCase));

			bool eloSupported =
				SupportedOptions.Any(o => o.Name.Equals(UCIConstants.UCIElo, StringComparison.OrdinalIgnoreCase));

			if (limitStrengthSupported && eloSupported)
			{
				await SetOptionAsync(UCIConstants.UCILimitstrength, "true",         cancellationToken);
				await SetOptionAsync(UCIConstants.UCIElo,           elo.ToString(), cancellationToken);
			}
			else
			{
				Console.WriteLine("Warning: Elo-based strength limiting not supported by this engine.");
			}
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

			await _processInput.WriteLineAsync(UCIConstants.UCICommand);

			var uciOkReceived = false;

			while (true)
			{
				string line = await ReadLineAsync(cancellationToken, 10000); // 10-second timeout

				if (string.IsNullOrEmpty(line))
				{
					continue;
				}

				if (line.StartsWith("id "))
				{
					_engineInfo.Add(line);
				}
				else if (line.StartsWith("option name"))
				{
					UCIOption? option = ParseOptionLine(line);
					if (option != null)
					{
						_supportedOptions.Add(option);
					}
				}
				else if (line == UCIConstants.UCIOkResponse)
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
			if (_isDisposed)
			{
				throw new ObjectDisposedException(nameof(UCIConnector));
			}

			// Acquire the semaphore once to ensure both commands are sent in a controlled sequence.
			await _commandSemaphore.WaitAsync(cancellationToken);
			try
			{
				// --- Part 1: Get the FEN string using the 'd' command ---
				await _processInput.WriteLineAsync(UCIConstants.DCommand);
				var currentFen = "";
				// Read lines until we find the FEN string. The 'd' command in Stockfish
				// doesn't have a clear "readyok" end signal, so we read until we find what we need
				// or time out. The timeout is critical to prevent a deadlock.
				using ( var fenCts = new CancellationTokenSource(TimeSpan.FromSeconds(5)) )
				{
					using var linkedFenCts =
						CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, fenCts.Token);

					while (true)
					{
						string? line = await ReadLineAsync(linkedFenCts.Token);
						if (line == null)
						{
							break;
						}

						if (line.StartsWith("Fen: "))
						{
							currentFen = line.Substring(5).Trim();
							break; // Found the FEN, we can stop reading for this command.
						}
					}
				}

				if (string.IsNullOrEmpty(currentFen))
				{
					throw new UCIException("Engine did not return a FEN string. The 'd' command may not be supported.");
				}

				// --- Part 2: Get the list of legal moves ---
				List<string>
					legalMoves =
						await GetLegalMovesAsync(cancellationToken, false); // `false` to not re-acquire the semaphore

				// --- Part 3: Classify the moves ---
				BoardState boardState      = ParseFen(currentFen);
				var        classifiedMoves = new List<MoveClassification>();
				foreach (string move in legalMoves)
				{
					classifiedMoves.Add(ClassifyMove(move, boardState));
				}

				return classifiedMoves;
			}
			finally
			{
				_commandSemaphore.Release();
			}
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
			// Get all classified legal moves for the current position.
			List<MoveClassification> allMovesWithDetails = await GetAllLegalMovesWithDetailsAsync(cancellationToken);

			// Filter the list to include only moves that start with the specified square.
			// The comparison is case-insensitive to be robust.
			List<MoveClassification> movesForSquare = allMovesWithDetails
													  .Where(m => m.Move.StartsWith(square,
														  StringComparison.OrdinalIgnoreCase))
													  .ToList();

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
			if (_isDisposed)
			{
				throw new ObjectDisposedException(nameof(UCIConnector));
			}

			var moves = new List<string>();

			if (acquireSemaphore)
			{
				await _commandSemaphore.WaitAsync(cancellationToken);
			}

			try
			{
				await _processInput.WriteLineAsync(UCIConstants.GOPerftOne);

				using var cts       = new CancellationTokenSource(TimeSpan.FromSeconds(5));
				using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, cts.Token);

				while (true)
				{
					string? line = await ReadLineAsync(linkedCts.Token);
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
			}
			catch (OperationCanceledException)
			{
				// Timeout is an acceptable way to exit if the engine is stuck.
			}
			finally
			{
				if (acquireSemaphore)
				{
					_commandSemaphore.Release();
				}
			}

			return moves;
		}

		/// <summary>
		///     Retrieves a list of all legal moves in the current position that start from a specific square.
		///     This is useful for UI implementations where you want to highlight valid destination squares when a user selects a
		///     piece.
		/// </summary>
		/// <param name="square">The starting square in algebraic notation (e.g., "e2").</param>
		/// <param name="cancellationToken">A token to cancel the operation.</param>
		/// <returns>A list of legal moves originating from the specified square.</returns>
		public async Task<List<string>> GetLegalMovesForSquareAsync(
			string square, CancellationToken cancellationToken = default)
		{
			if (!UCIHelper.IsValidAlgebraicNotation(square))
			{
				// Return an empty list if the square format is invalid.
				return new List<string>();
			}

			// Get all legal moves for the current position.
			List<string> allLegalMoves = await GetLegalMovesAsync(cancellationToken);

			// Filter the list to include only moves that start with the specified square.
			return allLegalMoves.Where(move => move.StartsWith(square, StringComparison.OrdinalIgnoreCase)).ToList();
		}

		public async Task<string?> GetBestMoveAsync(
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
				return await WaitForBestMoveResponseAsync(parameters, cancellationToken);
			}
			finally
			{
				_commandSemaphore.Release();
			}
		}

		/// <summary>
		///     Asks the engine to find the best move for the current position using a fixed amount of time.
		/// </summary>
		/// <param name="thinkingTimeMs">The maximum time the engine should think, in milliseconds.</param>
		/// <param name="cancellationToken">A token to cancel the operation.</param>
		/// <returns>The best move found by the engine in UCI format (e.g., "e2e4").</returns>
		public async Task<string> GetBestMoveAsync(int thinkingTimeMs, CancellationToken cancellationToken = default)
		{
			var searchParameters = new SearchParameters { MoveTimeMs = thinkingTimeMs };
			return await GetBestMoveAsync(searchParameters, cancellationToken);
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
		///     A helper method that sets the board to a specific position and then immediately finds the best move.
		///     This simplifies the common workflow of setting a position and then searching.
		/// </summary>
		/// <param name="fen">The FEN string for the position. Use "startpos" for the starting position.</param>
		/// <param name="moves">An optional sequence of moves to apply to the position.</param>
		/// <param name="thinkingTime">The maximum time the engine should think.</param>
		/// <param name="cancellationToken">A token to cancel the operation.</param>
		/// <returns>The best move found by the engine for the given position.</returns>
		public async Task<string> GetBestMoveForPositionAsync(
			string fen, IEnumerable<string> moves, TimeSpan thinkingTime, CancellationToken cancellationToken = default)
		{
			await SetPositionAsync(fen, moves, cancellationToken);
			return await GetBestMoveAsync(thinkingTime, cancellationToken);
		}

		/// <summary>
		///     Asks the engine to find the best move for the current position by searching to a specific depth.
		/// </summary>
		/// <param name="depth">The maximum depth for the engine to search.</param>
		/// <param name="cancellationToken">A token to cancel the operation.</param>
		/// <returns>The best move found by the engine in UCI format (e.g., "e2e4").</returns>
		public async Task<string> GetBestMoveWithDepthAsync(int depth, CancellationToken cancellationToken = default)
		{
			var searchParameters = new SearchParameters { Depth = depth };
			return await GetBestMoveAsync(searchParameters, cancellationToken);
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
			var commandBuilder = new StringBuilder(UCIConstants.GoCommand);

			if (parameters.SearchMoves?.Any() == true)
			{
				commandBuilder.Append($" {UCIConstants.SearchMoves} ").Append(string.Join(" ", parameters.SearchMoves));
			}

			if (parameters.WhiteTimeMs.HasValue)
			{
				commandBuilder.Append($" {UCIConstants.Wtime} ").Append(parameters.WhiteTimeMs.Value);
			}

			if (parameters.BlackTimeMs.HasValue)
			{
				commandBuilder.Append($" {UCIConstants.Btime} ").Append(parameters.BlackTimeMs.Value);
			}

			if (parameters.WhiteIncrementMs.HasValue)
			{
				commandBuilder.Append($" {UCIConstants.Winc} ").Append(parameters.WhiteIncrementMs.Value);
			}

			if (parameters.BlackIncrementMs.HasValue)
			{
				commandBuilder.Append($" {UCIConstants.Binc} ").Append(parameters.BlackIncrementMs.Value);
			}

			if (parameters.Depth.HasValue)
			{
				commandBuilder.Append($" {UCIConstants.Depth} ").Append(parameters.Depth.Value);
			}

			if (parameters.Nodes.HasValue)
			{
				commandBuilder.Append($" {UCIConstants.Nodes} ").Append(parameters.Nodes.Value);
			}

			if (parameters.Mate.HasValue)
			{
				commandBuilder.Append($" {UCIConstants.Mate} ").Append(parameters.Mate.Value);
			}

			if (parameters.MoveTimeMs.HasValue)
			{
				commandBuilder.Append($" {UCIConstants.Movetime} ").Append(parameters.MoveTimeMs.Value);
			}

			if (parameters.Infinite)
			{
				commandBuilder.Append($" {UCIConstants.Infinite}");
			}

			return commandBuilder.ToString();
		}

		private static bool IsCastling(ParsedMove move, char movingPiece) =>
			char.ToLower(movingPiece) == 'k' && Math.Abs(move.From[0] - move.To[0]) == 2;

		private static bool IsEnPassant(ParsedMove move, char movingPiece, bool isCapture, BoardState boardState) =>
			char.ToLower(movingPiece) == 'p'        &&
			move.From[0]              != move.To[0] &&
			!isCapture                              &&
			move.To.Equals(boardState.EnPassantTarget, StringComparison.OrdinalIgnoreCase);

		private static bool IsPromotion(char movingPiece, char promotionChar) =>
			char.ToLower(movingPiece) == 'p' && promotionChar != ' ';

		private static CancellationTokenSource? CreateTimeoutCtsForSearch(SearchParameters parameters)
		{
			if (parameters.Infinite)
			{
				return null;
			}

			// Add a 5-second buffer to the longest possible thinking time to allow for communication overhead.
			int timeoutMilliseconds = (parameters.MoveTimeMs ?? 0) + (parameters.WhiteTimeMs ?? 0) + 5000;
			return new CancellationTokenSource(TimeSpan.FromMilliseconds(timeoutMilliseconds));
		}

		/// <summary>
		///     Analyzes a single move based on the current board state to determine its type.
		/// </summary>
		private static MoveClassification ClassifyMove(string move, BoardState boardState)
		{
			if (string.IsNullOrEmpty(move) || move.Length < 4)
			{
				throw new InvalidOperationException($"Invalid move '{move}'.");
			}

			var parsedMove = new ParsedMove(
				move[..2],
				move[2..4],
				move.Length == 5 ? move[4] : ' '
			);

			if (!boardState.PiecePositions.TryGetValue(parsedMove.From, out char movingPiece))
			{
				// This indicates a severe inconsistency between the board state and the move.
				throw new InvalidOperationException(
					$"No piece found at the 'From' square '{parsedMove.From}' for move '{move}'.");
			}

			bool isCaptureOnToSquare = boardState.PiecePositions.ContainsKey(parsedMove.To);

			if (IsCastling(parsedMove, movingPiece))
			{
				return new MoveClassification(move) { IsCastling = true };
			}

			if (IsEnPassant(parsedMove, movingPiece, isCaptureOnToSquare, boardState))
			{
				return new MoveClassification(move) { IsCapture = true, IsEnPassant = true };
			}

			if (IsPromotion(movingPiece, parsedMove.PromotionChar))
			{
				return new MoveClassification(move) { IsPromotion = true, IsCapture = isCaptureOnToSquare };
			}

			if (isCaptureOnToSquare)
			{
				return new MoveClassification(move) { IsCapture = true };
			}

			return new MoveClassification(move);
		}

		private static string? ParseBestMoveFromResponse(string bestMoveLine)
		{
			// Expected format is "bestmove <move> [ponder <move>]"
			string[] parts = bestMoveLine.Split(' ', StringSplitOptions.RemoveEmptyEntries);
			return parts.Length > 1 ? parts[1] : null;
		}

		private static UCIOption ParseOptionLine(string line)
		{
			string[] parts        = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
			string   name         = null, type = null, defaultValue = null, min = null, max = null;
			var      vars         = new List<string>();
			var      isVarSection = false;

			for (var i = 0 ; i < parts.Length ; i++)
			{
				switch (parts[i])
				{
					case "name":    name         = parts[++i]; break;
					case "type":    type         = parts[++i]; break;
					case "default": defaultValue = parts[++i]; break;
					case "min":     min          = parts[++i]; break;
					case "max":     max          = parts[++i]; break;
					case "var":
						isVarSection = true;
						break;
					default:
						if (isVarSection)
						{
							vars.Add(parts[i]);
						}

						break;
				}
			}

			return new UCIOption(name, type, defaultValue, min, max, vars.ToArray());
		}

		/// <summary>
		///     Parses a FEN string to extract the piece positions and the en passant target square.
		/// </summary>
		private BoardState ParseFen(string fen)
		{
			string[]? parts           = fen.Split(' ');
			string    piecePlacement  = parts[0];
			string?   enPassantTarget = parts.Length > 3 && parts[3] != "-" ? parts[3] : null;

			var positions = new Dictionary<string, char>();
			var rank      = 8;
			var file      = 0; // 'a' is 0

			foreach (char c in piecePlacement)
			{
				if (c == '/')
				{
					rank--;
					file = 0;
				}
				else if (char.IsDigit(c))
				{
					file += (int)char.GetNumericValue(c);
				}
				else
				{
					var square = $"{(char)('a' + file)}{rank}";
					positions.Add(square, c);
					file++;
				}
			}

			return new BoardState(positions, enPassantTarget);
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
			await _processInput.WriteLineAsync(UCIConstants.IsReadyCommand);

			while (true)
			{
				string? line = await ReadLineAsync(cancellationToken, 10000); // 10-second timeout
				if (line == null)
				{
					throw new UCIException("Engine process exited unexpectedly while waiting for readyok.");
				}

				if (line == UCIConstants.ReadyOkResponse)
				{
					break;
				}
			}
		}

		private async Task<string?> WaitForBestMoveResponseAsync(
			SearchParameters parameters, CancellationToken cancellationToken)
		{
			using CancellationTokenSource? timeoutCts = CreateTimeoutCtsForSearch(parameters);
			using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
				cancellationToken, timeoutCts?.Token ?? CancellationToken.None);

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
				else if (line.StartsWith(UCIConstants.BestMoveResponse))
				{
					return ParseBestMoveFromResponse(line);
				}
			}
		}

		/// <summary>
		///     Sends the "d" command to the engine to get a text representation of the current board state,
		///     and extracts the FEN (Forsyth-Edwards Notation) string from the output.
		/// </summary>
		private async Task<string> GetCurrentFenAsync(CancellationToken cancellationToken = default)
		{
			await SendCommandAndWaitForReadyAsync(UCIConstants.DCommand, cancellationToken);
			var fen = string.Empty;
			while (true)
			{
				string line = await ReadLineAsync(cancellationToken);
				if (string.IsNullOrWhiteSpace(line))
				{
					continue;
				}

				if (line.StartsWith("Fen: ", StringComparison.OrdinalIgnoreCase))
				{
					fen = line.Substring(5);
				}
				// The "Checkers" line usually follows the FEN, marking the end of the position block.
				else if (line.StartsWith("Checkers:", StringComparison.OrdinalIgnoreCase))
				{
					break;
				}
			}

			return fen;
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

		/// <summary>
		///     A simple record to hold the essential parts of a board state parsed from a FEN string.
		/// </summary>
		private sealed record BoardState(Dictionary<string, char> PiecePositions, string? EnPassantTarget);

		private readonly struct ParsedMove
		{
			public ParsedMove(string from, string to, char promotionChar)
			{
				PromotionChar = promotionChar;
				From          = from;
				To            = to;
			}

			public char PromotionChar { get; }
			public string From { get; }
			public string To { get; }
		}
	}
}
