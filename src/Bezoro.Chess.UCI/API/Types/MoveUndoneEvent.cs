using System.Collections.Immutable;

namespace Bezoro.Chess.UCI.API.Types;

/// <summary>
///     Represents one or more moves that were undone.
/// </summary>
/// <param name="GameId">Stable game identifier for the current gameplay session.</param>
/// <param name="Moves">Chronological move payloads that were removed.</param>
/// <param name="ResultingFen">The exact board position after the undo is applied.</param>
/// <param name="TimestampUtc">Wall-clock timestamp when the undo payload was emitted.</param>
public readonly record struct MoveUndoneEvent(
	Guid                    GameId,
	ImmutableArray<GameMoveEvent> Moves,
	Fen                     ResultingFen,
	DateTimeOffset          TimestampUtc
);
