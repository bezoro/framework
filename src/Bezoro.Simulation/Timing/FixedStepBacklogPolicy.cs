namespace Bezoro.Simulation.Timing;

/// <summary>Determines what happens to whole steps beyond the per-advance execution budget.</summary>
public enum FixedStepBacklogPolicy
{
    /// <summary>Retains unexecuted time so later advances can catch up.</summary>
    Preserve,

    /// <summary>Discards excess whole steps, while retaining the fractional remainder.</summary>
    DropOverflow
}
