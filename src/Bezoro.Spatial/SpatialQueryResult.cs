namespace Bezoro.Spatial;

/// <summary>Reports the complete hit count and whether the supplied destination could hold it.</summary>
/// <remarks>The default result represents a successful empty query.</remarks>
public readonly struct SpatialQueryResult
{
    internal SpatialQueryResult(SpatialQueryStatus status, int writtenCount, int requiredCapacity)
    {
        Status = status;
        WrittenCount = writtenCount;
        RequiredCapacity = requiredCapacity;
    }

    /// <summary>Gets the query outcome.</summary>
    public SpatialQueryStatus Status { get; }
    /// <summary>Gets the number of published hits, or zero when the destination was too small.</summary>
    public int WrittenCount { get; }
    /// <summary>Gets the complete number of hits, including when no output could be published.</summary>
    public int RequiredCapacity { get; }
    /// <summary>Gets whether the whole result was published.</summary>
    public bool IsSuccess => Status == SpatialQueryStatus.Success;
}
