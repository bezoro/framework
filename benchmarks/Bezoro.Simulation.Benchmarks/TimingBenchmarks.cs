using BenchmarkDotNet.Attributes;
using Bezoro.Simulation.Timing;

namespace Bezoro.Simulation.Benchmarks;

[MemoryDiagnoser]
public sealed class TimingBenchmarks
{
    private TickScheduler<int> _scheduler = null!;
    private ScheduledItem<int>[] _due = null!;
    private ScheduledHandle[] _handles = null!;
    private FixedStepState _clockState;
    private readonly FixedStepConfiguration _configuration = new(TimeSpan.FromMilliseconds(20));

    [Params(16, 1024)]
    public int ItemCount { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _scheduler = new(ItemCount);
        _due = new ScheduledItem<int>[ItemCount];
        _handles = new ScheduledHandle[ItemCount];
    }

    [Benchmark]
    public FixedStepAdvance AdvanceClock()
    {
        var result = FixedStepClock.Advance(_configuration, _clockState, TimeSpan.FromMilliseconds(16));
        _clockState = result.State;
        return result;
    }

    [Benchmark(OperationsPerInvoke = 1)]
    public int ScheduleAndDrain()
    {
        for (int i = 0; i < ItemCount; i++) _scheduler.Schedule(i, 1);
        return _scheduler.AdvanceTo(_scheduler.CurrentTick + 1, _due).Count;
    }

    [Benchmark]
    public int ScheduleAndCancel()
    {
        for (int i = 0; i < ItemCount; i++) _handles[i] = _scheduler.Schedule(i, 1_000);
        for (int i = ItemCount - 1; i >= 0; i--) _scheduler.TryCancel(_handles[i], out _);
        return _scheduler.RemainingCapacity;
    }
}
