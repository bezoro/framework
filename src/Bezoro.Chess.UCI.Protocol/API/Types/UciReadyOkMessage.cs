namespace Bezoro.Chess.UCI.Protocol.API.Types;

/// <summary>
///     Represents the <c>readyok</c> response.
/// </summary>
public readonly record struct UciReadyOkMessage(string RawLine);
