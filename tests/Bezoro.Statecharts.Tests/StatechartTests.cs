using Bezoro.Conditions;
using Bezoro.StateMachines;
using Xunit;

namespace Bezoro.Statecharts.Tests;

public sealed class StatechartTests
{
    private static StateId S(int id) => new(id);
    private static RegionId R(int id) => new(id);
    private static SignalId E(int id) => new(id);

    private static StatechartDefinition Character(params ChartTransition[] extras)
    {
        ChartState[] states = { new(R(0)), new(R(0)), new(R(2)), new(R(2)), new(R(3)), new(R(3)),
            new(R(1)), new(R(1)), new(R(4)), new(R(4)) };
        ChartRegion[] regions = { new(S(0)), new(S(6)), new(S(2), S(0)), new(S(4), S(1)), new(S(8), S(7)) };
        var transitions = new List<ChartTransition>
        {
            new(S(2), E(0), S(3)), new(S(0), E(1), S(1)), new(S(1), E(3), S(0)),
            new(S(6), E(2), S(8), ConditionExpression.Predicate(0).Compile()),
            new(S(6), E(2), S(9), ConditionExpression.Predicate(1).Compile()),
            new(S(7), E(4), S(6))
        };
        transitions.AddRange(extras);
        return new(states, regions, transitions.ToArray(), 6);
    }

    [Fact]
    public void WalkingAndGroundedDamage_RunInIndependentRegions()
    {
        var context = new LogContext();
        var runner = new StatechartRunner<LogContext>(Character(), new LogHandler());
        runner.Start(ref context);
        Assert.Equal(new[] { 0, 2, 6 }, runner.ActiveStates.ToArray().Select(x => x.Value));
        Assert.Equal(new[] { "+0", "+2", "+6" }, context.Events);
        StateActivation grounded = runner.GetActivation(S(0));
        runner.Dispatch(E(0), ref context);
        StateActivation walking = runner.GetActivation(S(3));
        context.Events.Clear();
        var result = runner.Dispatch(E(2), ref context);
        Assert.Equal(1, result.TransitionCount);
        Assert.Equal(new[] { "-6", "t3", "+7", "+8" }, context.Events);
        Assert.True(runner.IsActive(S(0)) && runner.IsActive(S(3)) && runner.IsActive(S(7)) && runner.IsActive(S(8)));
        Assert.Equal(grounded, runner.GetActivation(S(0)));
        Assert.Equal(walking, runner.GetActivation(S(3)));
        Assert.True(runner.IsActive(walking));
    }

    [Fact]
    public void AirborneDamage_SelectsKnockbackWithoutVisitingGroundedInitialChild()
    {
        var context = new LogContext();
        var runner = new StatechartRunner<LogContext>(Character(), new LogHandler());
        runner.Start(ref context);
        runner.Dispatch(E(0), ref context);
        context.Events.Clear();
        runner.Dispatch(E(1), ref context);
        Assert.Equal(new[] { "-3", "-0", "t1", "+1", "+4" }, context.Events);
        context.Events.Clear();
        runner.Dispatch(E(2), ref context);
        Assert.Equal(new[] { "-6", "t4", "+7", "+9" }, context.Events);
        Assert.True(runner.IsActive(S(1)) && runner.IsActive(S(4)) && runner.IsActive(S(9)));
        Assert.False(runner.IsActive(S(8)));
    }

    [Fact]
    public void EveryGuard_SeesTheConfigurationBeforeAnySelectedExit()
    {
        var def = Character(new ChartTransition(S(0), E(2), S(1)));
        var context = new LogContext();
        var runner = new StatechartRunner<LogContext>(def, new LogHandler());
        runner.Start(ref context);
        var result = runner.Dispatch(E(2), ref context);
        Assert.Equal(2, result.TransitionCount);
        Assert.True(runner.IsActive(S(1)));
        Assert.True(runner.IsActive(S(8))); // The reaction guard observed Grounded before the locomotion transition.
        Assert.False(runner.IsActive(S(9)));
    }

    private static StatechartDefinition Composite(ChartTransition transition) => new(
        new ChartState[] { new(R(0)), new(R(0)), new(R(1)), new(R(1)), new(R(2)), new(R(2)) },
        new ChartRegion[] { new(S(0)), new(S(2), S(0)), new(S(4), S(0)) }, new[] { transition }, 1);

    [Fact]
    public void LocalTransition_PreservesAncestorAndOtherChildRegions()
    {
        var context = new LogContext();
        var runner = new StatechartRunner<LogContext>(Composite(new(S(0), E(0), S(3), kind: ChartTransitionKind.Local)), new LogHandler());
        runner.Start(ref context);
        StateActivation parent = runner.GetActivation(S(0)), sibling = runner.GetActivation(S(4));
        context.Events.Clear();
        var result = runner.Dispatch(E(0), ref context);
        Assert.Equal(new[] { "-2", "t0", "+3" }, context.Events);
        Assert.Equal(1, result.ExitedCount);
        Assert.Equal(parent, runner.GetActivation(S(0)));
        Assert.Equal(sibling, runner.GetActivation(S(4)));
    }

    [Fact]
    public void ExternalAncestorToDescendant_ReentersTheAncestorAndAllItsRegions()
    {
        var context = new LogContext();
        var runner = new StatechartRunner<LogContext>(Composite(new(S(0), E(0), S(3))), new LogHandler());
        runner.Start(ref context);
        var old = runner.GetActivation(S(0));
        context.Events.Clear();
        var result = runner.Dispatch(E(0), ref context);
        Assert.Equal(new[] { "-4", "-2", "-0", "t0", "+0", "+3", "+4" }, context.Events);
        Assert.Equal(3, result.ExitedCount);
        Assert.False(runner.IsActive(old));
        Assert.NotEqual(old, runner.GetActivation(S(0)));
    }

    [Fact]
    public void ExternalDescendantToAncestor_ReentersTheAncestorInitialConfiguration()
    {
        var context = new LogContext();
        var runner = new StatechartRunner<LogContext>(Composite(new(S(2), E(0), S(0))), new LogHandler());
        runner.Start(ref context);
        var old = runner.GetActivation(S(4));
        context.Events.Clear();
        runner.Dispatch(E(0), ref context);
        Assert.Equal(new[] { "-4", "-2", "-0", "t0", "+0", "+2", "+4" }, context.Events);
        Assert.False(runner.IsActive(old));
    }

    [Fact]
    public void InternalTransition_HasOnlyAnEffectAndPreservesTokens()
    {
        var context = new LogContext();
        var runner = new StatechartRunner<LogContext>(Composite(new(S(2), E(0), S(2), kind: ChartTransitionKind.Internal)), new LogHandler());
        runner.Start(ref context);
        var token = runner.GetActivation(S(2));
        context.Events.Clear();
        var result = runner.Dispatch(E(0), ref context);
        Assert.Equal(new[] { "t0" }, context.Events);
        Assert.Equal(0, result.ExitedCount);
        Assert.Equal(0, result.EnteredCount);
        Assert.True(result.HasTransition);
        Assert.Equal(token, runner.GetActivation(S(2)));
    }

    [Theory]
    [InlineData(0, 3, ChartTransitionKind.Local)]
    [InlineData(0, 0, ChartTransitionKind.Internal)]
    public void ParentInternalAndChildTransitions_ConflictBySourceAncestry(int source, int target, ChartTransitionKind kind)
    {
        var baseDefinition = Composite(new(S(source), E(0), S(target), kind: kind));
        var def = new StatechartDefinition(baseDefinition.States, baseDefinition.Regions,
            new[] { baseDefinition.Transitions[0], new ChartTransition(S(2), E(0), S(3)) }, 1);
        int context = 0;
        var runner = new StatechartRunner<int>(def);
        runner.Start(ref context);
        Assert.Equal(1, runner.Dispatch(E(0), ref context).TransitionCount);
        Assert.True(runner.IsActive(S(3)));
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(1, false)]
    public void PriorityThenDepth_ResolveAncestorConflicts(int parentPriority, bool expectChild)
    {
        var baseDefinition = Composite(new(S(0), E(0), S(1), priority: parentPriority));
        var definition = new StatechartDefinition(baseDefinition.States, baseDefinition.Regions,
            new[] { baseDefinition.Transitions[0], new ChartTransition(S(2), E(0), S(3)) }, 1);
        int context = 0;
        var runner = new StatechartRunner<int>(definition);
        runner.Start(ref context);
        Assert.Equal(1, runner.Dispatch(E(0), ref context).TransitionCount);
        Assert.Equal(expectChild, runner.IsActive(S(3)));
        Assert.Equal(!expectChild, runner.IsActive(S(1)));
    }

    [Fact]
    public void FirstPassingGuard_UsesPriorityAndDeclarationOrderAndSkipsTheRest()
    {
        var context = new LogContext();
        var def = new StatechartDefinition(new ChartState[] { new(R(0)), new(R(0)), new(R(0)) },
            new ChartRegion[] { new(S(0)) }, new ChartTransition[]
            {
                new(S(0), E(0), S(1), ConditionExpression.Predicate(2).Compile()),
                new(S(0), E(0), S(2), ConditionExpression.Predicate(2).Compile(), 1),
                new(S(0), E(0), S(1), ConditionExpression.Predicate(999).Compile())
            }, 1);
        var runner = new StatechartRunner<LogContext>(def, new LogHandler());
        runner.Start(ref context);
        runner.Dispatch(E(0), ref context);
        Assert.True(runner.IsActive(S(2)));
        Assert.Equal(1, context.GuardCalls);
    }

    [Fact]
    public void Tokens_AreInvalidAfterSelfTransitionStopAndOtherOwner()
    {
        var definition = new StatechartDefinition(new ChartState[] { new(R(0)) }, new ChartRegion[] { new(S(0)) },
            new ChartTransition[] { new(S(0), E(0), S(0)) }, 1);
        int context = 0;
        var runner = new StatechartRunner<int>(definition);
        var other = new StatechartRunner<int>(definition);
        runner.Start(ref context); other.Start(ref context);
        var token = runner.GetActivation(S(0));
        Assert.False(other.IsActive(token));
        Assert.False(runner.IsActive(default(StateActivation)));
        runner.Dispatch(E(0), ref context);
        Assert.False(runner.IsActive(token));
        var second = runner.GetActivation(S(0));
        runner.Stop(ref context); runner.Stop(ref context);
        Assert.False(runner.IsActive(second));
        runner.Start(ref context);
        Assert.NotEqual(second, runner.GetActivation(S(0)));
        Assert.False(runner.TryGetActivation(S(100), out var missing));
        Assert.False(missing.IsValid);
    }

    [Fact]
    public void GuardException_PreservesEveryActivationAndRunningStatus()
    {
        var handler = new LogHandler { ThrowGuard = true };
        var context = new LogContext();
        var runner = new StatechartRunner<LogContext>(Character(), handler);
        runner.Start(ref context);
        var before = runner.ActiveStates.ToArray();
        var token = runner.GetActivation(S(6));
        context.Events.Clear();
        Assert.Throws<InvalidOperationException>(() => runner.Dispatch(E(2), ref context));
        Assert.Equal(before, runner.ActiveStates.ToArray());
        Assert.Equal(token, runner.GetActivation(S(6)));
        Assert.Empty(context.Events);
        Assert.True(runner.IsRunning);
        Assert.False(runner.IsFaulted);
    }

    [Fact]
    public void StartFailure_CleansOnlyEnteredStatesIncludingTheFailingEntry()
    {
        var handler = new LogHandler { ThrowEnter = 2 };
        var context = new LogContext();
        var runner = new StatechartRunner<LogContext>(Character(), handler);
        Assert.Throws<InvalidOperationException>(() => runner.Start(ref context));
        Assert.Equal(new[] { "+0", "+2", "-2", "-0" }, context.Events);
        Assert.Empty(runner.ActiveStates.ToArray());
        Assert.True(runner.IsFaulted);
        Assert.False(runner.IsRunning);
        Assert.Throws<InvalidOperationException>(() => runner.Start(ref context));
        handler.ThrowEnter = -1;
        runner.Reset(ref context);
        runner.Start(ref context);
        Assert.True(runner.IsRunning);
    }

    [Fact]
    public void FailedExit_IsNotRepeatedAndAllOtherStatesAreCleaned()
    {
        var handler = new LogHandler { ThrowExit = 2 };
        var context = new LogContext();
        var runner = new StatechartRunner<LogContext>(Character(), handler);
        runner.Start(ref context);
        context.Events.Clear();
        Assert.Throws<InvalidOperationException>(() => runner.Dispatch(E(0), ref context));
        Assert.Equal(new[] { "-2", "-6", "-0" }, context.Events);
        Assert.Empty(runner.ActiveStates.ToArray());
        Assert.True(runner.IsFaulted);
    }

    [Fact]
    public void CleanupFailures_AreAggregatedWithoutSkippingOtherExits()
    {
        var handler = new LogHandler { ThrowExit = 2, ThrowOtherExit = 6 };
        var context = new LogContext();
        var runner = new StatechartRunner<LogContext>(Character(), handler);
        runner.Start(ref context);
        context.Events.Clear();
        var failure = Assert.Throws<AggregateException>(() => runner.Dispatch(E(0), ref context));
        Assert.Equal(2, failure.InnerExceptions.Count);
        Assert.Equal(new[] { "-2", "-6", "-0" }, context.Events);
        Assert.Empty(runner.ActiveStates.ToArray());
    }

    [Fact]
    public void Stop_TriesAllExitsAndPropagatesFailure()
    {
        var handler = new LogHandler { ThrowExit = 2 };
        var context = new LogContext();
        var runner = new StatechartRunner<LogContext>(Character(), handler);
        runner.Start(ref context);
        context.Events.Clear();
        Assert.Throws<InvalidOperationException>(() => runner.Stop(ref context));
        Assert.Equal(new[] { "-6", "-2", "-0" }, context.Events);
        Assert.Empty(runner.ActiveStates.ToArray());
        Assert.True(runner.IsFaulted);
        runner.Stop(ref context);
    }

    [Fact]
    public void Reentrancy_IsRejectedAndLeavesNoPartiallyActiveStart()
    {
        var handler = new LogHandler();
        var context = new LogContext();
        var runner = new StatechartRunner<LogContext>(Character(), handler);
        handler.OnEntry = () => runner.Start(ref context);
        Assert.Throws<InvalidOperationException>(() => runner.Start(ref context));
        Assert.True(runner.IsFaulted);
        Assert.Empty(runner.ActiveStates.ToArray());
    }

    [Fact]
    public void InvalidSignalsAndLifecycleCalls_DoNotChangeConfiguration()
    {
        int context = 0;
        var runner = new StatechartRunner<int>(Character());
        Assert.Throws<InvalidOperationException>(() => runner.Dispatch(E(0), ref context));
        runner.Start(ref context);
        var before = runner.ActiveStates.ToArray();
        Assert.Throws<InvalidOperationException>(() => runner.Start(ref context));
        Assert.Throws<ArgumentOutOfRangeException>(() => runner.Dispatch(E(6), ref context));
        Assert.Equal(before, runner.ActiveStates.ToArray());
    }

    [Fact]
    public void UnusedStates_DoNotIncreaseRunnerStorageOrMaximumActivity()
    {
        var states = Enumerable.Repeat(new ChartState(R(0)), 100_000).ToArray();
        var small = new StatechartDefinition(states.AsSpan(0, 2), new ChartRegion[] { new(S(0)) },
            new ChartTransition[] { new(S(0), E(0), S(1)) }, 1);
        var large = new StatechartDefinition(states, new ChartRegion[] { new(S(0)) },
            new ChartTransition[] { new(S(0), E(0), S(99_999)) }, 1);
        Assert.Equal(1, small.MaximumActiveStates);
        Assert.Equal(1, large.MaximumActiveStates);
        _ = new StatechartRunner<int>(small); _ = new StatechartRunner<int>(large);
        long before = GC.GetAllocatedBytesForCurrentThread();
        var a = new StatechartRunner<int>(small);
        long smallBytes = GC.GetAllocatedBytesForCurrentThread() - before;
        before = GC.GetAllocatedBytesForCurrentThread();
        var b = new StatechartRunner<int>(large);
        long largeBytes = GC.GetAllocatedBytesForCurrentThread() - before;
        GC.KeepAlive(a); GC.KeepAlive(b);
        Assert.Equal(smallBytes, largeBytes);
    }

    [Fact]
    public void CharacterDispatchAndLifecycle_AllocateNoManagedMemoryAfterSetup()
    {
        var definition = Character();
        var runner = new StatechartRunner<Counts>(definition, new CountHandler());
        var context = new Counts();
        static void Run(StatechartRunner<Counts> r, ref Counts c)
        {
            r.Start(ref c); r.Dispatch(E(0), ref c); r.Dispatch(E(1), ref c);
            r.Dispatch(E(2), ref c); r.Dispatch(E(4), ref c); r.Dispatch(E(3), ref c); r.Stop(ref c);
        }
        for (int i = 0; i < 1_000; i++) Run(runner, ref context);
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 10_000; i++) Run(runner, ref context);
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
        Assert.True(context.Entries > 0 && context.Exits == context.Entries);
    }

    [Fact]
    public async Task SharedDefinition_SupportsIndependentConcurrentOwners()
    {
        var definition = Character();
        await Task.WhenAll(Enumerable.Range(0, 16).Select(_ => Task.Run(() =>
        {
            var runner = new StatechartRunner<Counts>(definition, new CountHandler());
            var context = new Counts();
            for (int i = 0; i < 1_000; i++)
            {
                runner.Start(ref context); runner.Dispatch(E(1), ref context); runner.Dispatch(E(2), ref context);
                Assert.True(runner.IsActive(S(9)));
                runner.Stop(ref context);
            }
        })));
    }

    [Fact]
    public void PreemptedParentGuard_IsNotEvaluated()
    {
        var baseDefinition = Composite(new(S(0), E(0), S(1), ConditionExpression.Predicate(999).Compile()));
        var definition = new StatechartDefinition(baseDefinition.States, baseDefinition.Regions,
            new[] { baseDefinition.Transitions[0], new ChartTransition(S(2), E(0), S(3)) }, 1);
        var context = new LogContext();
        var runner = new StatechartRunner<LogContext>(definition, new LogHandler());
        runner.Start(ref context);
        Assert.Equal(1, runner.Dispatch(E(0), ref context).TransitionCount);
        Assert.Equal(0, context.GuardCalls);
        Assert.True(runner.IsActive(S(3)));
    }

    [Fact]
    public void ExitedSourceGuard_IsNotEvaluatedAfterHigherPriorityAncestorWins()
    {
        var baseDefinition = Composite(new(S(0), E(0), S(1), priority: 10));
        var definition = new StatechartDefinition(baseDefinition.States, baseDefinition.Regions,
            new[] { baseDefinition.Transitions[0], new ChartTransition(S(2), E(0), S(3), ConditionExpression.Predicate(999).Compile()) }, 1);
        var context = new LogContext();
        var runner = new StatechartRunner<LogContext>(definition, new LogHandler());
        runner.Start(ref context);
        Assert.Equal(1, runner.Dispatch(E(0), ref context).TransitionCount);
        Assert.Equal(0, context.GuardCalls);
        Assert.True(runner.IsActive(S(1)));
    }

    [Theory]
    [InlineData(0, 10, true)]
    [InlineData(10, 0, false)]
    public void InternalTransition_ConflictsWithAnotherRegionsExitOfItsSource(int externalPriority, int internalPriority, bool internalWins)
    {
        var baseDefinition = Composite(new(S(2), E(0), S(1), priority: externalPriority));
        var definition = new StatechartDefinition(baseDefinition.States, baseDefinition.Regions,
            new[] { baseDefinition.Transitions[0], new ChartTransition(S(4), E(0), S(4), priority: internalPriority, kind: ChartTransitionKind.Internal) }, 1);
        var context = new LogContext();
        var runner = new StatechartRunner<LogContext>(definition, new LogHandler());
        runner.Start(ref context);
        context.Events.Clear();
        Assert.Equal(1, runner.Dispatch(E(0), ref context).TransitionCount);
        Assert.Equal(internalWins, runner.IsActive(S(4)));
        Assert.Equal(!internalWins, runner.IsActive(S(1)));
        Assert.Contains(internalWins ? "t1" : "t0", context.Events);
        Assert.DoesNotContain(internalWins ? "t0" : "t1", context.Events);
    }

    [Fact]
    public void InternalOnlyRunner_DoesNotReserveLifecyclePlans()
    {
        var definition = Composite(new(S(2), E(0), S(2), kind: ChartTransitionKind.Internal));
        Assert.False(definition.HasConfigurationTransitions);
        var runner = new StatechartRunner<int>(definition);
        foreach (string field in new[] { "_exitPlan", "_entryPlan", "_regionStack", "_targetPath" })
        {
            var value = (Array)typeof(StatechartRunner<int>).GetField(field,
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(runner)!;
            Assert.Empty(value);
        }
    }

    [Fact]
    public void ConstantShortCircuitedGuard_DoesNotRequireAHandler()
    {
        var guard = ConditionExpression.Any(ConditionExpression.True, ConditionExpression.Predicate(999)).Compile();
        var definition = new StatechartDefinition(new ChartState[] { new(R(0)) }, new ChartRegion[] { new(S(0)) },
            new ChartTransition[] { new(S(0), E(0), S(0), guard) }, 1);
        int context = 0;
        var runner = new StatechartRunner<int>(definition);
        runner.Start(ref context);
        Assert.True(runner.Dispatch(E(0), ref context).HasTransition);
    }

    [Fact]
    public void InternalEffectInAnUnrelatedRegion_CoexistsWithConfigurationTransition()
    {
        var context = new LogContext();
        var definition = Character(new ChartTransition(S(6), E(0), S(6), kind: ChartTransitionKind.Internal));
        var runner = new StatechartRunner<LogContext>(definition, new LogHandler());
        runner.Start(ref context);
        var token = runner.GetActivation(S(6));
        context.Events.Clear();
        var result = runner.Dispatch(E(0), ref context);
        Assert.Equal(2, result.TransitionCount);
        Assert.Equal(1, result.ExitedCount);
        Assert.Equal(1, result.EnteredCount);
        Assert.Equal(new[] { "-2", "t0", "t6", "+3" }, context.Events);
        Assert.Equal(token, runner.GetActivation(S(6)));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void StopAndFault_InvalidateAllTokensBeforeAnyExitCallback(bool fault)
    {
        var definition = new StatechartDefinition(
            new ChartState[] { new(R(0)), new(R(1)), new(R(2)) },
            new ChartRegion[] { new(S(0)), new(S(1)), new(S(2)) },
            new ChartTransition[] { new(S(0), E(0), S(0), kind: ChartTransitionKind.Internal) }, 1);
        var handler = new StopTokenObserver { ThrowTransition = fault };
        int context = 0;
        var runner = new StatechartRunner<int>(definition, handler);
        handler.Runner = runner;
        runner.Start(ref context);
        handler.OtherRegionToken = runner.GetActivation(S(0));
        Assert.True(runner.IsActive(handler.OtherRegionToken));
        if (fault)
            Assert.Throws<InvalidOperationException>(() => runner.Dispatch(E(0), ref context));
        else
            runner.Stop(ref context);
        Assert.True(handler.ObservedExit);
        Assert.True(handler.OtherRegionStillInConfiguration);
        Assert.False(handler.OtherRegionTokenStillLive);
        Assert.Empty(runner.ActiveStates.ToArray());
        Assert.False(runner.IsActive(handler.OtherRegionToken));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Dispatch_RevokesPendingExitsBeforeCancellationAndAcceptsNewReentryTokens(bool reenter)
    {
        var definition = new StatechartDefinition(
            new ChartState[] { new(R(0)), new(R(0)), new(R(1)), new(R(2)), new(R(3)) },
            new ChartRegion[] { new(S(0)), new(S(2), S(0)), new(S(3), S(0)), new(S(4)) },
            new ChartTransition[] { new(S(0), E(0), reenter ? S(0) : S(1)) }, 1);
        var handler = new DispatchTokenObserver();
        int context = 0;
        var runner = new StatechartRunner<int>(definition, handler);
        handler.Runner = runner;
        runner.Start(ref context);
        handler.PendingToken = runner.GetActivation(S(2));
        handler.UnaffectedToken = runner.GetActivation(S(4));
        handler.ObserveTransition = true;
        var result = runner.Dispatch(E(0), ref context);
        Assert.Equal(1, result.TransitionCount);
        Assert.Equal(1, handler.CancellationCallbacks);
        Assert.True(handler.PendingStateStillInConfiguration);
        Assert.False(handler.PendingTokenStillLive);
        Assert.True(handler.UnaffectedTokenStillLive);
        Assert.True(handler.AllNewEntriesLive);
        Assert.False(runner.IsFaulted);
        Assert.True(runner.IsRunning);
        Assert.False(runner.IsActive(handler.PendingToken));
        Assert.True(runner.IsActive(handler.UnaffectedToken));
        Assert.Equal(reenter, runner.IsActive(S(2)));
        if (reenter) Assert.NotEqual(handler.PendingToken, runner.GetActivation(S(2)));
    }

    private sealed class DispatchTokenObserver : StatechartHandler<int>
    {
        public StatechartRunner<int> Runner = null!;
        public StateActivation PendingToken, UnaffectedToken;
        public bool ObserveTransition;
        public int CancellationCallbacks;
        public bool PendingStateStillInConfiguration, PendingTokenStillLive, UnaffectedTokenStillLive;
        public bool AllNewEntriesLive = true;
        public override void OnEnter(StateId state, StateActivation activation, ref int context)
        {
            if (ObserveTransition) AllNewEntriesLive &= Runner.IsActive(activation);
        }
        public override void OnExit(StateId state, StateActivation activation, ref int context)
        {
            if (state != S(3)) return;
            CancellationCallbacks++;
            PendingStateStillInConfiguration = Runner.IsActive(S(2));
            PendingTokenStillLive = Runner.IsActive(PendingToken);
            UnaffectedTokenStillLive = Runner.IsActive(UnaffectedToken);
            // A canceled job may deliver its completion synchronously. Revoked tokens
            // must prevent that completion from attempting a reentrant signal dispatch.
            if (Runner.IsActive(PendingToken)) Runner.Dispatch(E(0), ref context);
        }
    }

    private sealed class StopTokenObserver : StatechartHandler<int>
    {
        public StatechartRunner<int> Runner = null!;
        public StateActivation OtherRegionToken;
        public bool ThrowTransition;
        public bool ObservedExit, OtherRegionStillInConfiguration, OtherRegionTokenStillLive;
        public override void OnTransition(int index, in ChartTransition transition, ref int context)
        {
            if (ThrowTransition) throw new InvalidOperationException("transition");
        }
        public override void OnExit(StateId state, StateActivation activation, ref int context)
        {
            if (state != S(2)) return;
            ObservedExit = true;
            OtherRegionStillInConfiguration = Runner.IsActive(S(0));
            OtherRegionTokenStillLive = Runner.IsActive(OtherRegionToken);
        }
    }

    private sealed class LogContext { public List<string> Events { get; } = new(); public int GuardCalls; }
    private sealed class LogHandler : StatechartHandler<LogContext>
    {
        public bool ThrowGuard;
        public int ThrowEnter = -1, ThrowExit = -1, ThrowOtherExit = -1;
        public Action? OnEntry;
        public override bool EvaluateGuard(int predicateId, StatechartView configuration, in LogContext context)
        {
            context.GuardCalls++;
            if (ThrowGuard) throw new InvalidOperationException("guard");
            return predicateId switch { 0 => configuration.IsActive(S(0)), 1 => configuration.IsActive(S(1)),
                2 => true, _ => throw new InvalidOperationException("Unnecessary guard was evaluated.") };
        }
        public override void OnEnter(StateId state, StateActivation activation, ref LogContext context)
        {
            context.Events.Add("+" + state.Value);
            OnEntry?.Invoke();
            if (ThrowEnter == state.Value) throw new InvalidOperationException("enter");
        }
        public override void OnExit(StateId state, StateActivation activation, ref LogContext context)
        {
            context.Events.Add("-" + state.Value);
            if (ThrowExit == state.Value || ThrowOtherExit == state.Value) throw new InvalidOperationException("exit" + state.Value);
        }
        public override void OnTransition(int index, in ChartTransition transition, ref LogContext context) => context.Events.Add("t" + index);
    }
    private struct Counts { public int Entries, Exits; }
    private sealed class CountHandler : StatechartHandler<Counts>
    {
        public override bool EvaluateGuard(int id, StatechartView config, in Counts context) => id == 0 ? config.IsActive(S(0)) : config.IsActive(S(1));
        public override void OnEnter(StateId state, StateActivation token, ref Counts context) => context.Entries++;
        public override void OnExit(StateId state, StateActivation token, ref Counts context) => context.Exits++;
    }
}
