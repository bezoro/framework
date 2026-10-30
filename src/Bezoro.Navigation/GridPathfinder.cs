using System.Threading;

namespace Bezoro.Navigation;

/// <summary>
/// Owns reusable A* working storage. Searches allocate no managed memory after construction.
/// A map can be shared across workers; each concurrent worker must own a separate pathfinder.
/// </summary>
public sealed class GridPathfinder
{
    private readonly ulong[] _gScores;
    private readonly ulong[] _fScores;
    private readonly int[] _parents;
    private readonly uint[] _visitStamps;
    private readonly int[] _heapPositions;
    private readonly int[] _openHeap;
    private uint _searchId;
    private int _heapCount;
    private int _inUse;

    /// <summary>Allocates working storage for maps containing at most <paramref name="capacity"/> cells.</summary>
    public GridPathfinder(int capacity)
    {
        if (capacity <= 0) throw new ArgumentOutOfRangeException(nameof(capacity));
        Capacity = capacity;
        _gScores = new ulong[capacity];
        _fScores = new ulong[capacity];
        _parents = new int[capacity];
        _visitStamps = new uint[capacity];
        _heapPositions = new int[capacity];
        _openHeap = new int[capacity];
    }

    /// <summary>Gets the maximum supported map area.</summary>
    public int Capacity { get; }

    /// <summary>Finds the least-cost route and copies both endpoints into a sufficiently large destination.</summary>
    /// <remarks>
    /// Orthogonal steps cost 1000 times the destination cell's weight; diagonals cost 1414 times that weight.
    /// Failure outcomes leave the destination unchanged. Cancellation is checked during search and route sizing;
    /// the final bounded route copy completes once publication begins.
    /// Concurrent or reentrant use of this instance throws <see cref="InvalidOperationException"/>.
    /// </remarks>
    public GridPathResult FindPath(NavigationGrid grid, GridPoint start, GridPoint goal,
        Span<GridPoint> destination, GridPathOptions options = default, CancellationToken ct = default)
    {
        if (grid is null) throw new ArgumentNullException(nameof(grid));
        if (grid.CellCount > Capacity) throw new ArgumentException("Map area exceeds workspace capacity.", nameof(grid));
        int startIndex = grid.GetIndex(start);
        int goalIndex = grid.GetIndex(goal);
        if (Interlocked.CompareExchange(ref _inUse, 1, 0) != 0)
            throw new InvalidOperationException("This pathfinder is already running a search. Use one workspace per worker.");

        try
        {
            if (ct.IsCancellationRequested) return new(GridPathStatus.Canceled, 0);
            if (grid.GetCost(startIndex) == 0 || grid.GetCost(goalIndex) == 0)
                return new(GridPathStatus.Unreachable, 0);
            if (startIndex == goalIndex)
            {
                if (destination.IsEmpty) return new(GridPathStatus.DestinationTooSmall, 0, 1, 0);
                destination[0] = start;
                return new(GridPathStatus.Found, 0, 1, 0);
            }

            BeginSearch();
            _visitStamps[startIndex] = _searchId;
            _gScores[startIndex] = 0;
            _fScores[startIndex] = Heuristic(start.X, start.Y, goal, grid.MinimumTraversalCost, options.Movement);
            _parents[startIndex] = -1;
            Push(startIndex);
            int expanded = 0;

            while (_heapCount != 0)
            {
                if (ct.IsCancellationRequested) return new(GridPathStatus.Canceled, expanded);
                int current = Pop();
                if (current == goalIndex) return PublishPath(grid, goalIndex, destination, expanded, ct);
                if (expanded == options.MaxExpandedNodes)
                    return new(GridPathStatus.ExpansionLimitReached, expanded);
                expanded++;
                Expand(grid, current, goal, options);
            }
            return new(GridPathStatus.Unreachable, expanded);
        }
        finally
        {
            Volatile.Write(ref _inUse, 0);
        }
    }

    private void BeginSearch()
    {
        unchecked { _searchId++; }
        if (_searchId == 0)
        {
            Array.Clear(_visitStamps, 0, _visitStamps.Length);
            _searchId = 1;
        }
        _heapCount = 0;
    }

    private void Expand(NavigationGrid grid, int current, GridPoint goal, GridPathOptions options)
    {
        int width = grid.Width;
        int x = current % width;
        int y = current / width;
        bool left = x != 0;
        bool right = x + 1 < width;
        bool up = y != 0;
        bool down = y + 1 < grid.Height;
        if (up) Relax(grid, current, current - width, x, y - 1, 1000, goal, options.Movement);
        if (right) Relax(grid, current, current + 1, x + 1, y, 1000, goal, options.Movement);
        if (down) Relax(grid, current, current + width, x, y + 1, 1000, goal, options.Movement);
        if (left) Relax(grid, current, current - 1, x - 1, y, 1000, goal, options.Movement);
        if (options.Movement != GridMovement.EightWay) return;

        bool cut = options.AllowCornerCutting;
        if (up && left && (cut || (grid.GetCost(current - width) != 0 && grid.GetCost(current - 1) != 0)))
            Relax(grid, current, current - width - 1, x - 1, y - 1, 1414, goal, options.Movement);
        if (up && right && (cut || (grid.GetCost(current - width) != 0 && grid.GetCost(current + 1) != 0)))
            Relax(grid, current, current - width + 1, x + 1, y - 1, 1414, goal, options.Movement);
        if (down && right && (cut || (grid.GetCost(current + width) != 0 && grid.GetCost(current + 1) != 0)))
            Relax(grid, current, current + width + 1, x + 1, y + 1, 1414, goal, options.Movement);
        if (down && left && (cut || (grid.GetCost(current + width) != 0 && grid.GetCost(current - 1) != 0)))
            Relax(grid, current, current + width - 1, x - 1, y + 1, 1414, goal, options.Movement);
    }

    private void Relax(NavigationGrid grid, int current, int neighbor, int x, int y, uint step,
        GridPoint goal, GridMovement movement)
    {
        ushort weight = grid.GetCost(neighbor);
        if (weight == 0) return;
        ulong candidate = _gScores[current] + (ulong)step * weight;
        bool seen = _visitStamps[neighbor] == _searchId;
        if (seen && candidate >= _gScores[neighbor]) return;

        _visitStamps[neighbor] = _searchId;
        _gScores[neighbor] = candidate;
        _fScores[neighbor] = candidate + Heuristic(x, y, goal, grid.MinimumTraversalCost, movement);
        _parents[neighbor] = current;
        if (seen && _heapPositions[neighbor] >= 0) SiftUp(_heapPositions[neighbor]);
        else Push(neighbor);
    }

    private static ulong Heuristic(int x, int y, GridPoint goal, ushort minimumWeight, GridMovement movement)
    {
        ulong dx = (ulong)Math.Abs(x - goal.X);
        ulong dy = (ulong)Math.Abs(y - goal.Y);
        if (movement == GridMovement.FourWay) return (dx + dy) * 1000 * minimumWeight;
        ulong diagonal = Math.Min(dx, dy);
        return (diagonal * 1414 + (Math.Max(dx, dy) - diagonal) * 1000) * minimumWeight;
    }

    private GridPathResult PublishPath(NavigationGrid grid, int goal, Span<GridPoint> destination,
        int expanded, CancellationToken ct)
    {
        int length = 0;
        for (int node = goal; node >= 0; node = _parents[node])
        {
            if (ct.IsCancellationRequested) return new(GridPathStatus.Canceled, expanded);
            length++;
        }
        if (destination.Length < length)
            return new(GridPathStatus.DestinationTooSmall, expanded, length, _gScores[goal]);
        if (ct.IsCancellationRequested) return new(GridPathStatus.Canceled, expanded);
        int current = goal;
        for (int position = length - 1; position >= 0; position--)
        {
            destination[position] = new(current % grid.Width, current / grid.Width);
            current = _parents[current];
        }
        return new(GridPathStatus.Found, expanded, length, _gScores[goal]);
    }

    private bool ComesBefore(int left, int right)
    {
        if (_fScores[left] != _fScores[right]) return _fScores[left] < _fScores[right];
        if (_gScores[left] != _gScores[right]) return _gScores[left] > _gScores[right];
        return left < right;
    }

    private void Push(int node)
    {
        int position = _heapCount++;
        _openHeap[position] = node;
        _heapPositions[node] = position;
        SiftUp(position);
    }

    private int Pop()
    {
        int node = _openHeap[0];
        _heapPositions[node] = -1;
        _heapCount--;
        if (_heapCount != 0)
        {
            int replacement = _openHeap[_heapCount];
            _openHeap[0] = replacement;
            _heapPositions[replacement] = 0;
            SiftDown(0);
        }
        return node;
    }

    private void SiftUp(int position)
    {
        int node = _openHeap[position];
        while (position != 0)
        {
            int parent = (position - 1) / 2;
            int parentNode = _openHeap[parent];
            if (!ComesBefore(node, parentNode)) break;
            _openHeap[position] = parentNode;
            _heapPositions[parentNode] = position;
            position = parent;
        }
        _openHeap[position] = node;
        _heapPositions[node] = position;
    }

    private void SiftDown(int position)
    {
        int node = _openHeap[position];
        while (position < _heapCount / 2)
        {
            int child = position * 2 + 1;
            if (child + 1 < _heapCount && ComesBefore(_openHeap[child + 1], _openHeap[child])) child++;
            int childNode = _openHeap[child];
            if (!ComesBefore(childNode, node)) break;
            _openHeap[position] = childNode;
            _heapPositions[childNode] = position;
            position = child;
        }
        _openHeap[position] = node;
        _heapPositions[node] = position;
    }
}
