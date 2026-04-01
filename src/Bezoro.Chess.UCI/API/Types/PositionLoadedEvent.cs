using System.Collections.Immutable;

namespace Bezoro.Chess.UCI.API.Types;

/// <summary>
///     Represents a full position load, such as a new game, save-state restore, or editor jump.
/// </summary>
/// <param name="GameId">Stable game identifier for the current gameplay session.</param>
/// <param name="BaseFen">The base FEN supplied for the load operation.</param>
/// <param name="CurrentFen">The effective FEN after played moves are applied.</param>
/// <param name="PlayedMoves">Played moves appended to the base FEN.</param>
/// <param name="TimestampUtc">Wall-clock timestamp when the position load was emitted.</param>
public readonly record struct PositionLoadedEvent(
	Guid                   GameId,
	Fen                    BaseFen,
	Fen                    CurrentFen,
	ImmutableArray<string> PlayedMoves,
	DateTimeOffset         TimestampUtc
);
