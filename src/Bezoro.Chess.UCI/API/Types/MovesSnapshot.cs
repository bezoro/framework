using System.Collections.Generic;

namespace Bezoro.Chess.UCI.API.Types;

public sealed record MovesSnapshot(
	IReadOnlyCollection<ParsedMove>       Legal,
	IReadOnlyDictionary<ParsedMove, Move> Classified
);
