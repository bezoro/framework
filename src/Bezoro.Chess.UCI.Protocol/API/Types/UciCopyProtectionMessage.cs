namespace Bezoro.Chess.UCI.Protocol.API.Types;

/// <summary>
///     Represents a <c>copyprotection ...</c> line emitted by a UCI engine.
/// </summary>
public sealed record UciCopyProtectionMessage(UciProtectionState State, string RawLine)
	: UciProtocolMessage(UciProtocolMessageType.CopyProtection, RawLine);
