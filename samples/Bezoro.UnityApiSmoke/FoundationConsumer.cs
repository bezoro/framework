using System;
using System.Threading;
using System.Threading.Tasks;
using Bezoro.Core.Collections;
using Bezoro.Core.Randomness;
using Bezoro.Navigation;
using Bezoro.Persistence;
using Bezoro.Simulation.Attributes;
using Bezoro.Simulation.Timing;

namespace Bezoro.UnityApiSmoke
{
    public static class FoundationConsumer
    {
        public static int ExerciseDataApis()
        {
            var random = Pcg32State.Create(42, 54);
            var weights = new WeightedIndexTable(new ulong[] { 3, 1 });
            int selected = weights.Pick(ref random);
            var slots = new SlotMap<int>(2);
            SlotHandle handle = slots.Add(selected);
            var recent = new RingBuffer<int>(2);
            recent.Enqueue(slots[handle]);

            var id = new StatId(1);
            var stats = new StatSet(new[] { new StatDefinition(id, 10, 0, 100) }, 2);
            StatModifierHandle modifier = stats.AddModifier(id, new StatModifier(StatModifierOperation.Add, 5));
            var scheduler = new TickScheduler<StatModifierHandle>(2);
            scheduler.Schedule(modifier, 1);
            var configuration = new FixedStepConfiguration(TimeSpan.FromMilliseconds(20));
            FixedStepAdvance advance = FixedStepClock.Advance(configuration, default, TimeSpan.FromMilliseconds(20));
            Span<ScheduledItem<StatModifierHandle>> due = stackalloc ScheduledItem<StatModifierHandle>[2];
            SchedulerDrainResult batch = scheduler.AdvanceTo(advance.State.Tick, due);
            for (int i = 0; i < batch.Count; i++) stats.TryRemoveModifier(due[i].Value);

            var grid = new NavigationGrid(2, 2);
            var pathfinder = new GridPathfinder(grid.CellCount);
            Span<GridPoint> path = stackalloc GridPoint[4];
            GridPathResult route = pathfinder.FindPath(grid, default, new GridPoint(1, 1), path);

            byte[] payload = { 1, 2, 3 };
            Span<byte> envelope = stackalloc byte[SaveEnvelope.HeaderSize + 3];
            SaveEnvelope.Write(1, payload, envelope);
            // Older C# 9 compilers need the output span to have the stack buffer's lifetime.
            ReadOnlySpan<byte> restored = envelope.Slice(0, 0);
            SaveReadStatus decoded = SaveEnvelope.Decode(envelope, 3, out SaveEnvelopeHeader header,
                out restored, expectedSchemaVersion: 1);
            if (decoded != SaveReadStatus.Success || header.SchemaVersion != 1 || restored.Length != 3 ||
                route.Status != GridPathStatus.Found || stats.GetValue(id) != 10)
                throw new InvalidOperationException("The consumer examples must produce valid results.");
            return route.WrittenLength + recent.Dequeue() + (int)advance.State.Tick;
        }

        public static async ValueTask<SaveReadResult> ExerciseStorageAsync(
            ISaveStore store, ReadOnlyMemory<byte> payload, Memory<byte> destination,
            CancellationToken cancellationToken = default)
        {
            await store.WriteAsync("checkpoint", 1, payload, cancellationToken).ConfigureAwait(false);
            return await store.ReadAsync("checkpoint", destination, 1, cancellationToken).ConfigureAwait(false);
        }
    }
}
