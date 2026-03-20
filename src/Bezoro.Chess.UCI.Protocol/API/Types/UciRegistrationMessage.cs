namespace Bezoro.Chess.UCI.Protocol.API.Types;

/// <summary>
///     Represents a <c>registration ...</c> line emitted by a UCI engine.
/// </summary>
public sealed record UciRegistrationMessage(UciProtectionState State, string RawLine)
	: UciProtocolMessage(UciProtocolMessageType.Registration, RawLine);
