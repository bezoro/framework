namespace Bezoro.Chess.UCI.Protocol.API.Types;

/// <summary>
///     Represents a <c>copyprotection ...</c> line emitted by a UCI engine.
/// </summary>
public readonly record struct UciCopyProtectionMessage(UciProtectionState State, string RawLine);
