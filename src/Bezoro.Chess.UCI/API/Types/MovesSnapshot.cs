using System.Collections.Generic;

namespace Bezoro.Chess.UCI.API.Types;

/// <summary>
///     Captures the legal moves for a position and the subset with completed analysis.
/// </summary>
/// <param name="Legal">The legal parsed moves.</param>
/// <param name="Classified">Analyzed moves keyed by their parsed notation.</param>
public sealed record MovesSnapshot(
	IReadOnlyCollection<ParsedMove>       Legal,
	IReadOnlyDictionary<ParsedMove, Move> Classified
);
