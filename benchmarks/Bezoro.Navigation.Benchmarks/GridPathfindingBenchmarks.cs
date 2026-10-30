using BenchmarkDotNet.Attributes;

namespace Bezoro.Navigation.Benchmarks;

[MemoryDiagnoser]
public class GridPathfindingBenchmarks
{
    private NavigationGrid _open = null!;
    private NavigationGrid _weighted = null!;
    private NavigationGrid _maze = null!;
    private GridPathfinder _finder = null!;
    private GridPoint[] _path = null!;
    private readonly GridPathOptions _options = new(GridMovement.EightWay);

    [Params(64, 256)]
    public int Size { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        var weighted = new ushort[Size * Size];
        var maze = new ushort[Size * Size];
        for (int y = 0; y < Size; y++)
        for (int x = 0; x < Size; x++)
        {
            weighted[y * Size + x] = (ushort)(1 + (x * 17 + y * 31) % 7);
            maze[y * Size + x] = x % 8 == 4 && y != x * 7 % Size ? (ushort)0 : (ushort)1;
        }
        _open = new(Size, Size);
        _weighted = new(Size, Size, weighted);
        _maze = new(Size, Size, maze);
        _finder = new(Size * Size);
        _path = new GridPoint[Size * Size];
    }

    [Benchmark(Baseline = true)]
    public GridPathResult OpenGrid() => _finder.FindPath(_open, new(0, 0), new(Size - 1, Size - 1), _path, _options);

    [Benchmark]
    public GridPathResult WeightedGrid() => _finder.FindPath(_weighted, new(0, 0), new(Size - 1, Size - 1), _path, _options);

    [Benchmark]
    public GridPathResult MazeGrid() => _finder.FindPath(_maze, new(0, 0), new(Size - 1, Size - 1), _path, _options);
}
