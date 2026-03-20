namespace Bezoro.Chess.UCI.Protocol.API.Types;

/// <summary>
///     Represents a parsed UCI <c>bestmove ...</c> line.
/// </summary>
public sealed record UciBestMoveMessage(string BestMove, string PonderMove, string RawLine)
	: UciProtocolMessage(UciProtocolMessageType.BestMove, RawLine);
