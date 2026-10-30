using System.Runtime.CompilerServices;
using Bezoro.StateMachines;
using Xunit;

namespace Bezoro.Statecharts.Tests;

public sealed class DefinitionTests
{
    private static StateId S(int x) => new(x);
    private static RegionId R(int x) => new(x);
    private static SignalId E(int x) => new(x);

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(4)] [InlineData(5)]
    [InlineData(6)] [InlineData(7)] [InlineData(8)] [InlineData(9)] [InlineData(10)] [InlineData(11)]
    public void InvalidHierarchiesAndTransitions_AreRejected(int problem)
    {
        ChartState[] states = { new(R(0)), new(R(0)) };
        ChartRegion[] regions = { new(S(0)) };
        ChartTransition[] transitions = Array.Empty<ChartTransition>();
        switch (problem)
        {
            case 0: states = Array.Empty<ChartState>(); break;
            case 1: regions = Array.Empty<ChartRegion>(); break;
            case 2: states[1] = new(R(5)); break;
            case 3: regions[0] = new(S(2)); break;
            case 4: regions = new[] { new ChartRegion(S(0)), new ChartRegion(S(0)) }; break;
            case 5: regions[0] = new(S(0), S(2)); break;
            case 6: regions[0] = new(S(0), S(0)); break;
            case 7: transitions = new[] { new ChartTransition(S(2), E(0), S(0)) }; break;
            case 8: transitions = new[] { new ChartTransition(S(0), E(2), S(0)) }; break;
            case 9: transitions = new[] { new ChartTransition(S(0), E(0), S(0), kind: (ChartTransitionKind)5) }; break;
            case 10: transitions = new[] { new ChartTransition(S(0), E(0), S(1), kind: ChartTransitionKind.Local) }; break;
            case 11: transitions = new[] { new ChartTransition(S(0), E(0), S(1), kind: ChartTransitionKind.Internal) }; break;
        }
        Assert.Throws<ArgumentException>(() => new StatechartDefinition(states, regions, transitions, 1));
    }

    [Fact]
    public void LocalSelfTransition_IsRejectedInFavorOfExplicitInternalOrExternal()
    {
        Assert.Throws<ArgumentException>(() => new StatechartDefinition(new ChartState[] { new(R(0)) },
            new ChartRegion[] { new(S(0)) }, new ChartTransition[] { new(S(0), E(0), S(0), kind: ChartTransitionKind.Local) }, 1));
    }

    [Fact]
    public void RootAndChildOrthogonalJumps_AreRejected()
    {
        ChartState[] roots = { new(R(0)), new(R(1)) };
        ChartRegion[] regions = { new(S(0)), new(S(1)) };
        Assert.Throws<ArgumentException>(() => new StatechartDefinition(roots, regions, new ChartTransition[] { new(S(0), E(0), S(1)) }, 1));
        ChartState[] nested = { new(R(0)), new(R(1)), new(R(2)) };
        ChartRegion[] childRegions = { new(S(0)), new(S(1), S(0)), new(S(2), S(0)) };
        Assert.Throws<ArgumentException>(() => new StatechartDefinition(nested, childRegions, new ChartTransition[] { new(S(1), E(0), S(2)) }, 1));
    }

    [Fact]
    public void AncestryCycle_IsRejectedEvenWhenAnotherRootRegionExists()
    {
        ChartState[] states = { new(R(0)), new(R(1)), new(R(2)) };
        ChartRegion[] regions = { new(S(0)), new(S(1), S(2)), new(S(2), S(1)) };
        Assert.Throws<ArgumentException>(() => new StatechartDefinition(states, regions, ReadOnlySpan<ChartTransition>.Empty, 0));
    }

    [Fact]
    public void NegativeCountsAndRegionIds_AreRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new RegionId(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new StatechartDefinition(new ChartState[] { new(R(0)) },
            new ChartRegion[] { new(S(0)) }, Array.Empty<ChartTransition>(), -1));
    }

    [Fact]
    public void Constructor_CopiesItsDeclarations()
    {
        ChartState[] states = { new(R(0)), new(R(0)) };
        ChartRegion[] regions = { new(S(0)) };
        ChartTransition[] transitions = { new(S(0), E(0), S(1)) };
        var definition = new StatechartDefinition(states, regions, transitions, 1);
        states[0] = new(R(99)); regions[0] = new(S(99)); transitions[0] = new(S(99), E(99), S(99));
        int context = 0;
        var runner = new StatechartRunner<int>(definition);
        runner.Start(ref context);
        Assert.True(runner.Dispatch(E(0), ref context).HasTransition);
        Assert.True(runner.IsActive(S(1)));
    }

    [Fact]
    public void PeakActivity_UsesMaxWithinARegionAndSumAcrossParallelRegions()
    {
        // Root state 0 has two regions: region1 peaks at 2 states, region2 at 1.
        ChartState[] states = { new(R(0)), new(R(0)), new(R(1)), new(R(1)), new(R(2)), new(R(3)) };
        ChartRegion[] regions = { new(S(0)), new(S(2), S(0)), new(S(4), S(0)), new(S(5), S(3)) };
        var definition = new StatechartDefinition(states, regions, Array.Empty<ChartTransition>(), 0);
        Assert.Equal(4, definition.MaximumActiveStates);
        Assert.Equal(3, definition.MaximumDepth);
        int context = 0;
        var runner = new StatechartRunner<int>(definition);
        runner.Start(ref context);
        Assert.Equal(new[] { 0, 2, 4 }, runner.ActiveStates.ToArray().Select(x => x.Value));
    }

    [Fact]
    public void DeepHierarchy_ConstructsAndRunsIteratively()
    {
        const int depth = 20_000;
        var states = new ChartState[depth];
        var regions = new ChartRegion[depth];
        for (int i = 0; i < depth; i++)
        {
            states[i] = new(R(i));
            regions[i] = new(S(i), i == 0 ? null : S(i - 1));
        }
        var definition = new StatechartDefinition(states, regions, Array.Empty<ChartTransition>(), 0);
        Assert.Equal(depth, definition.MaximumActiveStates);
        Assert.Equal(depth, definition.MaximumDepth);
        int context = 0;
        var runner = new StatechartRunner<int>(definition);
        runner.Start(ref context);
        Assert.Equal(depth, runner.ActiveStates.Length);
        Assert.True(runner.IsActive(S(depth - 1)));
        runner.Stop(ref context);
        Assert.Empty(runner.ActiveStates.ToArray());
    }

    [Fact]
    public void Layouts_KeepIdsAndActivationTokensCompact()
    {
        Assert.Equal(4, Unsafe.SizeOf<RegionId>());
        Assert.Equal(4, Unsafe.SizeOf<ChartState>());
        Assert.Equal(16, Unsafe.SizeOf<StateActivation>());
    }
}
