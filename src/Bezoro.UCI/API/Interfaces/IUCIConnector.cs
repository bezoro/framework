using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Bezoro.UCI.API.Types;

namespace Bezoro.UCI.API.Interfaces
{
	/// <summary>
	///     Defines the interface for a UCI chess engine connector.
	///     Handles communication with UCI-compliant chess engines.
	/// </summary>
	public interface IUCIConnector : IAsyncDisposable
	{
		/// <summary>
		///     Sets a UCI option on the engine.
		/// </summary>
		/// <param name="name">The name of the option to set.</param>
		/// <param name="value">The value to set for the option.</param>
		/// <param name="cancellationToken">A token to cancel the operation.</param>
		Task SetOptionAsync(string name, string value, CancellationToken cancellationToken = default);

		/// <summary>
		///     Sets the board position using a FEN string and, optionally, a sequence of moves.
		/// </summary>
		/// <param name="fen">The FEN string for the position. Use "startpos" for the starting position.</param>
		/// <param name="moves">An optional sequence of moves to apply to the position.</param>
		/// <param name="ct">A token to cancel the operation.</param>
		Task SetPositionAsync(string? fen = null, IEnumerable<string>? moves = null, CancellationToken ct = default);

		/// <summary>
		///     Starts the engine process and initializes UCI communication.
		/// </summary>
		/// <param name="cancellationToken">A token to cancel the operation.</param>
		Task StartEngineAsync(CancellationToken cancellationToken = default);

		/// <summary>
		///     Stops the engine gracefully.
		/// </summary>
		Task StopEngineAsync(CancellationToken cancellationToken = default);

		/// <summary>
		///     Stops the engine's current calculation and asks for the best move found so far.
		/// </summary>
		Task StopSearchAsync();

		/// <summary>
		///     Tells the engine that the next search will be for a new game.
		///     This is used to clear hash tables and other game-specific data.
		///     Must be followed by a SetPositionAsync call to actually reset the board to a starting state.
		/// </summary>
		/// <param name="cancellationToken">A token to cancel the operation.</param>
		Task UCINewGameAsync(CancellationToken cancellationToken = default);

		/// <summary>
		///     Checks if the engine is ready to accept commands.
		/// </summary>
		/// <param name="ct">Token to cancel the operation.</param>
		Task<bool> IsEngineReadyAsync(CancellationToken ct = default);

		/// <summary>
		///     Checks if a single move is legal in the current position.
		/// </summary>
		/// <param name="move">The move to check, in UCI format (e.g., "e2e4").</param>
		/// <param name="cancellationToken">A token to cancel the operation.</param>
		/// <returns>True if the move is legal, otherwise false.</returns>
		Task<bool> IsMoveLegalAsync(string move, CancellationToken cancellationToken = default);

		/// <summary>
		///     Checks if a given square on the board is being attacked by any pieces.
		/// </summary>
		/// <param name="square">The square to check, in algebraic notation (e.g., "e4").</param>
		/// <param name="attackerColor">The color to check attacks from ('w' for white, 'b' for black).</param>
		/// <param name="ct">A token to cancel the operation.</param>
		Task<bool> IsSquareAttackedAsync(string square, char attackerColor, CancellationToken ct = default);

		/// <summary>
		///     Checks if the current position is a stalemate.
		/// </summary>
		/// <param name="cancellationToken">A token to cancel the operation.</param>
		Task<bool> IsStalemateAsync(CancellationToken cancellationToken = default);

		/// <summary>
		///     Waits for the engine to finish processing all previous commands and be ready to accept new ones.
		///     This method sends the "isready" command and waits for the "readyok" response.
		/// </summary>
		/// <param name="cancellationToken">A token to cancel the operation.</param>
		Task<bool> WaitForEngineToBeReadyAsync(CancellationToken cancellationToken = default);

		/// <summary>
		///     Retrieves a list of all legal moves with detailed classification.
		/// </summary>
		/// <param name="cancellationToken">A token to cancel the operation.</param>
		Task<List<MoveClassification>> GetAllLegalMovesWithDetailsAsync(CancellationToken cancellationToken = default);

		/// <summary>
		///     Retrieves and classifies all legal moves that start from a specific square.
		/// </summary>
		/// <param name="square">The starting square in algebraic notation (e.g., "e2").</param>
		/// <param name="cancellationToken">A token to cancel the operation.</param>
		Task<List<MoveClassification>> GetLegalMovesForSquareWithDetailsAsync(
			string square, CancellationToken cancellationToken);

		/// <summary>
		///     Retrieves a list of all legal moves available in the current position.
		/// </summary>
		/// <param name="ct">A token to cancel the operation.</param>
		/// <param name="acquireSemaphore">Whether to acquire the command semaphore.</param>
		Task<List<string>> GetLegalMovesAsync(CancellationToken ct = default, bool acquireSemaphore = true);

		/// <summary>
		///     Performs a unified search operation with comprehensive result tracking.
		/// </summary>
		/// <param name="parameters">Search parameters defining time, depth, nodes, etc.</param>
		/// <param name="ct">Token to cancel the search operation.</param>
		Task<SearchResult> SearchAsync(SearchParameters parameters, CancellationToken ct = default);

		/// <summary>
		///     Gets the best move using the unified search API.
		/// </summary>
		Task<string?> GetBestMoveAsync(SearchParameters? parameters = null, CancellationToken ct = default);

		/// <summary>
		///     Gets the current position in FEN notation.
		/// </summary>
		/// <param name="ct">A token to cancel the operation.</param>
		Task<string> GetCurrentFENAsync(CancellationToken ct = default);
	}
}
