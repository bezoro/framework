namespace Bezoro.Simulation.Timing;

/// <summary>A snapshot of executed ticks and time not yet simulated.</summary>
public readonly record struct FixedStepState
{
    /// <summary>Creates a state. The default value is the initial state.</summary>
    public FixedStepState(long tick, TimeSpan accumulatedTime = default)
    {
        if (tick < 0) throw new ArgumentOutOfRangeException(nameof(tick));
        if (accumulatedTime.Ticks < 0) throw new ArgumentOutOfRangeException(nameof(accumulatedTime));
        Tick = tick;
        AccumulatedTime = accumulatedTime;
    }

    /// <summary>Gets the number of simulation steps already executed.</summary>
    public long Tick { get; }

    /// <summary>Gets retained time, including whole steps when preserving backlog.</summary>
    public TimeSpan AccumulatedTime { get; }
}
