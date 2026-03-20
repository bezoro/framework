namespace Bezoro.Chess.UCI.Protocol.API.Types;

/// <summary>
///     Represents an <c>id name</c> or <c>id author</c> line emitted by a UCI engine.
/// </summary>
public sealed record UciIdMessage(UciIdKind Kind, string Value, string RawLine)
	: UciProtocolMessage(UciProtocolMessageType.Id, RawLine);
