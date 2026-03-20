namespace Bezoro.Chess.UCI.Protocol.API.Types;

/// <summary>
///     Represents a parsed UCI <c>info ...</c> line.
/// </summary>
public readonly record struct UciInfoMessage(UciInfoPayload Payload, string RawLine);
