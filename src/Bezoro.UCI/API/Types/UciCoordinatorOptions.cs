namespace Bezoro.UCI.API.Types;

/// <summary>
///     Configuration options for the UCI Coordinator.
/// </summary>
public readonly record struct UciCoordinatorOptions(
    int PonderThreads = 2,
    int MultiPv = 1,
    uint ClassificationDepth = 6
);
