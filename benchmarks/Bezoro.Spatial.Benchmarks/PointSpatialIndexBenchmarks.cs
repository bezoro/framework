using System.Numerics;
using BenchmarkDotNet.Attributes;
using Bezoro.Core.Collections;

namespace Bezoro.Spatial.Benchmarks;

/// <summary>Measures reusable point queries and moves. Construction and output allocation are excluded.</summary>
[MemoryDiagnoser]
public class PointSpatialIndexBenchmarks
{
    private PointSpatialIndex<int> _index = null!;
    private SpatialHit<int>[] _hits = null!;
    private SpatialHit<int>[] _smallHits = null!;
    private SlotHandle _moving;
    private Bounds2D _rectangle;
    private bool _alternate;

    /// <summary>Gets or sets the populated point count.</summary>
    [Params(1024, 16384)]
    public int PointCount { get; set; }

    /// <summary>Builds a deterministic square distribution before measurement.</summary>
    [GlobalSetup]
    public void Setup()
    {
        _index = new PointSpatialIndex<int>(new Bounds2D(Vector2.Zero, new Vector2(128)), 4, PointCount, 1024);
        _hits = new SpatialHit<int>[PointCount];
        _smallHits = new SpatialHit<int>[1];
        int side = (int)Math.Sqrt(PointCount);
        float spacing = 128f / side;
        for (int i = 0; i < PointCount; i++)
        {
            var handle = _index.Add(new Vector2((i % side) * spacing, (i / side) * spacing), i);
            if (i == 0) _moving = handle;
        }
        _rectangle = new Bounds2D(new Vector2(56), new Vector2(72));
    }

    /// <summary>Queries a local circle with storage large enough for every point.</summary>
    [Benchmark]
    public SpatialQueryResult Circle() => _index.QueryCircle(new Vector2(64), 8, _hits);

    /// <summary>Queries a local rectangle with storage large enough for every point.</summary>
    [Benchmark]
    public SpatialQueryResult Rectangle() => _index.QueryRectangle(_rectangle, _hits);

    /// <summary>Counts the complete result when the supplied destination is too small.</summary>
    [Benchmark]
    public SpatialQueryResult InsufficientDestination() => _index.QueryRectangle(_rectangle, _smallHits);

    /// <summary>Moves an entry back and forth across several cell boundaries.</summary>
    [Benchmark]
    public bool MoveAcrossCells()
    {
        _alternate = !_alternate;
        return _index.TryMove(_moving, _alternate ? new Vector2(127) : Vector2.Zero);
    }
}
