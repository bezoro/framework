namespace Bezoro.Statecharts;

/// <summary>Declares a state in one region. Its position in the definition supplies its StateId.</summary>
public readonly struct ChartState
{
    /// <summary>Creates a state belonging to the supplied region.</summary>
    public ChartState(RegionId region) => Region = region;
    /// <summary>Gets the region containing this state.</summary>
    public RegionId Region { get; }
}
