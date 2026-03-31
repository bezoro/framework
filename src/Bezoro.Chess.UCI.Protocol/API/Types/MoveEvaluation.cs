namespace Bezoro.Chess.UCI.Protocol.API.Types;

/// <summary>
///     Represents a scored legal move candidate from the player's perspective.
/// </summary>
/// <param name="Move">Move in UCI notation.</param>
/// <param name="Display">Human-readable score delta or mate summary.</param>
/// <param name="SortValue">Numeric value suitable for descending sort order.</param>
public readonly record struct MoveEvaluation(string Move, string Display, double SortValue);
