using Bezoro.BehaviorTrees;
using Bezoro.Conditions;
using Bezoro.Statecharts;
using Bezoro.StateMachines;
using Xunit;

namespace Bezoro.Behaviors.Tests;

public sealed class CompositionTests
{
    private static readonly BehaviorCompositionLimits DeepLimits = new(64, 256);

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(4)]
    [InlineData(16)]
    [InlineData(32)]
    [InlineData(60)]
    public void NestedTrees_BudgetOne_ReachesTheActionInLinearQuanta(int depth)
    {
        var context = new Context();
        Behavior<Context> root = Leaf(0);
        for (int i = 1; i < depth; i++) root = Wrap(root);
        root.Start(ref context);
        int updates = 0;
        while (context.Ticks[0] == 0 && updates <= depth)
        {
            var budget = new BehaviorBudget(1);
            Assert.Equal(BehaviorStatus.Running, root.Tick(ref context, ref budget));
            Assert.InRange(budget.Consumed, 0, 1);
            updates++;
        }
        Assert.Equal(depth, updates);
        Assert.Equal(1, context.Ticks[0]);
        root.Stop(ref context);
        Assert.Equal(1, context.Aborts[0]);
    }

    [Fact]
    public void MixedNesting_BudgetOne_CompletesWithoutNewAllowances()
    {
        var context = new Context { Complete = true };
        Behavior<Context> behavior = Leaf(0);
        for (int i = 0; i < 5; i++)
        {
            behavior = new StateMachineBehavior<Context>(Flat(),
                new[] { new StateBehaviorBinding<Context>(new StateId(0), behavior, new SignalId(0)) },
                new[] { new StateId(1) }, limits: DeepLimits);
            behavior = Wrap(behavior);
            behavior = new StatechartBehavior<Context>(Chart(),
                bindings: new[] { new StateBehaviorBinding<Context>(new StateId(0), behavior, new SignalId(0)) },
                successStates: new[] { new StateId(1) }, limits: DeepLimits);
        }
        behavior.Start(ref context);
        int total = 0;
        for (int i = 0; i < 200 && behavior.Status == BehaviorStatus.Running; i++)
        {
            var budget = new BehaviorBudget(1);
            behavior.Tick(ref context, ref budget);
            Assert.InRange(budget.Consumed, 0, 1);
            total += budget.Consumed;
        }
        Assert.Equal(BehaviorStatus.Success, behavior.Status);
        Assert.Equal(1, context.Ticks[0]);
        Assert.InRange(total, 1, 100);
        behavior.Stop(ref context);
    }

    [Fact]
    public void Statechart_ExitDuringSuspension_StopsOldTreeAndRejectsLateCompletion()
    {
        var context = new Context();
        var handler = new Handler();
        var oldTree = new TreeBehavior<Context>(ActionDefinition(0), handler);
        var nextTree = Leaf(1);
        var machine = new StatechartBehavior<Context>(Chart(), bindings: new[]
        {
            new StateBehaviorBinding<Context>(new StateId(0), Wrap(oldTree)),
            new StateBehaviorBinding<Context>(new StateId(1), nextTree)
        });
        machine.Start(ref context);
        var oldState = machine.GetActivation(new StateId(0));
        var budget = new BehaviorBudget(1);
        machine.Tick(ref context, ref budget);
        Assert.True(machine.IsBudgetSuspended);
        machine.Dispatch(new SignalId(0), ref context);
        Assert.False(machine.IsActive(oldState));
        Assert.False(machine.TryDispatch(oldState, new SignalId(0), ref context));
        Assert.Equal(BehaviorStatus.Inactive, oldTree.Status);
        budget = new BehaviorBudget(10);
        machine.Tick(ref context, ref budget);
        Assert.Equal(0, context.Ticks[0]);
        Assert.Equal(1, context.Ticks[1]);
        machine.Stop(ref context);
    }

    [Fact]
    public void CompletionSignals_AreDeliveredOnceAndOldActivationIsDiscardedOnReentry()
    {
        var context = new Context { Complete = true };
        var tree = Leaf(0);
        var chart = new StatechartDefinition(
            new[] { new ChartState(new RegionId(0)), new ChartState(new RegionId(0)) },
            new[] { new ChartRegion(new StateId(0)) },
            new[] { new ChartTransition(new StateId(0), new SignalId(0), new StateId(1)),
                new ChartTransition(new StateId(0), new SignalId(1), new StateId(0)) }, 2);
        var machine = new StatechartBehavior<Context>(chart,
            bindings: new[] { new StateBehaviorBinding<Context>(new StateId(0), tree, new SignalId(0)) },
            successStates: new[] { new StateId(1) });
        machine.Start(ref context);
        var old = machine.GetActivation(new StateId(0));
        var one = new BehaviorBudget(1);
        machine.Tick(ref context, ref one);
        Assert.Equal(1, machine.PendingCompletionCount);
        machine.Dispatch(new SignalId(1), ref context);
        Assert.Equal(0, machine.PendingCompletionCount);
        Assert.False(machine.IsActive(old));
        one = new BehaviorBudget(1);
        machine.Tick(ref context, ref one);
        one = new BehaviorBudget(1);
        Assert.Equal(BehaviorStatus.Success, machine.Tick(ref context, ref one));
        Assert.Equal(2, context.Ticks[0]);
        int exits = context.Exits[0];
        one = new BehaviorBudget(100);
        Assert.Equal(BehaviorStatus.Success, machine.Tick(ref context, ref one));
        Assert.Equal(0, one.Consumed);
        machine.Stop(ref context);
        Assert.Equal(exits, context.Exits[0]);
    }

    [Fact]
    public void ParallelStateBehaviors_RoundRobinSurvivesCompletedRemoval()
    {
        var context = new Context();
        var machine = Parallel(4, allowSelfTransition: true);
        machine.Start(ref context);
        var one = new BehaviorBudget(1);
        machine.Tick(ref context, ref one); // A
        one = new BehaviorBudget(1);
        machine.Tick(ref context, ref one); // B
        one = new BehaviorBudget(1);
        machine.Tick(ref context, ref one); // C, next cursor points to D.
        machine.Dispatch(new SignalId(0), ref context); // Reenter A: active order B,C,D,A, starting at D.
        context.CompleteAction = 1;
        int[] before = (int[])context.Ticks.Clone();
        var four = new BehaviorBudget(4);
        machine.Tick(ref context, ref four); // D,A,B completes,C; a swapped visited A must not replace C's visit.
        for (int i = 0; i < 4; i++) Assert.Equal(before[i] + 1, context.Ticks[i]);
        machine.Stop(ref context);
    }

    [Fact]
    public void ParallelRunningChildren_YieldToAReactiveParentAfterACompletePhase()
    {
        var context = new Context { Allow = true };
        var machine = TwoParallel();
        var definition = new BehaviorTreeDefinition(BehaviorNode.ReactiveSequence(
            BehaviorNode.Condition(0), BehaviorNode.Action(1)));
        var root = new TreeBehavior<Context>(definition, new Handler(),
            new[] { new TreeBehaviorBinding<Context>(2, machine) });
        root.Start(ref context);
        for (int i = 0; i < 30 && context.Ticks[0] + context.Ticks[1] == 0; i++)
        { var budget = new BehaviorBudget(1); root.Tick(ref context, ref budget); }
        Assert.True(context.Ticks[0] + context.Ticks[1] > 0);
        context.Allow = false;
        for (int i = 0; i < 30 && root.Status == BehaviorStatus.Running; i++)
        { var budget = new BehaviorBudget(1); root.Tick(ref context, ref budget); }
        Assert.Equal(BehaviorStatus.Failure, root.Status);
        Assert.Equal(BehaviorStatus.Inactive, machine.Status);
        Assert.Equal(2, context.Aborts[0] + context.Aborts[1]);
        root.Stop(ref context);
    }

    [Fact]
    public void ConstructorFailure_DoesNotAdoptChildren_AndOwnedChildrenCannotBeDrivenDirectly()
    {
        var child = Leaf(0);
        Assert.Throws<ArgumentException>(() => new TreeBehavior<Context>(ActionDefinition(1), bindings: new[]
        { new TreeBehaviorBinding<Context>(0, child), new TreeBehaviorBinding<Context>(0, child) }));
        var context = new Context();
        child.Start(ref context);
        child.Stop(ref context);
        var parent = Wrap(child);
        Assert.Throws<InvalidOperationException>(() => child.Start(ref context));
        Assert.Throws<ArgumentException>(() => Wrap(child));
        parent.Start(ref context);
        parent.Stop(ref context);
    }

    [Fact]
    public void Limits_RejectTooDeepOrTooLargeGraphsWithoutClaimingTheChild()
    {
        Behavior<Context> child = Leaf(0);
        for (int i = 1; i < 8; i++) child = Wrap(child);
        Assert.Throws<ArgumentOutOfRangeException>(() => new TreeBehavior<Context>(ActionDefinition(0),
            bindings: new[] { new TreeBehaviorBinding<Context>(0, child) }, limits: new BehaviorCompositionLimits(8)));
        var context = new Context();
        child.Start(ref context);
        child.Stop(ref context);
        Assert.Throws<ArgumentOutOfRangeException>(() => new BehaviorCompositionLimits(65));
    }

    [Fact]
    public void InvalidSignal_PreservesTheStartedChildAndState()
    {
        var context = new Context();
        var child = Leaf(0);
        var chart = new StatechartBehavior<Context>(Chart(),
            bindings: new[] { new StateBehaviorBinding<Context>(new StateId(0), child) });
        chart.Start(ref context);
        var activation = chart.GetActivation(new StateId(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => chart.Dispatch(new SignalId(1), ref context));
        Assert.Equal(BehaviorStatus.Running, chart.Status);
        Assert.True(chart.IsActive(activation));
        Assert.Equal(BehaviorStatus.Running, child.Status);
        chart.Stop(ref context);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void FaultCleanup_StopsEachComponentExactlyOnce(bool failOnStart)
    {
        var child = new Probe(failOnStart, !failOnStart);
        var parent = new ProbeParent(child);
        var context = new Context();
        if (failOnStart) Assert.Throws<InvalidOperationException>(() => parent.Start(ref context));
        else
        {
            parent.Start(ref context);
            var budget = new BehaviorBudget(1);
            Assert.Throws<InvalidOperationException>(() => parent.Tick(ref context, ref budget));
        }
        Assert.Equal(1, child.Stops);
        Assert.Equal(BehaviorStatus.Faulted, parent.Status);
        parent.Stop(ref context);
        Assert.Equal(1, child.Stops);
    }

    [Fact]
    public void FlatMachine_UsesOnlyBoundStateAndLatchedFinalMapping()
    {
        var context = new Context { Complete = true };
        var child = Leaf(0);
        var machine = new StateMachineBehavior<Context>(Flat(),
            new[] { new StateBehaviorBinding<Context>(new StateId(0), child, new SignalId(0)) },
            new[] { new StateId(1) });
        machine.Start(ref context);
        ulong old = machine.ActivationVersion;
        var budget = new BehaviorBudget(2);
        Assert.Equal(BehaviorStatus.Success, machine.Tick(ref context, ref budget));
        Assert.Equal(new StateId(1), machine.CurrentState);
        Assert.False(machine.TryDispatch(old, new SignalId(0), ref context));
        Assert.Equal(BehaviorStatus.Inactive, child.Status);
        machine.Stop(ref context);
    }

    [Fact]
    public void WarmLifecycleAndTicks_AllocateNoManagedMemory()
    {
        var context = new Context();
        var machine = TwoParallel();
        for (int i = 0; i < 500; i++)
        { machine.Start(ref context); var budget = new BehaviorBudget(2); machine.Tick(ref context, ref budget); machine.Stop(ref context); }
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 10_000; i++)
        { machine.Start(ref context); var budget = new BehaviorBudget(2); machine.Tick(ref context, ref budget); machine.Stop(ref context); }
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
    }

    [Fact]
    public void AdapterStorage_DoesNotScaleWithInactiveDeclaredStatesOrWideUnvisitedBranches()
    {
        var smallChart = WideChart(1);
        var largeChart = WideChart(10_000);
        _ = new StatechartBehavior<Context>(smallChart);
        _ = new StatechartBehavior<Context>(largeChart);
        long before = GC.GetAllocatedBytesForCurrentThread();
        var small = new StatechartBehavior<Context>(smallChart);
        long smallBytes = GC.GetAllocatedBytesForCurrentThread() - before;
        before = GC.GetAllocatedBytesForCurrentThread();
        var large = new StatechartBehavior<Context>(largeChart);
        long largeBytes = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Equal(smallBytes, largeBytes);
        Assert.Empty(small.Bindings.ToArray());
        Assert.Equal(0, large.PendingCompletionCount);
        Assert.Equal(0, large.CompletionCapacity);

        var smallTree = new BehaviorTreeDefinition(BehaviorNode.Sequence(BehaviorNode.Action(0), BehaviorNode.Succeed));
        var branches = new BehaviorNode[10_000];
        Array.Fill(branches, BehaviorNode.Succeed);
        branches[0] = BehaviorNode.Action(0);
        var largeTree = new BehaviorTreeDefinition(BehaviorNode.Sequence(branches));
        var handler = new Handler();
        _ = new TreeBehavior<Context>(smallTree, handler);
        _ = new TreeBehavior<Context>(largeTree, handler);
        before = GC.GetAllocatedBytesForCurrentThread();
        _ = new TreeBehavior<Context>(smallTree, handler);
        smallBytes = GC.GetAllocatedBytesForCurrentThread() - before;
        before = GC.GetAllocatedBytesForCurrentThread();
        var wide = new TreeBehavior<Context>(largeTree, handler);
        largeBytes = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Equal(smallBytes, largeBytes);
        var context = new Context();
        wide.Start(ref context);
        var budget = new BehaviorBudget(100);
        wide.Tick(ref context, ref budget);
        Assert.Equal(1, context.Ticks[0]);
        Assert.True(budget.Consumed < 10);
        wide.Stop(ref context);
    }

    [Fact]
    public void FlatAdapter_SupportsSparseHugeStateRangesWithoutPerStateStorage()
    {
        var definition = new StateMachineDefinition(int.MaxValue, 0, Array.Empty<StateTransition>());
        var machine = new StateMachineBehavior<Context>(definition,
            new[] { new StateBehaviorBinding<Context>(new StateId(0), Leaf(0)) });
        var context = new Context();
        machine.Start(ref context);
        var budget = new BehaviorBudget(1);
        machine.Tick(ref context, ref budget);
        Assert.Equal(1, context.Ticks[0]);
        machine.Stop(ref context);
    }

    [Fact]
    public void ParallelFinalStates_FailureWinsAndDoNotTickChildren()
    {
        var definition = new StatechartDefinition(
            new[] { new ChartState(new RegionId(0)), new ChartState(new RegionId(1)) },
            new[] { new ChartRegion(new StateId(0)), new ChartRegion(new StateId(1)) },
            Array.Empty<ChartTransition>(), 0);
        var context = new Context();
        var machine = new StatechartBehavior<Context>(definition,
            successStates: new[] { new StateId(0) }, failureStates: new[] { new StateId(1) });
        machine.Start(ref context);
        var budget = new BehaviorBudget(0);
        Assert.Equal(BehaviorStatus.Failure, machine.Tick(ref context, ref budget));
        Assert.Equal(0, budget.Consumed);
        machine.Stop(ref context);
    }

    [Fact]
    public void CompletionLimit_DiscardsStaleRowsBeforeNewStateChildrenFinish()
    {
        // Each parent activates three regions, while A and B are mutually exclusive root states.
        var states = new[]
        {
            new ChartState(new RegionId(0)),
            new ChartState(new RegionId(1)), new ChartState(new RegionId(2)), new ChartState(new RegionId(3)),
            new ChartState(new RegionId(0)),
            new ChartState(new RegionId(4)), new ChartState(new RegionId(5)), new ChartState(new RegionId(6))
        };
        var regions = new[]
        {
            new ChartRegion(new StateId(0)),
            new ChartRegion(new StateId(1), new StateId(0)), new ChartRegion(new StateId(2), new StateId(0)),
            new ChartRegion(new StateId(3), new StateId(0)),
            new ChartRegion(new StateId(5), new StateId(4)), new ChartRegion(new StateId(6), new StateId(4)),
            new ChartRegion(new StateId(7), new StateId(4))
        };
        var chart = new StatechartDefinition(states, regions,
            new[] { new ChartTransition(new StateId(0), new SignalId(0), new StateId(4)) }, 1);
        var bindings = new StateBehaviorBinding<Context>[6];
        int[] stateIds = { 1, 2, 3, 5, 6, 7 };
        for (int i = 0; i < bindings.Length; i++)
            bindings[i] = new(new StateId(stateIds[i]), Leaf(i), new SignalId(0));
        var machine = new StatechartBehavior<Context>(chart, bindings: bindings, maximumCompletionsPerTick: 1);
        var context = new Context { Complete = true };
        machine.Start(ref context);
        var budget = new BehaviorBudget(3);
        machine.Tick(ref context, ref budget);
        Assert.Equal(3, machine.PendingCompletionCount);
        budget = new BehaviorBudget(4);
        machine.Tick(ref context, ref budget);
        Assert.True(machine.IsActive(new StateId(4)));
        Assert.Equal(3, machine.PendingCompletionCount);
        for (int i = 0; i < 6; i++) Assert.Equal(1, context.Ticks[i]);
        machine.Stop(ref context);
    }

    [Fact]
    public void GuardException_PreservesTheChartAndItsStartedChild()
    {
        var handler = new GuardHandler { Throw = true };
        var chart = new StatechartDefinition(
            new[] { new ChartState(new RegionId(0)), new ChartState(new RegionId(0)) },
            new[] { new ChartRegion(new StateId(0)) },
            new[] { new ChartTransition(new StateId(0), new SignalId(0), new StateId(1), ConditionExpression.Predicate(0).Compile()) }, 1);
        var child = Leaf(0);
        var machine = new StatechartBehavior<Context>(chart, handler,
            new[] { new StateBehaviorBinding<Context>(new StateId(0), child) });
        var context = new Context();
        machine.Start(ref context);
        var activation = machine.GetActivation(new StateId(0));
        Assert.Throws<InvalidOperationException>(() => machine.Dispatch(new SignalId(0), ref context));
        Assert.Equal(BehaviorStatus.Running, machine.Status);
        Assert.Equal(BehaviorStatus.Running, child.Status);
        Assert.True(machine.IsActive(activation));
        handler.Throw = false;
        machine.Dispatch(new SignalId(0), ref context);
        Assert.False(machine.IsActive(activation));
        machine.Stop(ref context);
    }

    [Fact]
    public void ReentrantHostAction_FaultsTheTreeAndAbortsItsActivationOnce()
    {
        var handler = new ReentrantHandler();
        var root = new TreeBehavior<Context>(ActionDefinition(0), handler);
        handler.Owner = root;
        var context = new Context();
        root.Start(ref context);
        var budget = new BehaviorBudget(4);
        Assert.Throws<InvalidOperationException>(() => root.Tick(ref context, ref budget));
        Assert.Equal(BehaviorStatus.Faulted, root.Status);
        Assert.Equal(1, context.Aborts[0]);
        root.Stop(ref context);
        Assert.Equal(1, context.Aborts[0]);
    }

    [Fact]
    public void CleanupFailure_AttemptsEveryStartedChildAndNeverRepeatsCallbacks()
    {
        var a = new ThrowingStop();
        var b = new ThrowingStop();
        var parent = new MultiParent(a, b);
        var context = new Context();
        parent.Start(ref context);
        AggregateException failure = Assert.Throws<AggregateException>(() => parent.Stop(ref context));
        Assert.Equal(2, failure.Flatten().InnerExceptions.Count);
        Assert.Equal(1, a.Stops);
        Assert.Equal(1, b.Stops);
        parent.Stop(ref context);
        Assert.Equal(1, a.Stops);
        Assert.Equal(1, b.Stops);
    }

    [Fact]
    public void ProtectedChildStorage_CopiesReferencesSoCallerMutationCannotCreateCycles()
    {
        var leaf = Leaf(0);
        var children = new Behavior<Context>[] { leaf };
        var parent = new MultiParent(children);
        children[0] = parent;
        Assert.Same(leaf, parent.Children[0]);
        var context = new Context();
        parent.Start(ref context);
        var budget = new BehaviorBudget(1);
        parent.Tick(ref context, ref budget);
        Assert.Equal(1, context.Ticks[0]);
        parent.Stop(ref context);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ProtectedChildStorage_AcceptsCovariantArraysIncludingEmptyArrays(bool empty)
    {
        Probe[] children = empty ? Array.Empty<Probe>() : new[] { new Probe(false, false) };
        var parent = new MultiParent(children);
        Assert.Equal(children.Length, parent.Children.Length);
        if (!empty) Assert.Same(children[0], parent.Children[0]);
        var context = new Context();
        parent.Start(ref context);
        parent.Stop(ref context);
    }

    [Fact]
    public void TerminalChart_ClosesRunningOrthogonalSiblingsBeforeReturning()
    {
        var chart = new StatechartDefinition(
            new[] { new ChartState(new RegionId(0)), new ChartState(new RegionId(0)), new ChartState(new RegionId(1)) },
            new[] { new ChartRegion(new StateId(0)), new ChartRegion(new StateId(2)) },
            new[] { new ChartTransition(new StateId(0), new SignalId(0), new StateId(1)) }, 1);
        var child = Leaf(0);
        var machine = new StatechartBehavior<Context>(chart,
            bindings: new[] { new StateBehaviorBinding<Context>(new StateId(2), child) },
            successStates: new[] { new StateId(1) });
        var context = new Context();
        machine.Start(ref context);
        var activation = machine.GetActivation(new StateId(2));
        var budget = new BehaviorBudget(1);
        machine.Tick(ref context, ref budget);
        machine.Dispatch(new SignalId(0), ref context);
        budget = new BehaviorBudget(0);
        Assert.Equal(BehaviorStatus.Success, machine.Tick(ref context, ref budget));
        Assert.Equal(BehaviorStatus.Inactive, child.Status);
        Assert.False(machine.IsActive(activation));
        Assert.Empty(machine.ActiveStates.ToArray());
        Assert.Equal(1, context.Aborts[0]);
        machine.Stop(ref context);
        Assert.Equal(1, context.Aborts[0]);
    }

    [Fact]
    public void TerminalCustomAndInitiallyFinalFlat_CloseTheirStartedResourcesOnce()
    {
        var resource = new Probe(false, false);
        var parent = new TerminalParent(resource);
        var context = new Context();
        parent.Start(ref context);
        var budget = new BehaviorBudget(1);
        Assert.Equal(BehaviorStatus.Success, parent.Tick(ref context, ref budget));
        Assert.Equal(1, resource.Stops);
        parent.Stop(ref context);
        Assert.Equal(1, resource.Stops);

        resource = new Probe(false, false);
        var flat = new StateMachineBehavior<Context>(Flat(),
            new[] { new StateBehaviorBinding<Context>(new StateId(0), resource) },
            new[] { new StateId(0) });
        flat.Start(ref context);
        budget = new BehaviorBudget(0);
        Assert.Equal(BehaviorStatus.Success, flat.Tick(ref context, ref budget));
        Assert.Equal(1, resource.Stops);
        Assert.Equal(new StateId(0), flat.CurrentState);
        flat.Stop(ref context);
        Assert.Equal(1, resource.Stops);
    }

    [Fact]
    public void TerminalCleanupFailure_FaultsParentAndClosesOtherSiblings()
    {
        var failing = new ThrowingStop();
        var other = new Probe(false, false);
        var root = new TerminalParent(failing, other);
        var context = new Context();
        root.Start(ref context);
        var budget = new BehaviorBudget(1);
        Assert.Throws<InvalidOperationException>(() => root.Tick(ref context, ref budget));
        Assert.Equal(BehaviorStatus.Faulted, root.Status);
        Assert.Equal(1, failing.Stops);
        Assert.Equal(1, other.Stops);
        root.Stop(ref context);
        Assert.Equal(1, failing.Stops);
        Assert.Equal(1, other.Stops);
    }

    [Fact]
    public void FailedChildStop_DoesNotLeaveTheChartTechnicallyFaultedOnReuse()
    {
        var child = new SwitchableStop();
        var chart = new StatechartBehavior<Context>(Chart(),
            bindings: new[] { new StateBehaviorBinding<Context>(new StateId(0), child) });
        var context = new Context();
        chart.Start(ref context);
        Assert.ThrowsAny<Exception>(() => chart.Stop(ref context));
        Assert.Equal(BehaviorStatus.Inactive, chart.Status);
        Assert.Equal(1, child.Stops);
        child.Throw = false;
        chart.Start(ref context);
        Assert.Equal(BehaviorStatus.Running, chart.Status);
        chart.Stop(ref context);
        Assert.Equal(2, child.Stops);
    }

    [Fact]
    public void FailedActionAbort_DoesNotLeaveTheTreeTechnicallyFaultedOnReuse()
    {
        var handler = new SwitchableAbortHandler();
        var root = new TreeBehavior<Context>(ActionDefinition(0), handler);
        var context = new Context();
        root.Start(ref context);
        var budget = new BehaviorBudget(1);
        root.Tick(ref context, ref budget);
        Assert.Throws<InvalidOperationException>(() => root.Stop(ref context));
        Assert.Equal(BehaviorStatus.Inactive, root.Status);
        Assert.Equal(1, context.Aborts[0]);
        handler.Throw = false;
        root.Start(ref context);
        budget = new BehaviorBudget(1);
        root.Tick(ref context, ref budget);
        root.Stop(ref context);
        Assert.Equal(2, context.Aborts[0]);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FlatExit_InvalidatesItsEntryBeforeSynchronousCancellationCallbacks(bool stop)
    {
        var child = new CancellationSignal();
        var machine = new StateMachineBehavior<Context>(Flat(),
            new[] { new StateBehaviorBinding<Context>(new StateId(0), child) });
        child.Owner = machine;
        var context = new Context();
        machine.Start(ref context);
        child.Version = machine.ActivationVersion;
        if (stop) machine.Stop(ref context);
        else machine.Dispatch(new SignalId(0), ref context);
        Assert.Equal(1, child.Cancellations);
        Assert.False(child.Accepted);
        Assert.Equal(stop ? BehaviorStatus.Inactive : BehaviorStatus.Running, machine.Status);
        if (!stop)
        {
            Assert.Equal(new StateId(1), machine.CurrentState);
            machine.Stop(ref context);
        }
    }

    [Fact]
    public void WholeGraphStop_RejectsLateResultsFromAStillUnwindingOtherRegion()
    {
        var action = new CapturingHandler();
        var tree = new TreeBehavior<Context>(ActionDefinition(0), action);
        var flat = new StateMachineBehavior<Context>(Flat(),
            new[] { new StateBehaviorBinding<Context>(new StateId(0), tree) });
        var handler = new CrossRegionCancellation { Flat = flat, Tree = tree, Action = action };
        var definition = new StatechartDefinition(
            new[] { new ChartState(new RegionId(0)), new ChartState(new RegionId(1)) },
            new[] { new ChartRegion(new StateId(0)), new ChartRegion(new StateId(1)) },
            new[] { new ChartTransition(new StateId(0), new SignalId(0), new StateId(0)) }, 1);
        var root = new StatechartBehavior<Context>(definition, handler,
            new[] { new StateBehaviorBinding<Context>(new StateId(0), flat) });
        handler.Owner = root;
        var context = new Context();
        root.Start(ref context);
        handler.OtherToken = root.GetActivation(new StateId(0));
        handler.FlatVersion = flat.ActivationVersion;
        var budget = new BehaviorBudget(1);
        root.Tick(ref context, ref budget);
        Assert.True(tree.IsActive(action.Activation));
        root.Stop(ref context);
        Assert.Equal(1, handler.Checks);
        Assert.False(handler.ChartAccepted);
        Assert.False(handler.FlatAccepted);
        Assert.False(handler.TreeTokenLive);
        Assert.Equal(1, context.Aborts[0]);
    }

    [Fact]
    public void AncestorExit_RevokesNestedScopesBeforeTheirPhysicalCancellationAndPreservesOtherRegions()
    {
        var aHandler = new CapturingHandler();
        var cHandler = new CapturingHandler();
        var treeA = new TreeBehavior<Context>(ActionDefinition(0), aHandler);
        var innerA = new StatechartBehavior<Context>(Chart(),
            bindings: new[] { new StateBehaviorBinding<Context>(new StateId(0), treeA) });
        var treeC = new TreeBehavior<Context>(ActionDefinition(2), cHandler);
        var probe = new AncestorCancellationProbe { InnerA = innerA, TreeA = treeA, TreeC = treeC,
            AHandler = aHandler, CHandler = cHandler };
        var treeB = new TreeBehavior<Context>(ActionDefinition(1), probe);
        var states = new[]
        {
            new ChartState(new RegionId(0)), new ChartState(new RegionId(1)),
            new ChartState(new RegionId(2)), new ChartState(new RegionId(0)), new ChartState(new RegionId(3))
        };
        var regions = new[]
        {
            new ChartRegion(new StateId(0)), new ChartRegion(new StateId(1), new StateId(0)),
            new ChartRegion(new StateId(2), new StateId(0)), new ChartRegion(new StateId(4))
        };
        var definition = new StatechartDefinition(states, regions,
            new[] { new ChartTransition(new StateId(0), new SignalId(0), new StateId(3)) }, 1);
        var root = new StatechartBehavior<Context>(definition, bindings: new[]
        {
            new StateBehaviorBinding<Context>(new StateId(1), innerA),
            new StateBehaviorBinding<Context>(new StateId(2), treeB),
            new StateBehaviorBinding<Context>(new StateId(4), treeC)
        });
        var context = new Context();
        root.Start(ref context);
        probe.TokenA = innerA.GetActivation(new StateId(0));
        var budget = new BehaviorBudget(3);
        root.Tick(ref context, ref budget);
        Assert.True(treeA.IsActive(aHandler.Activation));
        Assert.True(treeC.IsActive(cHandler.Activation));
        root.Dispatch(new SignalId(0), ref context);
        Assert.Equal(1, probe.Checks);
        Assert.False(probe.TreeALive);
        Assert.False(probe.InnerALive);
        Assert.False(probe.Accepted);
        Assert.True(probe.TreeCLive);
        Assert.Equal(BehaviorStatus.Running, root.Status);
        Assert.True(root.IsActive(new StateId(3)));
        root.Stop(ref context);
    }

    private static BehaviorTreeDefinition ActionDefinition(int action) => new(BehaviorNode.Action(action));
    private static TreeBehavior<Context> Leaf(int action) => new(ActionDefinition(action), new Handler());
    private static TreeBehavior<Context> Wrap(Behavior<Context> child) => new(ActionDefinition(10),
        bindings: new[] { new TreeBehaviorBinding<Context>(0, child) }, limits: DeepLimits);
    private static StateMachineDefinition Flat() => new(2, 1,
        new[] { new StateTransition(new StateId(0), new SignalId(0), new StateId(1)) });
    private static StatechartDefinition Chart() => new(
        new[] { new ChartState(new RegionId(0)), new ChartState(new RegionId(0)) },
        new[] { new ChartRegion(new StateId(0)) },
        new[] { new ChartTransition(new StateId(0), new SignalId(0), new StateId(1)) }, 1);
    private static StatechartBehavior<Context> TwoParallel() => Parallel(2);
    private static StatechartDefinition WideChart(int states)
    {
        var definitions = new ChartState[states];
        Array.Fill(definitions, new ChartState(new RegionId(0)));
        return new(definitions, new[] { new ChartRegion(new StateId(0)) }, Array.Empty<ChartTransition>(), 0);
    }
    private static StatechartBehavior<Context> Parallel(int count, bool allowSelfTransition = false)
    {
        var states = new ChartState[count];
        var regions = new ChartRegion[count];
        var bindings = new StateBehaviorBinding<Context>[count];
        for (int i = 0; i < count; i++)
        { states[i] = new(new RegionId(i)); regions[i] = new(new StateId(i)); bindings[i] = new(new StateId(i), Leaf(i)); }
        ChartTransition[] transitions = allowSelfTransition
            ? new[] { new ChartTransition(new StateId(0), new SignalId(0), new StateId(0)) }
            : Array.Empty<ChartTransition>();
        return new(new StatechartDefinition(states, regions, transitions, allowSelfTransition ? 1 : 0), bindings: bindings);
    }

    private sealed class Context
    {
        internal readonly int[] Ticks = new int[32];
        internal readonly int[] Enters = new int[32];
        internal readonly int[] Exits = new int[32];
        internal readonly int[] Aborts = new int[32];
        internal bool Complete;
        internal bool Allow;
        internal int CompleteAction = -1;
    }
    private sealed class Handler : IBehaviorTreeHandler<Context>
    {
        public bool Evaluate(int predicateId, in Context context) => context.Allow;
        public void Enter(int nodeId, int actionId, BehaviorActivation activation, ref Context context) => context.Enters[actionId]++;
        public BehaviorStatus Tick(int nodeId, int actionId, BehaviorActivation activation, ref Context context, ref BehaviorBudget budget)
        { context.Ticks[actionId]++; return context.Complete || context.CompleteAction == actionId ? BehaviorStatus.Success : BehaviorStatus.Running; }
        public void Exit(int nodeId, int actionId, BehaviorActivation activation, BehaviorStatus result, ref Context context) => context.Exits[actionId]++;
        public void Abort(int nodeId, int actionId, BehaviorActivation activation, ref Context context) => context.Aborts[actionId]++;
    }
    private sealed class Probe(bool failOnStart, bool failOnTick) : Behavior<Context>
    {
        internal int Stops;
        protected override void OnStart(ref Context context) { if (failOnStart) throw new InvalidOperationException("start"); }
        protected override BehaviorStatus OnTick(ref Context context, ref BehaviorBudget budget)
        { if (failOnTick) throw new InvalidOperationException("tick"); return BehaviorStatus.Running; }
        protected override void OnStop(ref Context context) => Stops++;
    }
    private sealed class ProbeParent : Behavior<Context>
    {
        private readonly Probe _child;
        internal ProbeParent(Probe child) : base(new Behavior<Context>[] { child }, default) => _child = child;
        protected override void OnStart(ref Context context) => StartChild(_child, ref context);
        protected override BehaviorStatus OnTick(ref Context context, ref BehaviorBudget budget) => TickChild(_child, ref context, ref budget);
        protected override void OnStop(ref Context context) { }
    }
    private sealed class GuardHandler : StatechartHandler<Context>
    {
        internal bool Throw;
        public override bool EvaluateGuard(int predicateId, StatechartView configuration, in Context context) =>
            Throw ? throw new InvalidOperationException("guard") : true;
    }
    private sealed class ReentrantHandler : IBehaviorTreeHandler<Context>
    {
        internal TreeBehavior<Context> Owner = null!;
        public bool Evaluate(int predicateId, in Context context) => true;
        public void Enter(int nodeId, int actionId, BehaviorActivation activation, ref Context context) { }
        public BehaviorStatus Tick(int nodeId, int actionId, BehaviorActivation activation, ref Context context, ref BehaviorBudget budget) =>
            Owner.Tick(ref context, ref budget);
        public void Exit(int nodeId, int actionId, BehaviorActivation activation, BehaviorStatus result, ref Context context) { }
        public void Abort(int nodeId, int actionId, BehaviorActivation activation, ref Context context) => context.Aborts[0]++;
    }
    private sealed class ThrowingStop : Behavior<Context>
    {
        internal int Stops;
        protected override void OnStart(ref Context context) { }
        protected override BehaviorStatus OnTick(ref Context context, ref BehaviorBudget budget) => BehaviorStatus.Running;
        protected override void OnStop(ref Context context) { Stops++; throw new InvalidOperationException("cleanup"); }
    }
    private sealed class MultiParent : Behavior<Context>
    {
        internal MultiParent(params Behavior<Context>[] children) : base(children, default) { }
        protected override void OnStart(ref Context context)
        { foreach (var child in Children) StartChild(child, ref context); }
        protected override BehaviorStatus OnTick(ref Context context, ref BehaviorBudget budget)
        { foreach (var child in Children) TickChild(child, ref context, ref budget); return BehaviorStatus.Running; }
        protected override void OnStop(ref Context context) { }
    }
    private sealed class TerminalParent : Behavior<Context>
    {
        internal TerminalParent(params Behavior<Context>[] children) : base(children, default) { }
        protected override void OnStart(ref Context context)
        { foreach (var child in Children) StartChild(child, ref context); }
        protected override BehaviorStatus OnTick(ref Context context, ref BehaviorBudget budget) => BehaviorStatus.Success;
        protected override void OnStop(ref Context context) { }
    }
    private sealed class SwitchableStop : Behavior<Context>
    {
        internal bool Throw = true;
        internal int Stops;
        protected override void OnStart(ref Context context) { }
        protected override BehaviorStatus OnTick(ref Context context, ref BehaviorBudget budget) => BehaviorStatus.Running;
        protected override void OnStop(ref Context context) { Stops++; if (Throw) throw new InvalidOperationException("cleanup"); }
    }
    private sealed class SwitchableAbortHandler : IBehaviorTreeHandler<Context>
    {
        internal bool Throw = true;
        public bool Evaluate(int predicateId, in Context context) => true;
        public void Enter(int nodeId, int actionId, BehaviorActivation activation, ref Context context) { }
        public BehaviorStatus Tick(int nodeId, int actionId, BehaviorActivation activation, ref Context context, ref BehaviorBudget budget) => BehaviorStatus.Running;
        public void Exit(int nodeId, int actionId, BehaviorActivation activation, BehaviorStatus result, ref Context context) { }
        public void Abort(int nodeId, int actionId, BehaviorActivation activation, ref Context context)
        { context.Aborts[0]++; if (Throw) throw new InvalidOperationException("abort"); }
    }
    private sealed class CancellationSignal : Behavior<Context>
    {
        internal StateMachineBehavior<Context> Owner = null!;
        internal ulong Version;
        internal int Cancellations;
        internal bool Accepted;
        protected override void OnStart(ref Context context) { }
        protected override BehaviorStatus OnTick(ref Context context, ref BehaviorBudget budget) => BehaviorStatus.Running;
        protected override void OnStop(ref Context context)
        { Cancellations++; Accepted = Owner.TryDispatch(Version, new SignalId(0), ref context); }
    }
    private sealed class CapturingHandler : IBehaviorTreeHandler<Context>
    {
        internal BehaviorActivation Activation;
        public bool Evaluate(int predicateId, in Context context) => true;
        public void Enter(int nodeId, int actionId, BehaviorActivation activation, ref Context context) => Activation = activation;
        public BehaviorStatus Tick(int nodeId, int actionId, BehaviorActivation activation, ref Context context, ref BehaviorBudget budget) => BehaviorStatus.Running;
        public void Exit(int nodeId, int actionId, BehaviorActivation activation, BehaviorStatus result, ref Context context) { }
        public void Abort(int nodeId, int actionId, BehaviorActivation activation, ref Context context) => context.Aborts[0]++;
    }
    private sealed class CrossRegionCancellation : StatechartHandler<Context>
    {
        internal StatechartBehavior<Context> Owner = null!;
        internal StateMachineBehavior<Context> Flat = null!;
        internal TreeBehavior<Context> Tree = null!;
        internal CapturingHandler Action = null!;
        internal StateActivation OtherToken;
        internal ulong FlatVersion;
        internal int Checks;
        internal bool ChartAccepted;
        internal bool FlatAccepted;
        internal bool TreeTokenLive;
        public override void OnExit(StateId state, StateActivation activation, ref Context context)
        {
            if (state.Value != 1) return;
            Checks++;
            ChartAccepted = Owner.TryDispatch(OtherToken, new SignalId(0), ref context);
            FlatAccepted = Flat.TryDispatch(FlatVersion, new SignalId(0), ref context);
            TreeTokenLive = Tree.IsActive(Action.Activation);
        }
    }
    private sealed class AncestorCancellationProbe : IBehaviorTreeHandler<Context>
    {
        internal StatechartBehavior<Context> InnerA = null!;
        internal TreeBehavior<Context> TreeA = null!;
        internal TreeBehavior<Context> TreeC = null!;
        internal CapturingHandler AHandler = null!;
        internal CapturingHandler CHandler = null!;
        internal StateActivation TokenA;
        internal int Checks;
        internal bool TreeALive;
        internal bool InnerALive;
        internal bool TreeCLive;
        internal bool Accepted;
        public bool Evaluate(int predicateId, in Context context) => true;
        public void Enter(int nodeId, int actionId, BehaviorActivation activation, ref Context context) { }
        public BehaviorStatus Tick(int nodeId, int actionId, BehaviorActivation activation, ref Context context, ref BehaviorBudget budget) => BehaviorStatus.Running;
        public void Exit(int nodeId, int actionId, BehaviorActivation activation, BehaviorStatus result, ref Context context) { }
        public void Abort(int nodeId, int actionId, BehaviorActivation activation, ref Context context)
        {
            Checks++;
            TreeALive = TreeA.IsActive(AHandler.Activation);
            InnerALive = InnerA.IsActive(TokenA);
            Accepted = InnerA.TryDispatch(TokenA, new SignalId(0), ref context);
            TreeCLive = TreeC.IsActive(CHandler.Activation);
        }
    }
}
