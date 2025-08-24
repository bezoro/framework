using System.Collections.Generic;

namespace Bezoro.UCI.API.Types;

public sealed record MovesSnapshot(
	IReadOnlyCollection<ParsedMove>       Legal,
	IReadOnlyDictionary<ParsedMove, Move> Classified
);
