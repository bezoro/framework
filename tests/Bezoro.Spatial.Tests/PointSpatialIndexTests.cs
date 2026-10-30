using System.Numerics;
using System.Runtime.CompilerServices;
using Bezoro.Core.Collections;
using Xunit;

namespace Bezoro.Spatial.Tests;

public sealed class PointSpatialIndexTests
{
    private static readonly Bounds2D World = new(new Vector2(-32), new Vector2(32));

    [Fact]
    public void Constructor_ComputesPartialCellsAndZeroCapacityWithoutAcceptingPoints()
    {
        var index = new PointSpatialIndex<int>(new Bounds2D(Vector2.Zero, new Vector2(10, 7)), 3, 0, 12);
        Assert.Equal(4, index.Columns);
        Assert.Equal(3, index.Rows);
        Assert.Equal(12, index.CellCount);
        Assert.Equal(0, index.Capacity);
        Assert.False(index.TryAdd(Vector2.One, 1, out var handle));
        Assert.True(handle.IsDefault);
        Assert.Equal(0, index.QueryRectangle(World, Span<SpatialHit<int>>.Empty).WrittenCount);
        Assert.Equal(0, index.QueryCircle(Vector2.Zero, 1, Span<SpatialHit<int>>.Empty).WrittenCount);
        index.Clear();
    }

    [Fact]
    public void Constructor_RejectsInvalidConfigurationBeforeAllocating()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new PointSpatialIndex<int>(default, 1, 10, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new PointSpatialIndex<int>(World, 0, 10, 4096));
        Assert.Throws<ArgumentOutOfRangeException>(() => new PointSpatialIndex<int>(World, -1, 10, 4096));
        Assert.Throws<ArgumentOutOfRangeException>(() => new PointSpatialIndex<int>(World, float.NaN, 10, 4096));
        Assert.Throws<ArgumentOutOfRangeException>(() => new PointSpatialIndex<int>(World, float.PositiveInfinity, 10, 4096));
        Assert.Throws<ArgumentOutOfRangeException>(() => new PointSpatialIndex<int>(World, 1, -1, 4096));
        Assert.Throws<ArgumentOutOfRangeException>(() => new PointSpatialIndex<int>(World, 1, 10, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new PointSpatialIndex<int>(World, 1, 10, 4095));
        Assert.Throws<ArgumentOutOfRangeException>(() => new PointSpatialIndex<int>(World, float.Epsilon, int.MaxValue, 4096));
        var huge = new Bounds2D(new Vector2(-float.MaxValue), new Vector2(float.MaxValue));
        Assert.Throws<ArgumentOutOfRangeException>(() => new PointSpatialIndex<int>(huge, 1, int.MaxValue, 4096));
        Assert.Throws<ArgumentOutOfRangeException>(() => new PointSpatialIndex<int>(
            new Bounds2D(Vector2.Zero, new Vector2(50_000)), 1, int.MaxValue, int.MaxValue));
    }

    [Fact]
    public void AddMoveUpdateAndRemove_PreserveHandleIdentityAndRejectStaleOrForeignHandles()
    {
        var index = Create(2);
        var first = index.Add(new Vector2(-31), 10);
        var other = Create(2).Add(Vector2.Zero, 30);
        Assert.True(index.Contains(first));
        Assert.False(index.Contains(other));
        Assert.False(index.Contains(default));
        Assert.True(index.TryMove(first, new Vector2(31)));
        Assert.True(index.TrySetValue(first, 20));
        Assert.True(index.TryGet(first, out var hit));
        Assert.Equal(first, hit.Handle);
        Assert.Equal(new Vector2(31), hit.Position);
        Assert.Equal(20, hit.Value);
        Assert.False(index.TryGet(other, out var missing));
        Assert.True(missing.Handle.IsDefault);
        Assert.False(index.TryMove(other, Vector2.Zero));
        Assert.False(index.TrySetValue(other, 0));
        Assert.False(index.Remove(other));
        Assert.True(index.Remove(first));
        Assert.False(index.Remove(first));
        Assert.False(index.TryMove(first, Vector2.Zero));
        Assert.False(index.TrySetValue(first, 0));
        Assert.False(index.TryGet(first, out _));
        var replacement = index.Add(Vector2.Zero, 40);
        Assert.NotEqual(first, replacement);
        Assert.Equal(1, index.Count);
    }

    [Fact]
    public void AddAndMoveFailure_DoNotChangeAnyEntry()
    {
        var index = Create(1);
        var handle = index.Add(Vector2.Zero, 12);
        Assert.False(index.TryAdd(Vector2.One, 5, out var rejected));
        Assert.True(rejected.IsDefault);
        Assert.False(index.TryAdd(new Vector2(33), 5, out rejected));
        Assert.True(rejected.IsDefault);
        Assert.Throws<InvalidOperationException>(() => index.Add(Vector2.One, 5));
        Assert.Throws<ArgumentOutOfRangeException>(() => index.Add(new Vector2(33), 5));
        Assert.Throws<ArgumentOutOfRangeException>(() => index.TryAdd(new Vector2(float.NaN), 5, out _));
        Assert.Throws<ArgumentOutOfRangeException>(() => index.TryMove(handle, new Vector2(float.PositiveInfinity)));
        Assert.False(index.TryMove(handle, new Vector2(33)));
        Assert.True(index.TryGet(handle, out var hit));
        Assert.Equal(Vector2.Zero, hit.Position);
        Assert.Equal(12, hit.Value);
        Assert.Equal(1, index.Count);
    }

    [Fact]
    public void Queries_IncludeWorldEdgesCellEdgesAndClosedCircleBoundary()
    {
        var index = Create(8);
        index.Add(World.Minimum, 1);
        index.Add(World.Maximum, 2);
        index.Add(new Vector2(3, 4), 3);
        index.Add(new Vector2(-3, -4), 4);
        index.Add(Vector2.Zero, 5);
        index.Add(new Vector2(3, 4.01f), 6);
        var hits = new SpatialHit<int>[8];
        AssertSet(index.QueryRectangle(World, hits), hits, 1, 2, 3, 4, 5, 6);
        AssertSet(index.QueryRectangle(new Bounds2D(World.Maximum, World.Maximum), hits), hits, 2);
        AssertSet(index.QueryRectangle(new Bounds2D(World.Minimum, World.Minimum), hits), hits, 1);
        AssertSet(index.QueryCircle(Vector2.Zero, 5, hits), hits, 3, 4, 5);
        AssertSet(index.QueryCircle(Vector2.Zero, 0, hits), hits, 5);
        AssertSet(index.QueryRectangle(new Bounds2D(new Vector2(33), new Vector2(40)), hits), hits);
        AssertSet(index.QueryCircle(new Vector2(34), 1, hits), hits);
    }

    [Fact]
    public void SmallDestination_RemainsUnchangedAndReportsTheExactRequiredCount()
    {
        var index = Create(4);
        var sentinel = index.Add(Vector2.Zero, 42);
        index.Add(Vector2.One, 43);
        index.Add(new Vector2(2), 44);
        Assert.True(index.TryGet(sentinel, out var original));
        var hits = new[] { original, original };
        var result = index.QueryRectangle(World, hits);
        Assert.False(result.IsSuccess);
        Assert.Equal(SpatialQueryStatus.DestinationTooSmall, result.Status);
        Assert.Equal(0, result.WrittenCount);
        Assert.Equal(3, result.RequiredCapacity);
        Assert.All(hits, hit => Assert.Equal(sentinel, hit.Handle));
        result = index.QueryCircle(Vector2.Zero, 10, hits);
        Assert.Equal(SpatialQueryStatus.DestinationTooSmall, result.Status);
        Assert.Equal(3, result.RequiredCapacity);
        Assert.All(hits, hit => Assert.Equal(sentinel, hit.Handle));
        AssertSet(index.QueryCircle(Vector2.Zero, 0, hits), hits, 42);
        Assert.Equal(sentinel, hits[1].Handle);
        Assert.Equal(3, index.QueryRectangle(World, Span<SpatialHit<int>>.Empty).RequiredCapacity);
        Assert.True(index.QueryRectangle(new Bounds2D(new Vector2(10), new Vector2(11)), Span<SpatialHit<int>>.Empty).IsSuccess);
    }

    [Fact]
    public void InvalidCircleArguments_ThrowBeforePublishingOutputEvenOnAnEmptyIndex()
    {
        var index = Create(1);
        var output = new SpatialHit<int>[1];
        Assert.Throws<ArgumentOutOfRangeException>(() => index.QueryCircle(new Vector2(float.NaN, 0), 1, output));
        Assert.Throws<ArgumentOutOfRangeException>(() => index.QueryCircle(new Vector2(0, float.PositiveInfinity), 1, output));
        Assert.Throws<ArgumentOutOfRangeException>(() => index.QueryCircle(Vector2.Zero, -1, output));
        Assert.Throws<ArgumentOutOfRangeException>(() => index.QueryCircle(Vector2.Zero, float.NaN, output));
        Assert.Throws<ArgumentOutOfRangeException>(() => index.QueryCircle(Vector2.Zero, float.PositiveInfinity, output));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void RemovingAnyBucketPosition_RepairsTheSwapBackLinks(int removedIndex)
    {
        var index = Create(5);
        var handles = Enumerable.Range(0, 4).Select(i => index.Add(new Vector2(-31 + i / 10f), i)).ToArray();
        Assert.True(index.Remove(handles[removedIndex]));
        var hits = new SpatialHit<int>[5];
        AssertSet(index.QueryRectangle(World, hits), hits, Enumerable.Range(0, 4).Where(i => i != removedIndex).ToArray());
        for (int i = 0; i < handles.Length; i++)
        {
            if (i == removedIndex) continue;
            Assert.True(index.TryGet(handles[i], out var hit));
            Assert.Equal(i, hit.Value);
            Assert.True(index.TryMove(handles[i], new Vector2(20 - i)));
        }
        AssertSet(index.QueryRectangle(World, hits), hits, Enumerable.Range(0, 4).Where(i => i != removedIndex).ToArray());
    }

    [Fact]
    public void RemovingFromAnotherCell_RepairsMovedTailBeforeMovingItBackIntoTheRemovedCell()
    {
        var index = Create(8);
        var a = index.Add(new Vector2(-31), 1);
        var b = index.Add(new Vector2(-30), 2);
        var c = index.Add(new Vector2(30), 3);
        var d = index.Add(new Vector2(31), 4);
        Assert.True(index.Remove(b));
        Assert.True(index.TryMove(d, new Vector2(-29)));
        Assert.True(index.Remove(a));
        Assert.True(index.TryMove(c, new Vector2(-28)));
        var hits = new SpatialHit<int>[8];
        AssertSet(index.QueryRectangle(World, hits), hits, 3, 4);
        Assert.True(index.Remove(d));
        AssertSet(index.QueryRectangle(World, hits), hits, 3);
        Assert.True(index.Remove(c));
        AssertSet(index.QueryRectangle(World, hits), hits);
    }

    [Fact]
    public void Clear_InvalidatesEveryHandleAndRetainsReusableStorage()
    {
        var index = Create(3);
        var first = index.Add(Vector2.Zero, 1);
        var second = index.Add(Vector2.One, 2);
        index.Clear();
        index.Clear();
        Assert.Equal(0, index.Count);
        Assert.Equal(3, index.AvailableCapacity);
        Assert.False(index.Contains(first));
        Assert.False(index.Contains(second));
        var replacement = index.Add(World.Maximum, 3);
        Assert.NotEqual(first, replacement);
        Assert.NotEqual(second, replacement);
        var hits = new SpatialHit<int>[3];
        AssertSet(index.QueryRectangle(World, hits), hits, 3);
    }

    [Fact]
    public void ExtremeFiniteCoordinates_KeepCircleArithmeticAndCellConversionFinite()
    {
        var world = new Bounds2D(new Vector2(-float.MaxValue), new Vector2(float.MaxValue));
        var index = new PointSpatialIndex<int>(world, float.MaxValue, 4, 4);
        Assert.Equal(4, index.CellCount);
        index.Add(new Vector2(-float.MaxValue, 0), 1);
        index.Add(Vector2.Zero, 2);
        index.Add(new Vector2(float.MaxValue, 0), 3);
        index.Add(new Vector2(float.MaxValue, float.MaxValue), 4);
        var hits = new SpatialHit<int>[4];
        AssertSet(index.QueryCircle(Vector2.Zero, float.MaxValue, hits), hits, 1, 2, 3);
        AssertSet(index.QueryCircle(new Vector2(float.MaxValue, 0), float.MaxValue, hits), hits, 2, 3, 4);
        AssertSet(index.QueryRectangle(world, hits), hits, 1, 2, 3, 4);
        var tiny = new PointSpatialIndex<int>(new Bounds2D(Vector2.Zero, new Vector2(float.Epsilon)), float.MaxValue, 1, 1);
        tiny.Add(new Vector2(float.Epsilon), 7);
        AssertSet(tiny.QueryCircle(Vector2.Zero, 0, hits), hits);
        AssertSet(tiny.QueryCircle(new Vector2(float.Epsilon), 0, hits), hits, 7);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(4)]
    [InlineData(8)]
    public void ExtremeCircleCenters_KeepInclusionIndependentOfCellSize(float cellSize)
    {
        var world = new Bounds2D(new Vector2(-2), new Vector2(2));
        var index = new PointSpatialIndex<int>(world, cellSize, 3, 16);
        index.Add(new Vector2(-1, 0), -1);
        index.Add(Vector2.Zero, 0);
        index.Add(new Vector2(1, 0), 1);
        var hits = new SpatialHit<int>[3];
        AssertSet(index.QueryCircle(new Vector2(float.MaxValue, 0), float.MaxValue, hits), hits, 0, 1);
        AssertSet(index.QueryCircle(new Vector2(-float.MaxValue, 0), float.MaxValue, hits), hits, -1, 0);
        AssertSet(index.QueryCircle(new Vector2(0, float.MaxValue), float.MaxValue, hits), hits, -1, 0, 1);
        var small = new[] { hits[0] };
        var unchanged = small[0].Handle;
        var result = index.QueryCircle(new Vector2(float.MaxValue, 0), float.MaxValue, small);
        Assert.Equal(SpatialQueryStatus.DestinationTooSmall, result.Status);
        Assert.Equal(2, result.RequiredCapacity);
        Assert.Equal(unchanged, small[0].Handle);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(17)]
    [InlineData(991)]
    public void SeededMutationsAndQueries_MatchAnIndependentBruteForceModel(int seed)
    {
        var random = new Random(seed);
        var index = new PointSpatialIndex<int>(World, 3.5f, 64, 400);
        var model = new Dictionary<SlotHandle, (Vector2 Position, int Value)>();
        var hits = new SpatialHit<int>[64];
        var stale = new List<SlotHandle>();
        for (int step = 0; step < 6000; step++)
        {
            int action = random.Next(7);
            Vector2 position = new(random.Next(-32, 33), random.Next(-32, 33));
            if (action <= 1)
            {
                bool added = index.TryAdd(position, step, out var handle);
                Assert.Equal(model.Count < 64, added);
                if (added) model.Add(handle, (position, step));
            }
            else if (model.Count > 0 && action <= 4)
            {
                var handle = model.Keys.ElementAt(random.Next(model.Count));
                if (action == 2)
                {
                    Assert.True(index.TryMove(handle, position));
                    model[handle] = (position, model[handle].Value);
                }
                else if (action == 3)
                {
                    Assert.True(index.Remove(handle));
                    model.Remove(handle);
                    stale.Add(handle);
                }
                else
                {
                    Assert.True(index.TrySetValue(handle, -step));
                    model[handle] = (model[handle].Position, -step);
                }
            }
            else if (action == 5 && step % 97 == 0)
            {
                stale.AddRange(model.Keys);
                index.Clear();
                model.Clear();
            }

            if (step % 13 == 0)
            {
                Vector2 end = new(random.Next(-40, 41), random.Next(-40, 41));
                var rectangle = new Bounds2D(Vector2.Min(position, end), Vector2.Max(position, end));
                AssertModel(index.QueryRectangle(rectangle, hits), hits, model.Where(pair =>
                    pair.Value.Position.X >= rectangle.Minimum.X && pair.Value.Position.X <= rectangle.Maximum.X &&
                    pair.Value.Position.Y >= rectangle.Minimum.Y && pair.Value.Position.Y <= rectangle.Maximum.Y));
                float radius = random.Next(0, 50);
                AssertModel(index.QueryCircle(position, radius, hits), hits, model.Where(pair =>
                {
                    double dx = (double)pair.Value.Position.X - position.X;
                    double dy = (double)pair.Value.Position.Y - position.Y;
                    return dx * dx + dy * dy <= (double)radius * radius;
                }));
                foreach (var pair in model)
                {
                    Assert.True(index.TryGet(pair.Key, out var hit));
                    Assert.Equal(pair.Value.Position, hit.Position);
                    Assert.Equal(pair.Value.Value, hit.Value);
                }
                if (stale.Count > 0) Assert.False(index.Contains(stale[random.Next(stale.Count)]));
            }
            Assert.Equal(model.Count, index.Count);
        }
    }

    [Fact]
    public async Task Queries_CanReadOneUnchangedIndexConcurrently()
    {
        var index = Create(100);
        for (int i = 0; i < 100; i++) index.Add(new Vector2(i % 10, i / 10), i);
        await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => Task.Run(() =>
        {
            var hits = new SpatialHit<int>[100];
            for (int i = 0; i < 200; i++)
            {
                Assert.Equal(100, index.QueryRectangle(World, hits).WrittenCount);
                Assert.Equal(1, index.QueryCircle(Vector2.Zero, 0, hits).WrittenCount);
                Assert.Equal(0, hits[0].Value);
            }
        })));
    }

    [Fact]
    public void WarmQueriesAndMutations_AllocateNoManagedMemory()
    {
        var index = Create(8);
        var handle = index.Add(Vector2.Zero, 1);
        var hits = new SpatialHit<int>[8];
        for (int i = 0; i < 100; i++) Exercise(index, handle, hits);
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 1000; i++) Exercise(index, handle, hits);
        long bytes = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Equal(0, bytes);
        GC.KeepAlive(hits);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RemovalAndClear_ReleaseReferencePayloads(bool clear)
    {
        var index = new PointSpatialIndex<object>(World, 4, 2, 256);
        var (handle, weak) = AddReference(index);
        if (clear) index.Clear();
        else Assert.True(index.Remove(handle));
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        Assert.False(weak.IsAlive);
        GC.KeepAlive(index);
    }

    private static void Exercise(PointSpatialIndex<int> index, SlotHandle handle, SpatialHit<int>[] hits)
    {
        index.TryMove(handle, new Vector2(12));
        index.TrySetValue(handle, 2);
        var temporary = index.Add(new Vector2(-12), 3);
        index.QueryRectangle(World, hits);
        index.QueryCircle(new Vector2(12), 1, hits);
        index.QueryCircle(new Vector2(12), 1, hits.AsSpan(0, 1));
        index.QueryRectangle(World, Span<SpatialHit<int>>.Empty);
        index.TryGet(handle, out _);
        index.Remove(temporary);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static (SlotHandle, WeakReference) AddReference(PointSpatialIndex<object> index)
    {
        var value = new object();
        return (index.Add(Vector2.Zero, value), new WeakReference(value));
    }

    private static PointSpatialIndex<int> Create(int capacity) => new(World, 4, capacity, 256);

    private static void AssertSet(SpatialQueryResult result, SpatialHit<int>[] hits, params int[] values)
    {
        Assert.True(result.IsSuccess);
        Assert.Equal(values.Length, result.WrittenCount);
        Assert.Equal(values.Length, result.RequiredCapacity);
        Assert.Equal(values.Order(), hits.Take(result.WrittenCount).Select(hit => hit.Value).Order());
    }

    private static void AssertModel(SpatialQueryResult result, SpatialHit<int>[] hits,
        IEnumerable<KeyValuePair<SlotHandle, (Vector2 Position, int Value)>> expected)
    {
        var entries = expected.ToDictionary(pair => pair.Key, pair => pair.Value);
        Assert.True(result.IsSuccess);
        Assert.Equal(entries.Count, result.WrittenCount);
        Assert.Equal(entries.Count, result.RequiredCapacity);
        for (int i = 0; i < result.WrittenCount; i++)
        {
            Assert.True(entries.Remove(hits[i].Handle, out var expectedEntry), "A missing or duplicate identity was returned.");
            Assert.Equal(expectedEntry.Position, hits[i].Position);
            Assert.Equal(expectedEntry.Value, hits[i].Value);
        }
        Assert.Empty(entries);
    }
}
