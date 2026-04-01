using System.Collections.Immutable;
using Bezoro.Chess.UCI.API.Common.Enums;
using Bezoro.Chess.UCI.API.Types;

namespace Bezoro.Chess.UCI.Internal;

internal readonly record struct PendingPromotionRequest(
	long                      Id,
	GameMoveActor             Actor,
	string                    From,
	string                    To,
	Piece                     MovingPiece,
	ImmutableArray<PieceType> AllowedPromotionPieces,
	UciState                  State
);
