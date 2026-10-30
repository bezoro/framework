namespace Bezoro.Spatial;

/// <summary>Describes whether a spatial query could publish its complete result.</summary>
public enum SpatialQueryStatus
{
    /// <summary>The entire result was written.</summary>
    Success,
    /// <summary>The destination was too small and was left unchanged.</summary>
    DestinationTooSmall
}
