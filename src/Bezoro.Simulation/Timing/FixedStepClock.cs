namespace Bezoro.Simulation.Timing;

/// <summary>Pure fixed-step advancement with no wall clock, callbacks, or engine dependency.</summary>
public static class FixedStepClock
{
    /// <summary>
    /// Adds exact elapsed time and returns bounded simulation work.
    /// Invalid configuration or negative time is rejected. Time or tick overflow throws
    /// before any state changes, because the supplied state is immutable.
    /// </summary>
    public static FixedStepAdvance Advance(in FixedStepConfiguration configuration,
        in FixedStepState state, TimeSpan elapsed)
    {
        long stepTicks = configuration.StepDuration.Ticks;
        if (stepTicks <= 0 || configuration.MaxStepsPerAdvance <= 0 ||
            configuration.BacklogPolicy is not FixedStepBacklogPolicy.Preserve and not FixedStepBacklogPolicy.DropOverflow)
            throw new ArgumentException("A constructed, valid fixed-step configuration is required.", nameof(configuration));
        if (elapsed.Ticks < 0) throw new ArgumentOutOfRangeException(nameof(elapsed));

        long availableTime = checked(state.AccumulatedTime.Ticks + elapsed.Ticks);
        long availableSteps = availableTime / stepTicks;
        int stepCount = (int)Math.Min(availableSteps, configuration.MaxStepsPerAdvance);
        long nextTick = checked(state.Tick + stepCount);
        long remainingTime = availableTime - stepCount * stepTicks;
        long droppedTime = 0;
        if (configuration.BacklogPolicy == FixedStepBacklogPolicy.DropOverflow)
        {
            long fractionalTime = availableTime % stepTicks;
            droppedTime = remainingTime - fractionalTime;
            remainingTime = fractionalTime;
        }

        var nextState = new FixedStepState(nextTick, TimeSpan.FromTicks(remainingTime));
        return new(nextState, stepCount, TimeSpan.FromTicks(droppedTime), remainingTime / stepTicks,
            Math.Min((double)remainingTime / stepTicks, 1d));
    }

    /// <summary>
    /// Converts finite nonnegative seconds to the nearest 100 ns, using ties-to-even,
    /// then advances. For partition-exact replay, supply recorded TimeSpan values to Advance.
    /// </summary>
    public static FixedStepAdvance AdvanceSeconds(in FixedStepConfiguration configuration,
        in FixedStepState state, double elapsedSeconds)
    {
        if (double.IsNaN(elapsedSeconds) || double.IsInfinity(elapsedSeconds) || elapsedSeconds < 0)
            throw new ArgumentOutOfRangeException(nameof(elapsedSeconds));
        double ticks = Math.Round(elapsedSeconds * TimeSpan.TicksPerSecond, MidpointRounding.ToEven);
        // 2^63 is the first double at or above Int64.MaxValue. Validate before conversion.
        if (ticks >= 9223372036854775808d)
            throw new ArgumentOutOfRangeException(nameof(elapsedSeconds), "Elapsed time exceeds TimeSpan's range.");
        return Advance(configuration, state, TimeSpan.FromTicks((long)ticks));
    }
}
