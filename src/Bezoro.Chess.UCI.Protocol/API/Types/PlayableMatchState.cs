using System.Collections.Immutable;

namespace Bezoro.Chess.UCI.Protocol.API.Types;

/// <summary>
///     Represents the current playable match position, legal moves, advantage, played-move history, and adjudicated
///     metadata.
/// </summary>
/// <param name="Fen">Current board position.</param>
/// <param name="PositionKey">Stable identifier for the current position, typically the raw FEN.</param>
/// <param name="LegalMoves">Current legal moves in lowercase UCI notation.</param>
/// <param name="LegalMoveClassifications">Latest cached move classifications keyed by legal move notation.</param>
/// <param name="Advantage">Current player-relative advantage for the position.</param>
/// <param name="MoveHistory">Played moves in chronological order.</param>
/// <param name="PendingPromotion">Pending promotion request when the current move still requires a promotion choice.</param>
/// <param name="Result">Current adjudicated match result, or <see cref="PlayableMatchResultReason.None" /> when the match is still in progress.</param>
/// <param name="ClaimableResult">Currently claimable draw result when explicit claiming is required.</param>
/// <param name="DrawOfferedBy">Side that currently has a pending draw offer, when applicable.</param>
/// <param name="Clock">Current clock snapshot when time control is enabled.</param>
public readonly record struct PlayableMatchState(
	Fen                                   Fen,
	string                                PositionKey,
	ImmutableArray<string>                LegalMoves,
	ImmutableDictionary<string, MoveClassification> LegalMoveClassifications,
	PositionAdvantage                     Advantage,
	ImmutableArray<PlayedMove>            MoveHistory,
	PendingPromotionRequest?              PendingPromotion,
	PlayableMatchResult                   Result,
	PlayableMatchResult?                  ClaimableResult,
	char?                                 DrawOfferedBy,
	PlayableMatchClockState?              Clock
);
