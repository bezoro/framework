using System.Buffers.Binary;
using Bezoro.Core.Collections;
using Bezoro.Core.Randomness;
using Bezoro.Navigation;
using Bezoro.Persistence;
using Bezoro.Simulation.Attributes;
using Bezoro.Simulation.Timing;

Snapshot snapshot = RunSimulation();
await SaveRoundTripAsync(snapshot);

static Snapshot RunSimulation()
{
    // Separate streams keep loot draws independent of terrain-generation work.
    var terrainRandom = Pcg32State.Create(seed: 42, stream: 1);
    var lootRandom = Pcg32State.Create(seed: 42, stream: 2);
    var lootWeights = new WeightedIndexTable(new ulong[] { 70, 25, 5 });
    int lootIndex = lootWeights.Pick(ref lootRandom);

    const int width = 20;
    const int height = 12;
    var costs = new ushort[width * height];
    for (int cell = 0; cell < costs.Length; cell++)
        costs[cell] = (ushort)Pcg32.NextUInt32(ref terrainRandom, 5);
    // Guarantee a connected corridor while leaving weighted alternatives to search.
    for (int x = 0; x < width; x++) costs[x] = 1;
    for (int y = 0; y < height; y++) costs[y * width + width - 1] = 1;

    var map = new NavigationGrid(width, height, costs);
    var pathfinder = new GridPathfinder(map.CellCount);
    var path = new GridPoint[map.CellCount];
    var start = new GridPoint(0, 0);
    var goal = new GridPoint(width - 1, height - 1);
    GridPathResult route = pathfinder.FindPath(map, start, goal, path);
    if (!route.HasPath) throw new InvalidOperationException("The guaranteed corridor must be reachable.");

    var actors = new SlotMap<GridPoint>(capacity: 16);
    SlotHandle actor = actors.Add(start);
    actors[actor] = path[route.WrittenLength - 1];

    var attack = new StatId(1);
    var attributes = new StatSet(new[] { new StatDefinition(attack, 100, 0, 1_000) }, modifierCapacity: 8);
    StatModifierHandle buff = attributes.AddModifier(attack, new StatModifier(StatModifierOperation.AddPercent, 0.25));
    if (attributes.GetValue(attack) != 125) throw new InvalidOperationException("The buff must increase attack.");

    var scheduler = new TickScheduler<StatModifierHandle>(capacity: 8);
    scheduler.Schedule(buff, delayTicks: 25);
    Span<ScheduledItem<StatModifierHandle>> due = stackalloc ScheduledItem<StatModifierHandle>[4];
    var recentTicks = new RingBuffer<long>(capacity: 8);
    var configuration = new FixedStepConfiguration(TimeSpan.FromMilliseconds(20), maxStepsPerAdvance: 8);
    var clock = default(FixedStepState);
    for (int frame = 0; frame < 120; frame++)
    {
        FixedStepAdvance advance = FixedStepClock.Advance(configuration, clock, TimeSpan.FromMilliseconds(16));
        for (int step = 1; step <= advance.StepCount; step++)
        {
            long tick = clock.Tick + step;
            SchedulerDrainResult batch = scheduler.AdvanceTo(tick, due);
            do
            {
                for (int i = 0; i < batch.Count; i++)
                    attributes.TryRemoveModifier(due[i].Value);
                if (!batch.HasMoreDue) break;
                batch = scheduler.DrainDue(due);
            } while (true);
            recentTicks.EnqueueOverwrite(tick, out _);
        }
        clock = advance.State;
    }

    if (clock.Tick != 96 || attributes.GetValue(attack) != 100 || scheduler.Count != 0)
        throw new InvalidOperationException("Simulation or modifier expiration did not match the recorded inputs.");
    actors.Remove(actor);
    if (actors.Contains(actor)) throw new InvalidOperationException("A removed actor handle must be stale.");
    Console.WriteLine($"Route: {route.WrittenLength} cells, cost {route.TotalCost}. Loot index: {lootIndex}.");
    Console.WriteLine($"Simulation: {clock.Tick} ticks, attack {attributes.GetValue(attack)}, recent ticks {recentTicks.Count}.");
    return new Snapshot(lootRandom, clock.Tick, attributes.GetDefinition(attack).BaseValue);
}

static async Task SaveRoundTripAsync(Snapshot snapshot)
{
    // The application owns schema 1. Runtime handles are deliberately not persisted.
    byte[] encoded = EncodeSnapshot(snapshot);
    string directory = Path.Combine(Path.GetTempPath(), "bezoro-gameplay-demo", Guid.NewGuid().ToString("N"));
    try
    {
        var store = new AtomicFileSaveStore(directory, maximumPayloadBytes: 128);
        await store.WriteAsync("checkpoint", schemaVersion: 1, encoded);
        var buffer = new byte[32];
        SaveReadResult read = await store.ReadAsync("checkpoint", buffer, expectedSchemaVersion: 1);
        if (!read.IsSuccess || read.BytesWritten != buffer.Length)
            throw new InvalidOperationException("The checkpoint did not round-trip.");
        Snapshot restored = DecodeSnapshot(buffer);
        Pcg32State expectedRandom = snapshot.Random;
        Pcg32State actualRandom = restored.Random;
        if (Pcg32.NextUInt32(ref expectedRandom) != Pcg32.NextUInt32(ref actualRandom) ||
            restored.Tick != snapshot.Tick || restored.BaseAttack != snapshot.BaseAttack)
            throw new InvalidOperationException("The restored checkpoint must resume the same random stream and state.");
        await store.DeleteAsync("checkpoint");
        Console.WriteLine($"Checkpoint: schema {read.Header.SchemaVersion}, {read.BytesWritten} payload bytes, replay continuation verified.");
    }
    finally
    {
        if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
    }
}

static byte[] EncodeSnapshot(Snapshot snapshot)
{
    var bytes = new byte[32];
    BinaryPrimitives.WriteUInt64LittleEndian(bytes.AsSpan(0, 8), snapshot.Random.Value);
    BinaryPrimitives.WriteUInt64LittleEndian(bytes.AsSpan(8, 8), snapshot.Random.Stream);
    BinaryPrimitives.WriteInt64LittleEndian(bytes.AsSpan(16, 8), snapshot.Tick);
    BinaryPrimitives.WriteInt64LittleEndian(bytes.AsSpan(24, 8), BitConverter.DoubleToInt64Bits(snapshot.BaseAttack));
    return bytes;
}

static Snapshot DecodeSnapshot(byte[] bytes) => new(
    new Pcg32State(BinaryPrimitives.ReadUInt64LittleEndian(bytes.AsSpan(0, 8)),
        BinaryPrimitives.ReadUInt64LittleEndian(bytes.AsSpan(8, 8))),
    BinaryPrimitives.ReadInt64LittleEndian(bytes.AsSpan(16, 8)),
    BitConverter.Int64BitsToDouble(BinaryPrimitives.ReadInt64LittleEndian(bytes.AsSpan(24, 8))));

readonly struct Snapshot
{
    public Snapshot(Pcg32State random, long tick, double baseAttack)
    {
        if (tick < 0) throw new ArgumentOutOfRangeException(nameof(tick));
        if (double.IsNaN(baseAttack) || double.IsInfinity(baseAttack) || baseAttack < 0 || baseAttack > 1_000)
            throw new ArgumentOutOfRangeException(nameof(baseAttack));
        Random = random;
        Tick = tick;
        BaseAttack = baseAttack;
    }

    public Pcg32State Random { get; }
    public long Tick { get; }
    public double BaseAttack { get; }
}
