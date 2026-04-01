using System.Collections.Immutable;

namespace Bezoro.Chess.UCI.Protocol.API.Types;

/// <summary>
///     Represents a promotion move that has been selected up to the destination square but still requires the promoted
///     piece to be chosen.
/// </summary>
/// <param name="MovePrefix">Four-character UCI move prefix, such as <c>a7a8</c>.</param>
/// <param name="MovingSide">Side that must choose the promotion piece: <c>w</c> or <c>b</c>.</param>
/// <param name="MovingPiece">Moving pawn piece character.</param>
/// <param name="From">Source square.</param>
/// <param name="To">Destination square.</param>
/// <param name="PositionKey">Stable identifier for the parent position.</param>
/// <param name="Fen">Parent position.</param>
/// <param name="AllowedPromotionPieces">Allowed lowercase promotion suffixes, usually <c>q</c>, <c>r</c>, <c>b</c>, <c>n</c>.</param>
public readonly record struct PendingPromotionRequest(
	string               MovePrefix,
	char                 MovingSide,
	char                 MovingPiece,
	string               From,
	string               To,
	string               PositionKey,
	Fen                  Fen,
	ImmutableArray<char> AllowedPromotionPieces
);
