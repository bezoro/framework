using System.Reflection;
using System.Runtime.CompilerServices;
using Xunit;

namespace Bezoro.Navigation.Tests;

public sealed class GridPathfinderTests
{
    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, 0)]
    [InlineData(-1, 1)]
    [InlineData(1, -1)]
    [InlineData(int.MaxValue, 2)]
    public void GridRejectsInvalidAreaBeforeAllocating(int width, int height)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new NavigationGrid(width, height));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    [InlineData(5)]
    public void GridRequiresExactlyOneCostPerCell(int length)
    {
        Assert.Throws<ArgumentException>(() => new NavigationGrid(2, 2, new ushort[length]));
    }

    [Fact]
    public void GridCopiesCostsAndFindsSmallestWalkableWeight()
    {
        ushort[] costs = [0, 8, 3, 9];
        var grid = new NavigationGrid(2, 2, costs);
        costs[2] = 0;

        Assert.Equal((ushort)3, grid[0, 1]);
        Assert.Equal((ushort)3, grid.MinimumTraversalCost);
        Assert.Equal(4, grid.CellCount);
        Assert.False(grid.IsWalkable(new(-1, 0)));
        Assert.False(grid.IsWalkable(new(0, 0)));
        Assert.True(grid.IsWalkable(new(0, 1)));
        Assert.Throws<ArgumentOutOfRangeException>(() => grid[-1, 0]);
        Assert.Throws<ArgumentOutOfRangeException>(() => grid[2, 0]);
        Assert.Equal((ushort)0, new NavigationGrid(2, 2, traversalCost: 0).MinimumTraversalCost);
    }

    [Fact]
    public void DefaultOptionsAndResultHaveUsefulValues()
    {
        GridPoint point = default;
        Assert.Equal(new GridPoint(0, 0), point);
        GridPathOptions options = default;
        Assert.Equal(GridMovement.FourWay, options.Movement);
        Assert.False(options.AllowCornerCutting);
        Assert.Equal(int.MaxValue, options.MaxExpandedNodes);
        GridPathResult result = default;
        Assert.Equal(GridPathStatus.Unreachable, result.Status);
        Assert.False(result.HasPath);
        Assert.Null(result.TotalCost);
        Assert.Equal(0, result.WrittenLength);
    }

    [Fact]
    public void CancellationPrecedesBlockedEndpointsAndTrivialPaths()
    {
        using var canceled = new CancellationTokenSource();
        canceled.Cancel();
        var finder = new GridPathfinder(1);
        var result = finder.FindPath(new NavigationGrid(1, 1, traversalCost: 0), default, default,
            Span<GridPoint>.Empty, new(maxExpandedNodes: 0), canceled.Token);
        Assert.Equal(GridPathStatus.Canceled, result.Status);
        Assert.Equal(0, result.RequiredPathLength);
        Assert.Null(result.TotalCost);
    }

    [Fact]
    public void BlockedStartEqualsGoalIsUnreachableEvenWithZeroBudgetAndNoDestination()
    {
        var result = new GridPathfinder(1).FindPath(new NavigationGrid(1, 1, traversalCost: 0),
            default, default, Span<GridPoint>.Empty, new(maxExpandedNodes: 0));
        Assert.Equal(GridPathStatus.Unreachable, result.Status);
        Assert.Equal(0, result.RequiredPathLength);
        Assert.Null(result.TotalCost);
    }

    [Fact]
    public void InvalidOptionsAndWorkspaceCapacityAreRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new GridPathOptions((GridMovement)9));
        Assert.Throws<ArgumentOutOfRangeException>(() => new GridPathOptions(maxExpandedNodes: -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new GridPathfinder(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new GridPathfinder(-1));
    }

    [Fact]
    public void SearchRejectsInvalidArgumentsWithoutPoisoningWorkspace()
    {
        var finder = new GridPathfinder(4);
        var grid = new NavigationGrid(2, 2);
        GridPoint[] path = new GridPoint[4];
        Assert.Throws<ArgumentNullException>(() => finder.FindPath(null!, new(0, 0), new(1, 1), path));
        Assert.Throws<ArgumentException>(() => finder.FindPath(new NavigationGrid(3, 2), new(0, 0), new(1, 1), path));
        Assert.Throws<ArgumentOutOfRangeException>(() => finder.FindPath(grid, new(-1, 0), new(1, 1), path));
        Assert.Throws<ArgumentOutOfRangeException>(() => finder.FindPath(grid, new(0, 0), new(2, 0), path));
        Assert.Equal(GridPathStatus.Found, finder.FindPath(grid, new(0, 0), new(1, 1), path).Status);
    }

    [Fact]
    public void FourWayRouteContainsBothEndpointsAndExcludesStartWeight()
    {
        var grid = new NavigationGrid(3, 1, new ushort[] { 100, 4, 7 });
        var finder = new GridPathfinder(grid.CellCount);
        GridPoint[] path = new GridPoint[5];
        path[3] = path[4] = new(-1, -1);

        var result = finder.FindPath(grid, new(0, 0), new(2, 0), path);

        Assert.Equal(GridPathStatus.Found, result.Status);
        Assert.True(result.HasPath);
        Assert.Equal(3, result.RequiredPathLength);
        Assert.Equal(3, result.WrittenLength);
        Assert.Equal((ulong)11_000, result.TotalCost);
        Assert.Equal(new[] { new GridPoint(0, 0), new(1, 0), new(2, 0) }, path[..3]);
        Assert.Equal(new(-1, -1), path[3]);
        Assert.Equal(new(-1, -1), path[4]);
    }

    [Fact]
    public void WeightedRouteCanPreferMoreSteps()
    {
        var grid = new NavigationGrid(3, 2, new ushort[] { 1, 100, 1, 1, 1, 1 });
        GridPoint[] path = new GridPoint[grid.CellCount];
        var result = new GridPathfinder(grid.CellCount).FindPath(grid, new(0, 0), new(2, 0), path);

        Assert.Equal((ulong)4_000, result.TotalCost);
        Assert.Equal(5, result.WrittenLength);
        Assert.DoesNotContain(new GridPoint(1, 0), path[..result.WrittenLength]);
        AssertValidRoute(grid, path.AsSpan(0, result.WrittenLength), new(0, 0), new(2, 0), default, result.TotalCost!.Value);
    }

    [Fact]
    public void MaximumWeightsDoNotOverflow()
    {
        var grid = new NavigationGrid(3, 1, ushort.MaxValue);
        var result = new GridPathfinder(3).FindPath(grid, new(0, 0), new(2, 0), new GridPoint[3]);
        Assert.Equal(2UL * ushort.MaxValue * 1000, result.TotalCost);
    }

    [Fact]
    public void RouteCostsCanExceedUInt32WithoutTruncation()
    {
        var grid = new NavigationGrid(67, 1, ushort.MaxValue);
        GridPoint[] path = new GridPoint[67];
        var result = new GridPathfinder(67).FindPath(grid, new(0, 0), new(66, 0), path);

        Assert.Equal(GridPathStatus.Found, result.Status);
        Assert.Equal(4_325_310_000UL, result.TotalCost);
        Assert.Equal(67, result.RequiredPathLength);
        Assert.Equal(67, result.WrittenLength);
        Assert.Equal(new GridPoint(0, 0), path[0]);
        Assert.Equal(new GridPoint(66, 0), path[^1]);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DiagonalCornerPolicyIsExplicit(bool allowCornerCutting)
    {
        var grid = new NavigationGrid(2, 2, new ushort[] { 1, 0, 0, 3 });
        var options = new GridPathOptions(GridMovement.EightWay, allowCornerCutting);
        GridPoint[] path = new GridPoint[4];
        var result = new GridPathfinder(4).FindPath(grid, new(0, 0), new(1, 1), path, options);

        Assert.Equal(allowCornerCutting ? GridPathStatus.Found : GridPathStatus.Unreachable, result.Status);
        if (allowCornerCutting)
        {
            Assert.Equal((ulong)4_242, result.TotalCost);
            Assert.Equal(2, result.WrittenLength);
        }
        else Assert.Null(result.TotalCost);
    }

    [Fact]
    public void OneBlockedNeighborAlsoForbidsCuttingTheCorner()
    {
        var grid = new NavigationGrid(2, 2, new ushort[] { 1, 0, 1, 1 });
        GridPoint[] path = new GridPoint[4];
        var result = new GridPathfinder(4).FindPath(grid, new(0, 0), new(1, 1), path,
            new(GridMovement.EightWay));
        Assert.Equal((ulong)2_000, result.TotalCost);
        Assert.Equal(new GridPoint(0, 1), path[1]);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(0, 1)]
    [InlineData(1, 0)]
    public void BlockedEndpointsHaveNoRoute(int startX, int goalX)
    {
        var grid = new NavigationGrid(2, 1, new ushort[] { 0, 1 });
        GridPoint[] path = [new(-1, -1), new(-1, -1)];
        var result = new GridPathfinder(2).FindPath(grid, new(startX, 0), new(goalX, 0), path);
        Assert.Equal(GridPathStatus.Unreachable, result.Status);
        Assert.Equal(0, result.ExpandedNodes);
        Assert.All(path, point => Assert.Equal(new(-1, -1), point));
    }

    [Fact]
    public void StartEqualsGoalRequiresOnePointAndNoExpansion()
    {
        var grid = new NavigationGrid(1, 1);
        var finder = new GridPathfinder(1);
        var options = new GridPathOptions(maxExpandedNodes: 0);
        var tooSmall = finder.FindPath(grid, new(0, 0), new(0, 0), Span<GridPoint>.Empty, options);
        Assert.Equal(GridPathStatus.DestinationTooSmall, tooSmall.Status);
        Assert.Equal(1, tooSmall.RequiredPathLength);
        Assert.Equal((ulong)0, tooSmall.TotalCost);
        GridPoint[] path = [new(-1, -1)];
        var found = finder.FindPath(grid, new(0, 0), new(0, 0), path, options);
        Assert.Equal(GridPathStatus.Found, found.Status);
        Assert.Equal(0, found.ExpandedNodes);
        Assert.Equal(new(0, 0), path[0]);
    }

    [Fact]
    public void DestinationTooSmallReportsFullLengthAndCostWithoutWritingAPrefix()
    {
        var grid = new NavigationGrid(4, 1);
        GridPoint[] path = [new(-1, -1), new(-1, -1)];
        var result = new GridPathfinder(4).FindPath(grid, new(0, 0), new(3, 0), path);
        Assert.Equal(GridPathStatus.DestinationTooSmall, result.Status);
        Assert.Equal(4, result.RequiredPathLength);
        Assert.Equal(0, result.WrittenLength);
        Assert.Equal((ulong)3_000, result.TotalCost);
        Assert.All(path, point => Assert.Equal(new(-1, -1), point));
    }

    [Fact]
    public void ExpansionBudgetLeavesOutputUnchangedAndDoesNotPreventWorkspaceReuse()
    {
        var grid = new NavigationGrid(8, 8);
        var finder = new GridPathfinder(grid.CellCount);
        var path = Enumerable.Repeat(new GridPoint(-1, -1), grid.CellCount).ToArray();
        var result = finder.FindPath(grid, new(0, 0), new(7, 7), path,
            new(maxExpandedNodes: 2));
        Assert.Equal(GridPathStatus.ExpansionLimitReached, result.Status);
        Assert.Equal(2, result.ExpandedNodes);
        Assert.False(result.HasPath);
        Assert.Null(result.TotalCost);
        Assert.All(path, point => Assert.Equal(new(-1, -1), point));
        Assert.Equal(GridPathStatus.Found, finder.FindPath(grid, new(0, 0), new(7, 7), path).Status);
    }

    [Fact]
    public void ReachingTheGoalDoesNotConsumeAnExpansion()
    {
        var finder = new GridPathfinder(2);
        var grid = new NavigationGrid(2, 1);
        GridPoint[] path = new GridPoint[2];
        Assert.Equal(GridPathStatus.ExpansionLimitReached,
            finder.FindPath(grid, new(0, 0), new(1, 0), path, new(maxExpandedNodes: 0)).Status);
        var result = finder.FindPath(grid, new(0, 0), new(1, 0), path, new(maxExpandedNodes: 1));
        Assert.Equal(GridPathStatus.Found, result.Status);
        Assert.Equal(1, result.ExpandedNodes);
    }

    [Fact]
    public void CancellationIsAResultAndLeavesOutputUnchanged()
    {
        var finder = new GridPathfinder(4);
        var grid = new NavigationGrid(2, 2);
        using var canceled = new CancellationTokenSource();
        canceled.Cancel();
        var path = Enumerable.Repeat(new GridPoint(-1, -1), 4).ToArray();
        var result = finder.FindPath(grid, new(0, 0), new(1, 1), path, ct: canceled.Token);
        Assert.Equal(GridPathStatus.Canceled, result.Status);
        Assert.Null(result.TotalCost);
        Assert.All(path, point => Assert.Equal(new(-1, -1), point));
        Assert.Equal(GridPathStatus.Found, finder.FindPath(grid, new(0, 0), new(1, 1), path).Status);
    }

    [Fact]
    public void UnreachableRouteLeavesOutputUnchanged()
    {
        var grid = new NavigationGrid(3, 1, new ushort[] { 1, 0, 1 });
        GridPoint[] path = [new(-1, -1), new(-1, -1), new(-1, -1)];
        var result = new GridPathfinder(3).FindPath(grid, new(0, 0), new(2, 0), path);
        Assert.Equal(GridPathStatus.Unreachable, result.Status);
        Assert.Equal(1, result.ExpandedNodes);
        Assert.All(path, point => Assert.Equal(new(-1, -1), point));
    }

    [Fact]
    public void EqualCostTieBreakingIsDeterministicAcrossWorkspaceReuse()
    {
        var grid = new NavigationGrid(10, 10);
        var finder = new GridPathfinder(100);
        GridPoint[] first = new GridPoint[100];
        var expected = finder.FindPath(grid, new(0, 0), new(9, 9), first);
        for (int i = 0; i < 20; i++)
        {
            GridPoint[] next = new GridPoint[100];
            var result = finder.FindPath(grid, new(0, 0), new(9, 9), next);
            Assert.Equal(expected.TotalCost, result.TotalCost);
            Assert.Equal(first[..expected.WrittenLength], next[..result.WrittenLength]);
        }
    }

    [Fact]
    public void VisitStampWrapClearsOldVisits()
    {
        var finder = new GridPathfinder(9);
        GridPoint[] path = new GridPoint[9];
        finder.FindPath(new NavigationGrid(3, 3), new(0, 0), new(2, 2), path);
        typeof(GridPathfinder).GetField("_searchId", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(finder, uint.MaxValue);
        var result = finder.FindPath(new NavigationGrid(9, 1), new(0, 0), new(8, 0), path);
        Assert.Equal(GridPathStatus.Found, result.Status);
        Assert.Equal(9, result.WrittenLength);
        Assert.Equal((ulong)8_000, result.TotalCost);
    }

    [Fact]
    public void ConcurrentOwnershipIsRejectedRatherThanSharingMutableSearchState()
    {
        var finder = new GridPathfinder(4);
        var ownerFlag = typeof(GridPathfinder).GetField("_inUse", BindingFlags.Instance | BindingFlags.NonPublic)!;
        ownerFlag.SetValue(finder, 1);
        try
        {
            Assert.Throws<InvalidOperationException>(() => finder.FindPath(new NavigationGrid(2, 2),
                new(0, 0), new(1, 1), new GridPoint[4]));
        }
        finally { ownerFlag.SetValue(finder, 0); }
        Assert.Equal(GridPathStatus.Found,
            finder.FindPath(new NavigationGrid(2, 2), new(0, 0), new(1, 1), new GridPoint[4]).Status);
    }

    [Fact]
    public void IndependentWorkspacesCanShareOneImmutableMap()
    {
        var grid = new NavigationGrid(32, 32);
        Parallel.For(0, 8, worker =>
        {
            var finder = new GridPathfinder(grid.CellCount);
            GridPoint[] path = new GridPoint[grid.CellCount];
            for (int i = 0; i < 8; i++)
            {
                var result = finder.FindPath(grid, new(0, worker), new(31, 31 - worker), path);
                Assert.Equal(GridPathStatus.Found, result.Status);
                Assert.Equal((ulong)(31 + Math.Abs(31 - 2 * worker)) * 1000, result.TotalCost);
            }
        });
    }

    [Fact]
    public void ReusedQueriesHaveAnAllocationFreeSteadyState()
    {
        var grid = new NavigationGrid(16, 16);
        var finder = new GridPathfinder(grid.CellCount);
        GridPoint[] path = new GridPoint[grid.CellCount];
        MeasureQueries(finder, grid, path, out _);
        long minimumAllocated = long.MaxValue;
        for (int sample = 0; sample < 8; sample++)
        {
            long allocated = MeasureQueries(finder, grid, path, out ulong total);
            Assert.Equal(30UL * 30_000, total);
            minimumAllocated = Math.Min(minimumAllocated, allocated);
        }
        Assert.Equal(0, minimumAllocated);
    }

    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
    private static long MeasureQueries(GridPathfinder finder, NavigationGrid grid, GridPoint[] path, out ulong total)
    {
        total = 0;
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 30; i++)
            total += finder.FindPath(grid, new(0, 0), new(15, 15), path).TotalCost!.Value;
        return GC.GetAllocatedBytesForCurrentThread() - before;
    }

    [Fact]
    public void WeightedAStarMatchesIndependentDijkstraOnSeededMaps()
    {
        var random = new Random(20261029);
        var finder = new GridPathfinder(100);
        GridPoint[] path = new GridPoint[100];
        for (int sample = 0; sample < 200; sample++)
        {
            int width = random.Next(2, 10);
            int height = random.Next(2, 10);
            var costs = new ushort[width * height];
            for (int index = 0; index < costs.Length; index++)
                costs[index] = random.Next(4) == 0 ? (ushort)0 : (ushort)random.Next(1, 8);
            GridPoint start = new(random.Next(width), random.Next(height));
            GridPoint goal = new(random.Next(width), random.Next(height));
            var grid = new NavigationGrid(width, height, costs);
            GridPathOptions[] configurations = [default, new(GridMovement.EightWay), new(GridMovement.EightWay, true)];
            foreach (var options in configurations)
            {
                ulong? expected = Dijkstra(width, height, costs, start, goal, options);
                var result = finder.FindPath(grid, start, goal, path, options);
                Assert.Equal(expected.HasValue ? GridPathStatus.Found : GridPathStatus.Unreachable, result.Status);
                Assert.Equal(expected, result.TotalCost);
                if (expected.HasValue)
                    AssertValidRoute(grid, path.AsSpan(0, result.WrittenLength), start, goal, options, expected.Value);
            }
        }
    }

    private static void AssertValidRoute(NavigationGrid grid, ReadOnlySpan<GridPoint> route,
        GridPoint start, GridPoint goal, GridPathOptions options, ulong expectedCost)
    {
        Assert.Equal(start, route[0]);
        Assert.Equal(goal, route[^1]);
        ulong cost = 0;
        for (int i = 1; i < route.Length; i++)
        {
            var previous = route[i - 1];
            var current = route[i];
            Assert.True(grid.IsWalkable(current));
            int dx = Math.Abs(current.X - previous.X);
            int dy = Math.Abs(current.Y - previous.Y);
            Assert.InRange(dx, 0, 1);
            Assert.InRange(dy, 0, 1);
            Assert.InRange(dx + dy, 1, options.Movement == GridMovement.FourWay ? 1 : 2);
            if (dx == 1 && dy == 1 && !options.AllowCornerCutting)
            {
                Assert.True(grid.IsWalkable(new(previous.X, current.Y)));
                Assert.True(grid.IsWalkable(new(current.X, previous.Y)));
            }
            cost += (ulong)grid[current.X, current.Y] * (dx == 1 && dy == 1 ? 1414UL : 1000UL);
        }
        Assert.Equal(expectedCost, cost);
    }

    private static ulong? Dijkstra(int width, int height, ushort[] costs,
        GridPoint start, GridPoint goal, GridPathOptions options)
    {
        int startIndex = start.Y * width + start.X;
        int goalIndex = goal.Y * width + goal.X;
        if (costs[startIndex] == 0 || costs[goalIndex] == 0) return null;
        var distances = Enumerable.Repeat(ulong.MaxValue, costs.Length).ToArray();
        var visited = new bool[costs.Length];
        distances[startIndex] = 0;
        while (true)
        {
            int best = -1;
            for (int i = 0; i < costs.Length; i++)
                if (!visited[i] && distances[i] != ulong.MaxValue && (best < 0 || distances[i] < distances[best])) best = i;
            if (best < 0) return null;
            if (best == goalIndex) return distances[best];
            visited[best] = true;
            int x = best % width;
            int y = best / width;
            for (int dy = -1; dy <= 1; dy++)
            for (int dx = -1; dx <= 1; dx++)
            {
                bool diagonal = dx != 0 && dy != 0;
                if ((dx == 0 && dy == 0) || (diagonal && options.Movement == GridMovement.FourWay)) continue;
                int nx = x + dx;
                int ny = y + dy;
                if (nx < 0 || nx >= width || ny < 0 || ny >= height) continue;
                int neighbor = ny * width + nx;
                if (costs[neighbor] == 0 || visited[neighbor]) continue;
                if (diagonal && !options.AllowCornerCutting && (costs[y * width + nx] == 0 || costs[ny * width + x] == 0)) continue;
                ulong candidate = distances[best] + (ulong)costs[neighbor] * (diagonal ? 1414UL : 1000UL);
                if (candidate < distances[neighbor]) distances[neighbor] = candidate;
            }
        }
    }
}
