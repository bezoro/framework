namespace Bezoro.Chess.UCI.Protocol.API.Types;

/// <summary>
///     Represents a parsed UCI <c>bestmove ...</c> line.
/// </summary>
public readonly record struct UciBestMoveMessage(string BestMove, string PonderMove, string RawLine);
