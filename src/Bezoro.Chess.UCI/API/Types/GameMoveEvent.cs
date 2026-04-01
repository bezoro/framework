using Bezoro.Chess.UCI.API.Common.Enums;

namespace Bezoro.Chess.UCI.API.Types;

/// <summary>
///     Rich immutable move event payload intended for game-engine UI layers.
/// </summary>
/// <param name="GameId">Stable game identifier for the current gameplay session.</param>
/// <param name="MoveId">Stable monotonic move identifier within the game.</param>
/// <param name="Ply">One-based ply number after the move is applied.</param>
/// <param name="Actor">Who initiated the move.</param>
/// <param name="Notation">Raw UCI move notation.</param>
/// <param name="From">Origin square in algebraic notation.</param>
/// <param name="To">Destination square in algebraic notation.</param>
/// <param name="KindFlags">Structural move kind flags.</param>
/// <param name="MovingPiece">The piece that moved.</param>
/// <param name="CapturedPiece">The captured piece, when applicable.</param>
/// <param name="PromotionPiece">The chosen promotion piece, when applicable.</param>
/// <param name="SecondaryPieceMove">A secondary piece move, such as the rook movement during castling.</param>
/// <param name="PreviousFen">The exact board position before the move.</param>
/// <param name="ResultingFen">The exact board position after the move.</param>
/// <param name="IsCheck">Whether the move leaves the opposing king in check.</param>
/// <param name="IsCheckmate">Whether the move checkmates the opposing side.</param>
/// <param name="IsStalemate">Whether the move stalemates the opposing side.</param>
/// <param name="Evaluation">Optional engine evaluation associated with the resulting position.</param>
/// <param name="TimestampUtc">Wall-clock timestamp when the move payload was emitted.</param>
public readonly record struct GameMoveEvent(
	Guid               GameId,
	long               MoveId,
	int                Ply,
	GameMoveActor      Actor,
	string             Notation,
	string             From,
	string             To,
	GameMoveKindFlags  KindFlags,
	Piece              MovingPiece,
	Piece?             CapturedPiece,
	Piece?             PromotionPiece,
	PieceMove?         SecondaryPieceMove,
	Fen                PreviousFen,
	Fen                ResultingFen,
	bool               IsCheck,
	bool               IsCheckmate,
	bool               IsStalemate,
	PrincipalVariation? Evaluation,
	DateTimeOffset     TimestampUtc
);
