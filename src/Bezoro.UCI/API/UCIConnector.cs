using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Bezoro.UCI.API.Interfaces;
using Bezoro.UCI.API.Types;
using Bezoro.UCI.Domain;
using Bezoro.UCI.Domain.Constants;
using Bezoro.UCI.Domain.Exceptions;
using Bezoro.UCI.Domain.Helpers;
using EngineOutputParser = Bezoro.UCI.Domain.EngineOutputParser;

namespace Bezoro.UCI.API
{
	/// <summary>
	///     Represents a connection to a UCI-compliant chess engine.
	///     This class handles process management, command serialization, and asynchronous communication.
	///     It is designed to be thread-safe and robust.
	/// </summary>
	public sealed class UCIConnector : IUCIConnector
	{
		private readonly BoardStateAnalyzer   _boardAnalyzer;
		private readonly EngineCommandSender  _commandSender;
		private readonly EngineOutputParser   _outputParser;
		private readonly EngineProcessManager _processManager;
		private readonly SearchService        _searchService;
		private volatile bool                 _isDisposed;

		/// <summary>
		///     Fires whenever the engine sends real-time analysis information.
		///     Subscribe to this event to get updates on the engine's search progress.
		/// </summary>
		public event EventHandler<SearchResult>? InfoReceived;

		/// <summary>
		///     Initializes a new instance of the <see cref="UCIConnector" /> class.
		/// </summary>
		/// <param name="enginePath">The file path to the UCI engine executable.</param>
		/// <param name="engineProcessManager">Optional process manager for the engine. If null, a new one will be created.</param>
		/// <param name="engineCommandSender">Optional command sender for the engine. If null, a new one will be created.</param>
		/// <param name="engineOutputParser">Optional output parser for the engine. If null, a new one will be created.</param>
		/// <param name="boardStateAnalyzer">Optional board analyzer. If null, a new one will be created.</param>
		/// <param name="searchService">Optional search service. If null, a new one will be created.</param>
		internal UCIConnector(
			string enginePath,
			EngineProcessManager? engineProcessManager = null, EngineCommandSender? engineCommandSender = null,
			EngineOutputParser? engineOutputParser = null, BoardStateAnalyzer? boardStateAnalyzer = null,
			SearchService? searchService = null)
		{
			ThrowIfInvalidEnginePath(enginePath);
			_processManager = engineProcessManager ?? new EngineProcessManager(enginePath);
			_commandSender  = engineCommandSender  ?? new EngineCommandSender(_processManager);
			_outputParser   = engineOutputParser   ?? new EngineOutputParser(_processManager);
			_boardAnalyzer  = boardStateAnalyzer   ?? new BoardStateAnalyzer(_commandSender, _outputParser);
			_searchService  = searchService        ?? new SearchService(_commandSender, _outputParser);
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
			ThrowIfDisposed();
			await _commandSender.SendCommandAsync($"{UCIConstants.SetOptionCommand} {name} value {value}", true,
				cancellationToken);
		}

		/// <summary>
		///     Sets the board position using a FEN string and, optionally, a sequence of moves.
		/// </summary>
		/// <param name="fen">The FEN string for the position. Use "startpos" for the starting position.</param>
		/// <param name="moves">An optional sequence of moves to apply to the position.</param>
		/// <param name="ct">A token to cancel the operation.</param>
		public async Task SetPositionAsync(
			string? fen = null, IEnumerable<string>? moves = null, CancellationToken ct = default)
		{
			ThrowIfDisposed();
			ThrowIfInvalidFEN(fen);

			string positionPart =
				string.IsNullOrEmpty(fen) ||
				fen.Equals(UCIConstants.StartPosCommand, StringComparison.OrdinalIgnoreCase)
					? UCIConstants.StartPosCommand
					: $"fen {fen}";

			var command = $"{UCIConstants.PositionCommand} {positionPart}";

			if (moves?.Any() == true)
			{
				command += " moves " + string.Join(" ", moves);
			}

			await _commandSender.SendCommandAsync(command, false, ct);
			Logger.LogSuccess($"Position Set Successfully: {command}");
		}

		/// <summary>
		///     Simplified search for infinite analysis mode.
		/// </summary>
		/// <param name="onAnalysisUpdate">Callback invoked for each analysis update.</param>
		/// <param name="ct">Token to stop the analysis.</param>
		public async Task StartAnalysisAsync(
			Action<EngineOutput> onAnalysisUpdate, CancellationToken ct = default)
		{
			ThrowIfDisposed();
			await _searchService.StartAnalysisAsync(onAnalysisUpdate, ct);
		}

		/// <summary>
		///     Starts the engine process and initializes UCI communication.
		/// </summary>
		/// <param name="cancellationToken">A token to cancel the operation.</param>
		public async Task StartEngineAsync(CancellationToken cancellationToken = default)
		{
			ThrowIfDisposed();

			_processManager.StartEngine();

			await _commandSender.SendCommandAsync(UCIConstants.UCICommand,        true, cancellationToken);
			await _commandSender.SendCommandAsync(UCIConstants.UCINewGameCommand, true, cancellationToken);
		}

		/// <summary>
		///     Stops the engine gracefully.
		/// </summary>
		public async Task StopEngineAsync(CancellationToken cancellationToken = default)
		{
			if (_isDisposed)
			{
				return;
			}

			await _processManager.StopAsync(cancellationToken);
		}

		/// <summary>
		///     Stops the engine's current calculation and asks for the best move found so far.
		/// </summary>
		public async Task StopSearchAsync()
		{
			ThrowIfDisposed();
			await _searchService.StopAnalysisAsync();
		}

		/// <summary>
		///     Tells the engine that the next search will be for a new game.
		///     This is used to clear hash tables and other game-specific data.
		///     Must be followed by a SetPositionAsync call to actually reset the board to a starting state.
		/// </summary>
		/// <param name="cancellationToken">A token to cancel the operation.</param>
		public async Task UCINewGameAsync(CancellationToken cancellationToken = default)
		{
			ThrowIfDisposed();
			await _commandSender.SendCommandAsync(UCIConstants.UCINewGameCommand, true, cancellationToken);
		}

		public Task<bool> IsEngineReadyAsync(CancellationToken ct = default)
		{
			ThrowIfDisposed();
			return Task.FromResult(_processManager.IsReady());
		}

		/// <summary>
		///     Checks if a single move is legal in the current position.
		/// </summary>
		/// <param name="move">The move to check, in UCI format (e.g., "e2e4").</param>
		/// <param name="cancellationToken">A token to cancel the operation.</param>
		/// <returns>True if the move is legal, otherwise false.</returns>
		public async Task<bool> IsMoveLegalAsync(string move, CancellationToken cancellationToken = default)
		{
			ThrowIfDisposed();

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
		/// <param name="ct">A token to cancel the operation.</param>
		/// <returns>True if the square is attacked by any piece of the specified color, otherwise false.</returns>
		/// <exception cref="ArgumentException">Thrown if the square is not in valid algebraic notation.</exception>
		/// <exception cref="ObjectDisposedException">Thrown if the connector has been disposed.</exception>
		/// <exception cref="UCIException">Thrown if communication with the engine fails.</exception>
		public async Task<bool> IsSquareAttackedAsync(
			string square, char? playerColor = null, CancellationToken ct = default)
		{
			ThrowIfDisposed();

			if (string.IsNullOrWhiteSpace(square) || !UCIHelper.IsValidAlgebraicNotation(square))
			{
				throw new ArgumentException($"Square '{square}' is not in valid algebraic notation (e.g., 'e4').",
					nameof(square));
			}

			// 1. Get the original position's FEN to understand the current state.
			string   originalFen = await _boardAnalyzer.GetCurrentFENAsync(ct);
			string[] fenParts    = originalFen.Split(' ');
			if (fenParts.Length < 2)
			{
				throw new UCIException("Failed to parse FEN string from engine.");
			}

			char activeColor  = fenParts[1][0];
			char colorToCheck = playerColor ?? activeColor;

			// 2. Get legal moves for the specified player.
			List<string> moves =
				await _boardAnalyzer.GetMovesForPlayerAsync(colorToCheck, activeColor, fenParts, originalFen,
					ct);

			// 3. Check if any available move targets the given square.
			bool isAttacked = moves.Any(move =>
				move.Length >= 4 &&
				move.Substring(2, 2).Equals(square, StringComparison.OrdinalIgnoreCase)
			);

			return isAttacked;
		}

		public async Task<bool> IsStalemateAsync(CancellationToken cancellationToken = default)
		{
			ThrowIfDisposed();

			const int  FenActiveColorIndex     = 1;
			const int  MinimumFenPartsRequired = 2;
			const char WhitePlayer             = 'w';
			const char BlackPlayer             = 'b';

			// Get current position to determine whose turn it is
			string   currentFen = await _boardAnalyzer.GetCurrentFENAsync(cancellationToken);
			string[] fenParts   = currentFen.Split(' ');

			if (fenParts.Length < MinimumFenPartsRequired)
			{
				throw new UCIException("Invalid FEN string returned from engine");
			}

			char activeColor = fenParts[FenActiveColorIndex][0];

			// Get all legal moves for the current player
			List<string> legalMoves = await _boardAnalyzer.GetLegalMovesAsync(cancellationToken);

			// If there are legal moves, it's not stalemate
			if (legalMoves.Count > 0)
			{
				return false;
			}

			// No legal moves - check if king is in check (checkmate vs stalemate)
			string kingSquare    = _boardAnalyzer.FindKingSquare(fenParts[0], activeColor);
			char   opponentColor = activeColor == WhitePlayer ? BlackPlayer : WhitePlayer;

			bool isKingInCheck = await IsSquareAttackedAsync(kingSquare, opponentColor, cancellationToken);

			// Stalemate = no legal moves AND king is NOT in check
			return !isKingInCheck;
		}

		/// <summary>
		///     Waits for the engine to finish processing all previous commands and be ready to accept new ones.
		///     This method sends the "isready" command and waits for the "readyok" response.
		/// </summary>
		/// <param name="cancellationToken">A token to cancel the operation.</param>
		public async Task<bool> WaitForEngineToBeReadyAsync(CancellationToken cancellationToken = default)
		{
			ThrowIfDisposed();
			await _commandSender.SendCommandAsync(UCIConstants.IsReadyCommand, true, cancellationToken);
			return true;
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
			return await _boardAnalyzer.GetAllLegalMovesWithDetailsAsync(cancellationToken);
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
				await _boardAnalyzer.GetAllLegalMovesWithDetailsAsync(cancellationToken);

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
			return await _boardAnalyzer.GetLegalMovesAsync(ct);
		}

		/// <summary>
		///     Performs a unified search operation with comprehensive result tracking.
		///     This is the recommended method for all search operations.
		/// </summary>
		/// <param name="parameters">Search parameters defining time, depth, nodes, etc.</param>
		/// <param name="ct">Token to cancel the search operation.</param>
		/// <returns>A comprehensive SearchResult containing best move and all analysis data.</returns>
		public async Task<SearchResult> SearchAsync(SearchParameters parameters, CancellationToken ct = default)
		{
			ThrowIfDisposed();
			return await _searchService.SearchAsync(parameters, ct);
		}

		public async Task<string?> GetBestMoveAsync(SearchParameters? parameters = null, CancellationToken ct = default)
		{
			ThrowIfDisposed();
			parameters ??= new SearchParameters { Depth = 20 };
			var result = await _searchService.SearchAsync(parameters.Value, ct);
			return result.BestMove;
		}

		public async Task<string?> ReadProcessOutputAsync(CancellationToken ct, int timeoutMs = 5000)
		{
			// ThrowIfEngineHasInvalidState();
			Task<string?> readTask      = _processManager.ProcessOutput.ReadLineAsync();
			var           completedTask = await Task.WhenAny(readTask, Task.Delay(timeoutMs, ct));
			if (completedTask != readTask)
			{
				throw new TimeoutException("The engine response timed out.");
			}

			Logger.LogInfo($"<<UCI>>{readTask.Result}");
			return await readTask;
		}

		public async Task<string> GetCurrentFENAsync(CancellationToken ct = default)
		{
			ThrowIfDisposed();
			return await _boardAnalyzer.GetCurrentFENAsync(ct);
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

			await _processManager.DisposeAsync();
			GC.SuppressFinalize(this);
		}

		private static void ThrowIfInvalidEnginePath(string enginePath)
		{
			if (string.IsNullOrWhiteSpace(enginePath))
			{
				throw new ArgumentException("Engine path cannot be null or whitespace.", nameof(enginePath));
			}
		}

		private static void ThrowIfInvalidFEN(string FEN)
		{
			if (string.IsNullOrWhiteSpace(FEN))
			{
				return;
			}

			if (!BoardStateParser.IsValidFEN(FEN))
			{
				throw new UCIException($"The FEN string '{FEN}' is not valid.");
			}
		}

		private void ThrowIfDisposed()
		{
			if (_isDisposed)
			{
				throw new ObjectDisposedException(nameof(UCIConnector));
			}
		}
	}
}
