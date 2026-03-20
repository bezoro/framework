namespace Bezoro.Chess.UCI.Protocol.API.Types;

/// <summary>
///     Represents the parsed score payload from an <c>info score ...</c> line.
/// </summary>
public readonly record struct UciInfoScore(int? Centipawns, int? Mate, UciScoreBound Bound);
