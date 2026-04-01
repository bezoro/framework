using System.Collections.Immutable;
using Bezoro.Chess.UCI.API.Common.Enums;

namespace Bezoro.Chess.UCI.API.Types;

/// <summary>
///     Represents a pending promotion choice that must be resolved before a move can be applied.
/// </summary>
/// <param name="GameId">Stable game identifier for the current gameplay session.</param>
/// <param name="PendingPromotionId">Stable identifier for the pending promotion request.</param>
/// <param name="Actor">Who initiated the pending move.</param>
/// <param name="From">Origin square in algebraic notation.</param>
/// <param name="To">Destination square in algebraic notation.</param>
/// <param name="MovingPiece">The pawn awaiting promotion.</param>
/// <param name="AllowedPromotionPieces">Allowed promotion piece types in deterministic display order.</param>
/// <param name="PreviousFen">The exact board position before the pending move.</param>
/// <param name="TimestampUtc">Wall-clock timestamp when the request was emitted.</param>
public readonly record struct PromotionRequiredEvent(
	Guid                     GameId,
	long                     PendingPromotionId,
	GameMoveActor            Actor,
	string                   From,
	string                   To,
	Piece                    MovingPiece,
	ImmutableArray<PieceType> AllowedPromotionPieces,
	Fen                      PreviousFen,
	DateTimeOffset           TimestampUtc
);
