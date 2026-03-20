namespace Bezoro.Chess.UCI.Protocol.API.Types;

/// <summary>
///     Represents the <c>uciok</c> response.
/// </summary>
public readonly record struct UciUciOkMessage(string RawLine);
