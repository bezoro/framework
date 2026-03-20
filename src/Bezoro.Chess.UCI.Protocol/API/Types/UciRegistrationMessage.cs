namespace Bezoro.Chess.UCI.Protocol.API.Types;

/// <summary>
///     Represents a <c>registration ...</c> line emitted by a UCI engine.
/// </summary>
public readonly record struct UciRegistrationMessage(UciProtectionState State, string RawLine);
