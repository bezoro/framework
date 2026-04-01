using Bezoro.Chess.UCI.API.Common.Enums;

namespace Bezoro.Chess.UCI.API.Types;

/// <summary>
///     Represents the resolution of a pending promotion choice.
/// </summary>
/// <param name="GameId">Stable game identifier for the current gameplay session.</param>
/// <param name="PendingPromotionId">Stable identifier for the pending promotion request.</param>
/// <param name="Notation">Final UCI move notation including the promotion suffix.</param>
/// <param name="PieceType">Chosen promotion piece type.</param>
/// <param name="TimestampUtc">Wall-clock timestamp when the choice was emitted.</param>
public readonly record struct PromotionChosenEvent(
	Guid           GameId,
	long           PendingPromotionId,
	string         Notation,
	PieceType      PieceType,
	DateTimeOffset TimestampUtc
);
