namespace Bezoro.Chess.UCI.API.Types;

/// <summary>
///     Represents the start of a new gameplay session.
/// </summary>
/// <param name="GameId">Stable game identifier for the new gameplay session.</param>
/// <param name="StartingFen">The starting board position for the new game.</param>
/// <param name="TimestampUtc">Wall-clock timestamp when the event was emitted.</param>
public readonly record struct GameStartedEvent(
	Guid           GameId,
	Fen            StartingFen,
	DateTimeOffset TimestampUtc
);
