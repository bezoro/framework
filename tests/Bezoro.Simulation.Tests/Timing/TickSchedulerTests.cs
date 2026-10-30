using System.Reflection;
using System.Runtime.CompilerServices;
using Bezoro.Simulation.Timing;
using Xunit;

namespace Bezoro.Simulation.Tests.Timing;

public sealed class TickSchedulerTests
{
    [Fact]
    public void Drain_UsesDueTimeThenOriginalInsertionOrder()
    {
        var scheduler = new TickScheduler<int>(5);
        scheduler.ScheduleAt(30, 30);
        scheduler.ScheduleAt(10, 10);
        scheduler.ScheduleAt(11, 10);
        scheduler.ScheduleAt(20, 20);
        Span<ScheduledItem<int>> items = stackalloc ScheduledItem<int>[5];
        var result = scheduler.AdvanceTo(30, items);
        Assert.Equal(4, result.Count);
        Assert.False(result.HasMoreDue);
        Assert.Equal(new[] { 10, 11, 20, 30 }, items[..result.Count].ToArray().Select(static item => item.Value));
        Assert.Equal(new long[] { 10, 10, 20, 30 }, items[..result.Count].ToArray().Select(static item => item.DueTick));
        Assert.Equal(0, scheduler.Count);
        Assert.Equal(5, scheduler.RemainingCapacity);
    }

    [Fact]
    public void Drain_WithShortOrEmptyBuffer_PreservesRemainingDueWork()
    {
        var scheduler = new TickScheduler<int>(4);
        for (int i = 0; i < 4; i++) scheduler.Schedule(i, 2);
        var empty = scheduler.AdvanceTo(2, Span<ScheduledItem<int>>.Empty);
        Assert.Equal(0, empty.Count);
        Assert.True(empty.HasMoreDue);
        Span<ScheduledItem<int>> buffer = stackalloc ScheduledItem<int>[1];
        for (int i = 0; i < 4; i++)
        {
            var result = scheduler.DrainDue(buffer);
            Assert.Equal(1, result.Count);
            Assert.Equal(i, buffer[0].Value);
            Assert.Equal(i != 3, result.HasMoreDue);
        }
        Assert.False(scheduler.HasDueItems);
        Assert.Equal(0, scheduler.DrainDue(buffer).Count);
    }

    [Fact]
    public void Reschedule_RepairsHeapInBothDirectionsAndRetainsTieOrder()
    {
        var scheduler = new TickScheduler<int>(4);
        var first = scheduler.ScheduleAt(1, 10);
        var second = scheduler.ScheduleAt(2, 20);
        var third = scheduler.ScheduleAt(3, 30);
        Assert.True(scheduler.TryRescheduleAt(third, 5));
        Assert.True(scheduler.TryPeek(out var earliest));
        Assert.Equal(3, earliest.Value);
        Assert.True(scheduler.TryRescheduleAt(third, 20));
        Assert.True(scheduler.TryRescheduleAt(first, 20));
        Span<ScheduledItem<int>> buffer = stackalloc ScheduledItem<int>[4];
        var result = scheduler.AdvanceTo(20, buffer);
        Assert.Equal(new[] { 1, 2, 3 }, buffer[..result.Count].ToArray().Select(static item => item.Value));
        Assert.False(scheduler.Contains(first));
        Assert.False(scheduler.Contains(second));
        Assert.False(scheduler.Contains(third));
    }

    [Fact]
    public void Cancel_ReusesCapacityWithoutAcceptingStaleHandles()
    {
        var scheduler = new TickScheduler<string>(1);
        var original = scheduler.Schedule("first", long.MaxValue);
        Assert.True(scheduler.TryCancel(original, out var canceled));
        Assert.Equal("first", canceled);
        var replacement = scheduler.Schedule("second", 10);
        Assert.NotEqual(original, replacement);
        Assert.False(scheduler.TryCancel(original, out _));
        Assert.False(scheduler.TryGet(original, out _));
        Assert.False(scheduler.TryRescheduleAt(original, 30));
        Assert.True(scheduler.Contains(replacement));
        Assert.True(scheduler.TryGet(replacement, out var item));
        Assert.Equal("second", item.Value);
        Assert.Equal(10, item.DueTick);
    }

    [Fact]
    public void Handles_AreScopedAcrossOwnersAndPayloadTypes()
    {
        var first = new TickScheduler<int>(1);
        var second = new TickScheduler<int>(1);
        var text = new TickScheduler<string>(1);
        var firstHandle = first.Schedule(1, 0);
        var secondHandle = second.Schedule(2, 0);
        var textHandle = text.Schedule("three", 0);
        Assert.NotEqual(firstHandle, secondHandle);
        Assert.NotEqual(firstHandle, textHandle);
        Assert.False(second.Contains(firstHandle));
        Assert.False(text.TryCancel(firstHandle, out _));
        Assert.False(first.Contains(default));
        Assert.True(default(ScheduledHandle).IsDefault);
        Assert.False(firstHandle.IsDefault);
    }

    [Fact]
    public void Clear_InvalidatesHandlesAndRetainsCurrentTick()
    {
        var scheduler = new TickScheduler<int>(3, 20);
        var first = scheduler.Schedule(1, 0);
        var second = scheduler.Schedule(2, 5);
        scheduler.Clear();
        Assert.Equal(0, scheduler.Count);
        Assert.Equal(3, scheduler.RemainingCapacity);
        Assert.Equal(20, scheduler.CurrentTick);
        Assert.False(scheduler.Contains(first));
        Assert.False(scheduler.Contains(second));
        var next = scheduler.Schedule(3, 0);
        Assert.NotEqual(first, next);
        Assert.NotEqual(second, next);
        Assert.False(scheduler.TryGet(first, out _));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void Capacity_RejectsAdditionalWorkWithoutChangingQueuedItems(int capacity)
    {
        var scheduler = new TickScheduler<int>(capacity);
        for (int i = 0; i < capacity; i++) scheduler.Schedule(i, 100);
        Assert.False(scheduler.TrySchedule(99, 100, out var handle));
        Assert.True(handle.IsDefault);
        Assert.Throws<InvalidOperationException>(() => scheduler.Schedule(99, 100));
        Assert.Equal(capacity, scheduler.Count);
        Assert.Equal(0, scheduler.RemainingCapacity);
    }

    [Fact]
    public void CancelingFarFutureItems_DoesNotLeaveTombstonesOrConsumeCapacity()
    {
        var scheduler = new TickScheduler<int>(8);
        for (int i = 0; i < 10_000; i++)
        {
            var handle = scheduler.Schedule(i, long.MaxValue);
            Assert.True(scheduler.TryCancel(handle, out _));
        }
        Assert.Equal(0, scheduler.Count);
        Assert.Equal(8, scheduler.RemainingCapacity);
        Assert.False(scheduler.TryPeek(out _));
    }

    [Fact]
    public void Time_RejectsNegativeBackwardAndOverflowValuesWithoutMutation()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new TickScheduler<int>(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new TickScheduler<int>(1, -1));
        var scheduler = new TickScheduler<int>(2, 100);
        var handle = scheduler.Schedule(1, 10);
        Assert.Throws<ArgumentOutOfRangeException>(() => scheduler.Schedule(2, -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => scheduler.ScheduleAt(2, 99));
        Assert.Throws<ArgumentOutOfRangeException>(() => scheduler.TryRescheduleAt(handle, 99));
        Assert.Throws<ArgumentOutOfRangeException>(() => scheduler.AdvanceTo(99, Span<ScheduledItem<int>>.Empty));
        Assert.Throws<OverflowException>(() => scheduler.Schedule(2, long.MaxValue));
        Assert.Equal(100, scheduler.CurrentTick);
        Assert.Equal(1, scheduler.Count);
        Assert.True(scheduler.TryGet(handle, out var unchanged));
        Assert.Equal(110, unchanged.DueTick);
    }

    [Fact]
    public void Time_AllowsLargestTickWithoutOverflow()
    {
        var scheduler = new TickScheduler<int>(1, long.MaxValue - 1);
        scheduler.Schedule(1, 1);
        Span<ScheduledItem<int>> buffer = stackalloc ScheduledItem<int>[1];
        Assert.Equal(1, scheduler.AdvanceTo(long.MaxValue, buffer).Count);
        scheduler.Schedule(2, 0);
        Assert.Equal(1, scheduler.DrainDue(buffer).Count);
        Assert.Equal(2, buffer[0].Value);
    }

    [Fact]
    public void GenerationExhaustion_RetiresSlotInsteadOfRevivingOldHandles()
    {
        var scheduler = new TickScheduler<int>(1);
        var original = scheduler.Schedule(5, 0);
        Array nodes = (Array)typeof(TickScheduler<int>).GetField("_nodes", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(scheduler)!;
        object node = nodes.GetValue(0)!;
        node.GetType().GetField("Generation", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(node, uint.MaxValue);
        nodes.SetValue(node, 0);
        Span<ScheduledItem<int>> buffer = stackalloc ScheduledItem<int>[1];
        Assert.Equal(1, scheduler.DrainDue(buffer).Count);
        Assert.False(scheduler.Contains(original));
        Assert.False(scheduler.Contains(buffer[0].Handle));
        Assert.Equal(1, scheduler.RetiredSlotCount);
        Assert.Equal(0, scheduler.RemainingCapacity);
        scheduler.Clear();
        Assert.False(scheduler.TrySchedule(6, 0, out _));
    }

    [Fact]
    public void SequenceExhaustion_ThrowsWithoutConsumingSlotAndClearAllowsReuse()
    {
        var scheduler = new TickScheduler<int>(1);
        typeof(TickScheduler<int>).GetField("_nextSequence", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(scheduler, ulong.MaxValue);
        Assert.Throws<OverflowException>(() => scheduler.Schedule(1, 0));
        Assert.Equal(0, scheduler.Count);
        Assert.Equal(1, scheduler.RemainingCapacity);
        scheduler.Clear();
        Assert.False(scheduler.Schedule(2, 0).IsDefault);
    }

    [Fact]
    public void RandomizedScheduling_MatchesReferenceOrderedQueue()
    {
        var scheduler = new TickScheduler<int>(64);
        var expected = new Dictionary<int, (ScheduledHandle Handle, long Due, long Order)>();
        var random = new Random(421);
        var buffer = new ScheduledItem<int>[7];
        long order = 0;
        int nextValue = 0;
        for (int operation = 0; operation < 8_000; operation++)
        {
            switch (random.Next(5))
            {
                case 0:
                case 1:
                    long due = scheduler.CurrentTick + random.Next(50);
                    bool added = scheduler.TryScheduleAt(nextValue, due, out var handle);
                    Assert.Equal(expected.Count < scheduler.Capacity, added);
                    if (added) expected.Add(nextValue++, (handle, due, order++));
                    break;
                case 2 when expected.Count != 0:
                    int canceledKey = expected.Keys.ElementAt(random.Next(expected.Count));
                    var canceled = expected[canceledKey];
                    Assert.True(scheduler.TryCancel(canceled.Handle, out var value));
                    Assert.Equal(canceledKey, value);
                    expected.Remove(canceledKey);
                    break;
                case 3 when expected.Count != 0:
                    int rescheduledKey = expected.Keys.ElementAt(random.Next(expected.Count));
                    var rescheduled = expected[rescheduledKey];
                    long newDue = scheduler.CurrentTick + random.Next(50);
                    Assert.True(scheduler.TryRescheduleAt(rescheduled.Handle, newDue));
                    expected[rescheduledKey] = (rescheduled.Handle, newDue, rescheduled.Order);
                    break;
                default:
                    long tick = scheduler.CurrentTick + random.Next(10);
                    var expectedItems = expected.OrderBy(static pair => pair.Value.Due)
                        .ThenBy(static pair => pair.Value.Order).Where(pair => pair.Value.Due <= tick)
                        .Take(buffer.Length).ToArray();
                    var drained = scheduler.AdvanceTo(tick, buffer);
                    Assert.Equal(expectedItems.Length, drained.Count);
                    for (int i = 0; i < drained.Count; i++)
                    {
                        Assert.Equal(expectedItems[i].Key, buffer[i].Value);
                        Assert.Equal(expectedItems[i].Value.Handle, buffer[i].Handle);
                        Assert.Equal(expectedItems[i].Value.Due, buffer[i].DueTick);
                        expected.Remove(buffer[i].Value);
                    }
                    Assert.Equal(expected.Any(pair => pair.Value.Due <= tick), drained.HasMoreDue);
                    break;
            }
            Assert.Equal(expected.Count, scheduler.Count);
            Assert.Equal(scheduler.Capacity - expected.Count, scheduler.RemainingCapacity);
            if (expected.Count > 0)
            {
                Assert.True(scheduler.TryPeek(out var earliest));
                var reference = expected.OrderBy(static pair => pair.Value.Due)
                    .ThenBy(static pair => pair.Value.Order).First();
                Assert.Equal(reference.Key, earliest.Value);
            }
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void ReleasingItems_DropsOwnedPayloadReferences(int removalMode)
    {
        var scheduler = new TickScheduler<object>(1);
        WeakReference payload = AddAndReleasePayload(scheduler, removalMode);
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        Assert.False(payload.IsAlive);
        GC.KeepAlive(scheduler);
    }

    [Fact]
    public void SteadyStateSchedulingCancellationAndDraining_DoNotAllocate()
    {
        var scheduler = new TickScheduler<int>(1);
        MeasureChurn(scheduler);
        var samples = new long[8];
        for (int sample = 0; sample < samples.Length; sample++)
            samples[sample] = MeasureChurn(scheduler);
        Assert.True(samples.Min() == 0, $"Allocated bytes per fixed batch: {string.Join(", ", samples)}");
        Assert.Equal(0, scheduler.Count);
        Assert.Equal(1, scheduler.RemainingCapacity);
    }

    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
    private static long MeasureChurn(TickScheduler<int> scheduler)
    {
        Span<ScheduledItem<int>> buffer = stackalloc ScheduledItem<int>[1];
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int operation = 0; operation < 10_000; operation++)
        {
            var handle = scheduler.Schedule(operation, 0);
            scheduler.TryCancel(handle, out _);
            scheduler.Schedule(operation, 0);
            scheduler.DrainDue(buffer);
        }
        return GC.GetAllocatedBytesForCurrentThread() - before;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference AddAndReleasePayload(TickScheduler<object> scheduler, int removalMode)
    {
        var payload = new object();
        var reference = new WeakReference(payload);
        var handle = scheduler.Schedule(payload, 0);
        if (removalMode == 0) scheduler.TryCancel(handle, out _);
        else if (removalMode == 1) scheduler.Clear();
        else scheduler.DrainDue(new ScheduledItem<object>[1]);
        return reference;
    }
}
