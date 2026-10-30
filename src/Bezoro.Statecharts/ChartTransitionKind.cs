namespace Bezoro.Statecharts;

/// <summary>Controls which state boundaries a transition replaces.</summary>
public enum ChartTransitionKind
{
    /// <summary>Exits and reenters the common region, including the source when targeting its descendant.</summary>
    External,
    /// <summary>Targets a strict descendant, preserving the source and its other child regions.</summary>
    Local,
    /// <summary>Requires identical source and target and executes only the transition effect.</summary>
    Internal
}
