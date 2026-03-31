using System.Collections.Immutable;

namespace Bezoro.Chess.UCI.Protocol.API.Types;

/// <summary>
///     Represents the current playable match position, legal moves, advantage, and played-move history.
/// </summary>
/// <param name="Fen">Current board position.</param>
/// <param name="PositionKey">Stable identifier for the current position, typically the raw FEN.</param>
/// <param name="LegalMoves">Current legal moves in lowercase UCI notation.</param>
/// <param name="LegalMoveClassifications">Latest cached move classifications keyed by legal move notation.</param>
/// <param name="Advantage">Current player-relative advantage for the position.</param>
/// <param name="MoveHistory">Played moves in chronological order.</param>
public readonly record struct PlayableMatchState(
	Fen                      Fen,
	string                   PositionKey,
	ImmutableArray<string>   LegalMoves,
	ImmutableDictionary<string, MoveClassification> LegalMoveClassifications,
	PositionAdvantage        Advantage,
	ImmutableArray<PlayedMove> MoveHistory
);
