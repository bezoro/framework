using System;
using System.Threading;
using System.Threading.Tasks;
using Bezoro.Chess.API.Types;

namespace Bezoro.Chess.API.Abstractions;

/// <summary>
///     Service interface for online multiplayer functionality.
///     Unity (or other consumers) implements this to handle networking.
/// </summary>
public interface IRemoteGameService
{
	/// <summary>
	///     Gets whether the service is connected to the server.
	/// </summary>
	bool IsConnected { get; }

	/// <summary>
	///     Accepts a draw offer from the opponent.
	/// </summary>
	/// <param name="matchId">The match ID.</param>
	/// <param name="ct">Cancellation token.</param>
	Task AcceptDrawAsync(string matchId, CancellationToken ct = default);

	/// <summary>
	///     Cancels an ongoing matchmaking search.
	/// </summary>
	/// <param name="ct">Cancellation token.</param>
	Task CancelMatchmakingAsync(CancellationToken ct = default);

	/// <summary>
	///     Connects to the game server.
	/// </summary>
	/// <param name="ct">Cancellation token.</param>
	Task ConnectAsync(CancellationToken ct = default);

	/// <summary>
	///     Declines a draw offer from the opponent.
	/// </summary>
	/// <param name="matchId">The match ID.</param>
	/// <param name="ct">Cancellation token.</param>
	Task DeclineDrawAsync(string matchId, CancellationToken ct = default);

	/// <summary>
	///     Disconnects from the game server.
	/// </summary>
	/// <param name="ct">Cancellation token.</param>
	Task DisconnectAsync(CancellationToken ct = default);

	/// <summary>
	///     Offers a draw to the opponent.
	/// </summary>
	/// <param name="matchId">The match ID.</param>
	/// <param name="ct">Cancellation token.</param>
	Task OfferDrawAsync(string matchId, CancellationToken ct = default);

	/// <summary>
	///     Resigns the current game.
	/// </summary>
	/// <param name="matchId">The match ID.</param>
	/// <param name="ct">Cancellation token.</param>
	Task ResignAsync(string matchId, CancellationToken ct = default);

	/// <summary>
	///     Sends a move to the opponent.
	/// </summary>
	/// <param name="matchId">The match ID.</param>
	/// <param name="move">The move in UCI notation.</param>
	/// <param name="ct">Cancellation token.</param>
	Task SendMoveAsync(string matchId, string move, CancellationToken ct = default);

	/// <summary>
	///     Finds a match for the player.
	/// </summary>
	/// <param name="player">The player's profile.</param>
	/// <param name="ct">Cancellation token.</param>
	/// <returns>The match details, or null if no match found/cancelled.</returns>
	Task<RemoteMatch?> FindMatchAsync(PlayerProfile player, CancellationToken ct = default);

	/// <summary>
	///     Raised when the connection to the server is lost.
	/// </summary>
	event Action? ConnectionLost;

	/// <summary>
	///     Raised when the opponent accepts a draw.
	/// </summary>
	event Action? DrawAccepted;

	/// <summary>
	///     Raised when the opponent declines a draw.
	/// </summary>
	event Action? DrawDeclined;

	/// <summary>
	///     Raised when the opponent offers a draw.
	/// </summary>
	event Action? DrawOffered;

	/// <summary>
	///     Raised when the opponent disconnects.
	/// </summary>
	event Action? OpponentDisconnected;

	/// <summary>
	///     Raised when the opponent resigns.
	/// </summary>
	event Action? OpponentResigned;

	/// <summary>
	///     Raised when an error occurs.
	/// </summary>
	event Action<Exception>? Error;

	/// <summary>
	///     Raised when the opponent makes a move.
	/// </summary>
	event Action<string>? OpponentMoved;
}

/// <summary>
///     Represents a matched online game.
/// </summary>
/// <param name="MatchId">Unique identifier for the match.</param>
/// <param name="Opponent">The opponent's profile.</param>
/// <param name="YourColor">The color assigned to the local player.</param>
/// <param name="StartingFen">Optional custom starting position.</param>
/// <param name="TimeControl">The time control for the game.</param>
public readonly record struct RemoteMatch(
	string        MatchId,
	PlayerProfile Opponent,
	PlayerColor   YourColor,
	string?       StartingFen,
	GameClock     TimeControl
);
