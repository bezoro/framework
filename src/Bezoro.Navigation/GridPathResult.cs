namespace Bezoro.Navigation;

/// <summary>An immutable result containing route size, weighted cost, and observed search work.</summary>
public readonly struct GridPathResult
{
    internal GridPathResult(GridPathStatus status, int expandedNodes, int requiredPathLength = 0, ulong? totalCost = null)
    {
        Status = status;
        ExpandedNodes = expandedNodes;
        RequiredPathLength = requiredPathLength;
        TotalCost = totalCost;
    }

    /// <summary>Gets the search outcome.</summary>
    public GridPathStatus Status { get; }

    /// <summary>Gets whether a route exists, including when the destination was too small.</summary>
    public bool HasPath => Status == GridPathStatus.Found || Status == GridPathStatus.DestinationTooSmall;

    /// <summary>Gets the complete point count, including both endpoints, or zero when no route was determined.</summary>
    public int RequiredPathLength { get; }

    /// <summary>Gets the number of points written, which is zero unless the complete route was copied.</summary>
    public int WrittenLength => Status == GridPathStatus.Found ? RequiredPathLength : 0;

    /// <summary>Gets the weighted cost, excluding the start cell, or null when no route was determined.</summary>
    public ulong? TotalCost { get; }

    /// <summary>Gets the count of nodes whose outgoing edges were examined. The goal is not expanded.</summary>
    public int ExpandedNodes { get; }
}
