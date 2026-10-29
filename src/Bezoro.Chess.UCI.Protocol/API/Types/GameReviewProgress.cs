namespace Bezoro.Chess.UCI.Protocol.API.Types;

/// <summary>Immutable review progress, reported after each completed position evaluation.</summary>
/// <param name="CompletedPositions">Number of completed position evaluations.</param>
/// <param name="TotalPositions">Total number of positions, including the initial and final positions.</param>
public readonly record struct GameReviewProgress(int CompletedPositions, int TotalPositions);

