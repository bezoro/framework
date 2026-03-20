namespace Bezoro.Chess.UCI.Protocol.API.Types;

/// <summary>
///     Represents an <c>option name ...</c> line emitted by a UCI engine.
/// </summary>
public sealed record UciOptionMessage(UciEngineOption Option, string RawLine)
	: UciProtocolMessage(UciProtocolMessageType.Option, RawLine);
