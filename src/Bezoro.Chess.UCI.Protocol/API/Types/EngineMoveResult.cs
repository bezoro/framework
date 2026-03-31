namespace Bezoro.Chess.UCI.Protocol.API.Types;

/// <summary>
///     Represents an engine move chosen during a playable match turn.
/// </summary>
/// <param name="Move">Chosen move in lowercase UCI notation.</param>
/// <param name="SearchResult">Search result that produced the move.</param>
public readonly record struct EngineMoveResult(string Move, SearchResult SearchResult);
