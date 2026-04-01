using System.Collections.Immutable;

namespace Bezoro.Chess.UCI.API.Types;

/// <summary>
///     Describes a rejected move attempt with enough data for UI error handling.
/// </summary>
/// <param name="GameId">Stable game identifier for the current gameplay session.</param>
/// <param name="AttemptedMove">The raw move string supplied by the caller.</param>
/// <param name="Reason">Human-readable rejection reason.</param>
/// <param name="LegalMoves">The current legal move list.</param>
/// <param name="IsPromotionChoicePending">Whether a pending promotion request is blocking new moves.</param>
/// <param name="TimestampUtc">Wall-clock timestamp when the rejection was emitted.</param>
public readonly record struct IllegalMoveRejectedEvent(
	Guid                   GameId,
	string                 AttemptedMove,
	string                 Reason,
	ImmutableArray<string> LegalMoves,
	bool                   IsPromotionChoicePending,
	DateTimeOffset         TimestampUtc
);
