using System.Collections.Immutable;

namespace Bezoro.Chess.UCI.Protocol.Internal;

internal readonly record struct PositionAnalysisWorkItem(
	string                 PositionKey,
	ImmutableArray<string> Moves,
	char                   SideToMove,
	char                   PlayerColor,
	ImmutableArray<string> LegalMoves
);
