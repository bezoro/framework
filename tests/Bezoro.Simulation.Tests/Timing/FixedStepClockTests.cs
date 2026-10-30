using Bezoro.Simulation.Timing;
using Xunit;

namespace Bezoro.Simulation.Tests.Timing;

public sealed class FixedStepClockTests
{
    [Fact]
    public void Advance_AccumulatesExactPartialStepsWithoutMutatingInput()
    {
        var configuration = new FixedStepConfiguration(TimeSpan.FromTicks(10));
        var initial = new FixedStepState(12, TimeSpan.FromTicks(3));
        var first = FixedStepClock.Advance(configuration, initial, TimeSpan.FromTicks(6));
        Assert.Equal(initial, new FixedStepState(12, TimeSpan.FromTicks(3)));
        Assert.Equal(0, first.StepCount);
        Assert.Equal(0.9, first.InterpolationAlpha);
        var next = FixedStepClock.Advance(configuration, first.State, TimeSpan.FromTicks(2));
        Assert.Equal(1, next.StepCount);
        Assert.Equal(13, next.State.Tick);
        Assert.Equal(1, next.State.AccumulatedTime.Ticks);
        Assert.Equal(0.1, next.InterpolationAlpha);
    }

    [Fact]
    public void Advance_ExactTimePartitionsProduceIdenticalStateWhenBudgetDoesNotBind()
    {
        var configuration = new FixedStepConfiguration(TimeSpan.FromTicks(17), 10_000);
        var expected = FixedStepClock.Advance(configuration, default, TimeSpan.FromTicks(100_000));
        var state = default(FixedStepState);
        int steps = 0;
        int remainingTicks = 100_000;
        var random = new Random(713);
        while (remainingTicks > 0)
        {
            int ticks = Math.Min(random.Next(1, 400), remainingTicks);
            var result = FixedStepClock.Advance(configuration, state, TimeSpan.FromTicks(ticks));
            state = result.State;
            steps += result.StepCount;
            remainingTicks -= ticks;
        }
        Assert.Equal(expected.State, state);
        Assert.Equal(expected.StepCount, steps);
    }

    [Fact]
    public void Preserve_KeepsWholeBacklogAndCanCatchUpWithoutNewElapsedTime()
    {
        var configuration = new FixedStepConfiguration(TimeSpan.FromTicks(10), 2);
        var first = FixedStepClock.Advance(configuration, default, TimeSpan.FromTicks(57));
        Assert.Equal(2, first.StepCount);
        Assert.Equal(37, first.State.AccumulatedTime.Ticks);
        Assert.Equal(3, first.PendingStepCount);
        Assert.Equal(1, first.InterpolationAlpha);
        Assert.Equal(TimeSpan.Zero, first.DroppedTime);
        var second = FixedStepClock.Advance(configuration, first.State, TimeSpan.Zero);
        var third = FixedStepClock.Advance(configuration, second.State, TimeSpan.Zero);
        Assert.Equal(5, third.State.Tick);
        Assert.Equal(7, third.State.AccumulatedTime.Ticks);
        Assert.Equal(0.7, third.InterpolationAlpha);
        Assert.Equal(0, third.PendingStepCount);
    }

    [Fact]
    public void DropOverflow_ReportsDiscardedWholeStepsAndRetainsFraction()
    {
        var configuration = new FixedStepConfiguration(TimeSpan.FromTicks(10), 2,
            FixedStepBacklogPolicy.DropOverflow);
        var result = FixedStepClock.Advance(configuration, default, TimeSpan.FromTicks(57));
        Assert.Equal(2, result.StepCount);
        Assert.Equal(2, result.State.Tick);
        Assert.Equal(7, result.State.AccumulatedTime.Ticks);
        Assert.Equal(30, result.DroppedTime.Ticks);
        Assert.Equal(0, result.PendingStepCount);
        Assert.Equal(0.7, result.InterpolationAlpha);
        Assert.Equal(0, FixedStepClock.Advance(configuration, result.State, TimeSpan.Zero).StepCount);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Configuration_RejectsNonpositiveStepDuration(long ticks)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new FixedStepConfiguration(TimeSpan.FromTicks(ticks)));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Configuration_RejectsNonpositiveWorkBudget(int maxSteps)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new FixedStepConfiguration(TimeSpan.FromTicks(1), maxSteps));
    }

    [Fact]
    public void Configuration_RejectsUnknownPolicy()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new FixedStepConfiguration(TimeSpan.FromTicks(1), 8, (FixedStepBacklogPolicy)99));
    }

    [Fact]
    public void Advance_RejectsDefaultConfiguration()
    {
        Assert.Throws<ArgumentException>(() => FixedStepClock.Advance(default, default, TimeSpan.Zero));
    }

    [Fact]
    public void StateAndAdvance_RejectNegativeValues()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new FixedStepState(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new FixedStepState(0, TimeSpan.FromTicks(-1)));
        var configuration = new FixedStepConfiguration(TimeSpan.FromTicks(10));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            FixedStepClock.Advance(configuration, default, TimeSpan.FromTicks(-1)));
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    [InlineData(-0.01)]
    [InlineData(double.MaxValue)]
    public void AdvanceSeconds_RejectsInvalidOrUnrepresentableTime(double elapsed)
    {
        var configuration = new FixedStepConfiguration(TimeSpan.FromSeconds(1));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            FixedStepClock.AdvanceSeconds(configuration, default, elapsed));
    }

    [Theory]
    [InlineData(0.00000005, 0)]
    [InlineData(0.00000015, 2)]
    [InlineData(0.125, 1_250_000)]
    public void AdvanceSeconds_UsesExplicitHundredNanosecondRounding(double seconds, long expectedTicks)
    {
        var configuration = new FixedStepConfiguration(TimeSpan.FromSeconds(1));
        var result = FixedStepClock.AdvanceSeconds(configuration, default, seconds);
        Assert.Equal(expectedTicks, result.State.AccumulatedTime.Ticks);
    }

    [Fact]
    public void Advance_PreservesLongRunningTickPrecisionBeyondDoubleIntegerRange()
    {
        const long initialTick = 9_007_199_254_740_992;
        var configuration = new FixedStepConfiguration(TimeSpan.FromTicks(2));
        var result = FixedStepClock.Advance(configuration, new(initialTick), TimeSpan.FromTicks(5));
        Assert.Equal(initialTick + 2, result.State.Tick);
        Assert.Equal(1, result.State.AccumulatedTime.Ticks);
    }

    [Fact]
    public void Advance_RejectsTickOverflowBeforeReturningAnyNewState()
    {
        var configuration = new FixedStepConfiguration(TimeSpan.FromTicks(1));
        var state = new FixedStepState(long.MaxValue);
        Assert.Throws<OverflowException>(() =>
            FixedStepClock.Advance(configuration, state, TimeSpan.FromTicks(1)));
        Assert.Equal(long.MaxValue, state.Tick);
        Assert.Equal(TimeSpan.Zero, state.AccumulatedTime);
    }

    [Fact]
    public void Advance_RejectsAccumulatedTimeOverflow()
    {
        var configuration = new FixedStepConfiguration(TimeSpan.MaxValue);
        var state = new FixedStepState(0, TimeSpan.MaxValue);
        Assert.Throws<OverflowException>(() =>
            FixedStepClock.Advance(configuration, state, TimeSpan.FromTicks(1)));
    }

    [Theory]
    [InlineData(FixedStepBacklogPolicy.Preserve)]
    [InlineData(FixedStepBacklogPolicy.DropOverflow)]
    public void Advance_HandlesLargestRepresentableElapsedDuration(FixedStepBacklogPolicy policy)
    {
        var configuration = new FixedStepConfiguration(TimeSpan.FromTicks(1), 2, policy);
        var result = FixedStepClock.Advance(configuration, default, TimeSpan.MaxValue);
        Assert.Equal(2, result.State.Tick);
        Assert.Equal(2, result.StepCount);
        if (policy == FixedStepBacklogPolicy.Preserve)
        {
            Assert.Equal(long.MaxValue - 2, result.PendingStepCount);
            Assert.Equal(0, result.DroppedTime.Ticks);
        }
        else
        {
            Assert.Equal(0, result.PendingStepCount);
            Assert.Equal(long.MaxValue - 2, result.DroppedTime.Ticks);
        }
    }

    [Fact]
    public void Snapshot_CanResumeTheSameInputStream()
    {
        var configuration = new FixedStepConfiguration(TimeSpan.FromTicks(7), 4);
        var first = FixedStepClock.Advance(configuration, default, TimeSpan.FromTicks(23));
        var restored = new FixedStepState(first.State.Tick, first.State.AccumulatedTime);
        var resumed = FixedStepClock.Advance(configuration, restored, TimeSpan.FromTicks(41));
        var original = FixedStepClock.Advance(configuration, first.State, TimeSpan.FromTicks(41));
        Assert.Equal(original, resumed);
    }
}
