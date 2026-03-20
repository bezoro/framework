namespace Bezoro.Chess.UCI.Protocol.API.Types;

/// <summary>
///     Represents the <c>uciok</c> response.
/// </summary>
public sealed record UciUciOkMessage(string RawLine)
	: UciProtocolMessage(UciProtocolMessageType.UciOk, RawLine);
