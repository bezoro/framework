namespace Bezoro.Chess.UCI.Protocol.API.Types;

/// <summary>
///     Represents an <c>id name</c> or <c>id author</c> line emitted by a UCI engine.
/// </summary>
public readonly record struct UciIdMessage(UciIdKind Kind, string Value, string RawLine);
