namespace Bezoro.Navigation;

/// <summary>Describes whether a route was found and copied to the caller's destination.</summary>
public enum GridPathStatus
{
    /// <summary>No path exists, or at least one endpoint is blocked.</summary>
    Unreachable = 0,
    /// <summary>The complete route was written to the destination.</summary>
    Found,
    /// <summary>A route was found, but the destination could not hold it. The destination was unchanged.</summary>
    DestinationTooSmall,
    /// <summary>Cancellation was observed before route publication. The destination was unchanged.</summary>
    Canceled,
    /// <summary>The expansion budget was exhausted before a route was found. The destination was unchanged.</summary>
    ExpansionLimitReached
}
