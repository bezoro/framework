namespace Bezoro.Simulation.Timing;

/// <summary>The immutable result of advancing a fixed-step clock.</summary>
public readonly record struct FixedStepAdvance
{
    internal FixedStepAdvance(FixedStepState state, int stepCount, TimeSpan droppedTime,
        long pendingStepCount, double interpolationAlpha)
    {
        State = state;
        StepCount = stepCount;
        DroppedTime = droppedTime;
        PendingStepCount = pendingStepCount;
        InterpolationAlpha = interpolationAlpha;
    }

    /// <summary>Gets the state to retain after executing the returned steps.</summary>
    public FixedStepState State { get; }

    /// <summary>Gets how many simulation steps the host should execute now.</summary>
    public int StepCount { get; }

    /// <summary>Gets the time discarded by this call under the drop-overflow policy.</summary>
    public TimeSpan DroppedTime { get; }

    /// <summary>Gets how many whole steps remain in the preserved backlog.</summary>
    public long PendingStepCount { get; }

    /// <summary>
    /// Gets accumulated time divided by step duration, clamped to one.
    /// A preserved backlog therefore produces one rather than extrapolation.
    /// </summary>
    public double InterpolationAlpha { get; }
}
