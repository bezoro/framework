namespace Bezoro.Chess.UCI.API.Types;

/// <summary>
///     Represents a turn transition after a move, undo, or position load.
/// </summary>
/// <param name="GameId">Stable game identifier for the current gameplay session.</param>
/// <param name="PreviousTurn">Side to move before the transition.</param>
/// <param name="CurrentTurn">Side to move after the transition.</param>
/// <param name="Position">The resulting position after the transition.</param>
/// <param name="TimestampUtc">Wall-clock timestamp when the transition was emitted.</param>
public readonly record struct TurnChangedEvent(
	Guid           GameId,
	char           PreviousTurn,
	char           CurrentTurn,
	Fen            Position,
	DateTimeOffset TimestampUtc
);
