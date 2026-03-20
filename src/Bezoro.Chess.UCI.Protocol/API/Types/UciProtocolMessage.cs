namespace Bezoro.Chess.UCI.Protocol.API.Types;

/// <summary>
///     Base type for a parsed line of UCI protocol output.
/// </summary>
public abstract record UciProtocolMessage(UciProtocolMessageType Type, string RawLine);
