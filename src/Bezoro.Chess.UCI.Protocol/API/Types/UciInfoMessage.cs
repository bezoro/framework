namespace Bezoro.Chess.UCI.Protocol.API.Types;

/// <summary>
///     Represents a parsed UCI <c>info ...</c> line.
/// </summary>
public sealed record UciInfoMessage(UciInfoPayload Payload, string RawLine)
	: UciProtocolMessage(UciProtocolMessageType.Info, RawLine);
