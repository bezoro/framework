namespace Bezoro.Simulation.Timing;

/// <summary>Immutable fixed-step duration, work budget, and backlog policy.</summary>
public readonly record struct FixedStepConfiguration
{
    /// <summary>Creates a configuration with a positive duration and execution budget.</summary>
    public FixedStepConfiguration(TimeSpan stepDuration, int maxStepsPerAdvance = 8,
        FixedStepBacklogPolicy backlogPolicy = FixedStepBacklogPolicy.Preserve)
    {
        if (stepDuration.Ticks <= 0) throw new ArgumentOutOfRangeException(nameof(stepDuration));
        if (maxStepsPerAdvance <= 0) throw new ArgumentOutOfRangeException(nameof(maxStepsPerAdvance));
        if (backlogPolicy is not FixedStepBacklogPolicy.Preserve and not FixedStepBacklogPolicy.DropOverflow)
            throw new ArgumentOutOfRangeException(nameof(backlogPolicy));
        StepDuration = stepDuration;
        MaxStepsPerAdvance = maxStepsPerAdvance;
        BacklogPolicy = backlogPolicy;
    }

    /// <summary>Gets the duration of one simulation step, in exact 100 ns units.</summary>
    public TimeSpan StepDuration { get; }

    /// <summary>Gets the maximum number of steps returned by one advance.</summary>
    public int MaxStepsPerAdvance { get; }

    /// <summary>Gets how whole steps beyond the work budget are handled.</summary>
    public FixedStepBacklogPolicy BacklogPolicy { get; }
}
