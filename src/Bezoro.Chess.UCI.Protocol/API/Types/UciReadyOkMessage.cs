namespace Bezoro.Chess.UCI.Protocol.API.Types;

/// <summary>
///     Represents the <c>readyok</c> response.
/// </summary>
public sealed record UciReadyOkMessage(string RawLine)
	: UciProtocolMessage(UciProtocolMessageType.ReadyOk, RawLine);
