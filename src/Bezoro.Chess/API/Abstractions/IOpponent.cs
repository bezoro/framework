using System;
using System.Threading;
using System.Threading.Tasks;
using Bezoro.Chess.API.Types;

namespace Bezoro.Chess.API.Abstractions;

/// <summary>
///     Abstraction for any opponent in a chess game (engine, local human, remote human).
/// </summary>
public interface IOpponent : IAsyncDisposable
{
	/// <summary>
	///     Gets the type of opponent.
	/// </summary>
	OpponentType Type { get; }

	/// <summary>
	///     Gets the opponent's profile (name, ELO, stats).
	/// </summary>
	PlayerProfile Profile { get; }

	/// <summary>
	///     Gets whether the opponent is ready to play.
	/// </summary>
	bool IsReady { get; }

	/// <summary>
	///     Gets a move from the opponent.
	///     For engine: returns the best move calculated.
	///     For local human: returns null (moves come from SubmitMove).
	///     For remote human: waits for move from network.
	/// </summary>
	/// <param name="state">The current game state.</param>
	/// <param name="ct">Cancellation token.</param>
	/// <returns>The move in UCI notation, or null if move comes from external source.</returns>
	Task<string?> GetMoveAsync(GameState state, CancellationToken ct = default);

	/// <summary>
	///     Initializes the opponent (starts engine, connects to server, etc.).
	/// </summary>
	/// <param name="ct">Cancellation token.</param>
	Task InitializeAsync(CancellationToken ct = default);

	/// <summary>
	///     Notifies the opponent that a move was played (for pondering, etc.).
	/// </summary>
	/// <param name="move">The move that was played.</param>
	/// <param name="state">The new game state.</param>
	/// <param name="ct">Cancellation token.</param>
	Task NotifyMovePlayedAsync(string move, GameState state, CancellationToken ct = default);

	/// <summary>
	///     Raised when the opponent submits a move (for local/remote human opponents).
	/// </summary>
	event Action<string>? MoveSubmitted;

	/// <summary>
	///     Raised when the opponent resigns.
	/// </summary>
	event Action? Resigned;

	/// <summary>
	///     Raised when the opponent offers a draw.
	/// </summary>
	event Action? DrawOffered;

	/// <summary>
	///     Raised when the opponent disconnects (for remote opponents).
	/// </summary>
	event Action? Disconnected;

	/// <summary>
	///     Raised when an error occurs with the opponent.
	/// </summary>
	event Action<Exception>? Error;
}

