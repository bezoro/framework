using System.Runtime.CompilerServices;
using Bezoro.Conditions;
using Xunit;

namespace Bezoro.BehaviorTrees.Tests;

public sealed class BehaviorTreeTests
{
    [Fact]
    public void ValuesAndBudgetHaveCompactExplicitDefaults()
    {
        Assert.Equal(16, Unsafe.SizeOf<BehaviorActivation>());
        Assert.Equal(8, Unsafe.SizeOf<BehaviorBudget>());
        Assert.Equal(BehaviorStatus.Inactive, default);
        var zero = default(BehaviorBudget);
        Assert.False(zero.TryConsume());
        Assert.Equal(0, zero.Consumed);
        var budget = new BehaviorBudget(2);
        Assert.True(budget.TryConsume()); Assert.True(budget.TryConsume()); Assert.False(budget.TryConsume());
        Assert.Equal(2, budget.Consumed); Assert.Equal(0, budget.Remaining);
        Assert.Throws<ArgumentOutOfRangeException>(() => new BehaviorBudget(-1));
    }

    [Fact]
    public void DefinitionsValidateInputsAndExpandPerOccurrence()
    {
        Assert.Throws<ArgumentNullException>(() => new BehaviorTreeDefinition(null!));
        Assert.Throws<ArgumentOutOfRangeException>(() => new BehaviorTreeDefinition(BehaviorNode.Succeed, maximumNodes: 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new BehaviorTreeDefinition(BehaviorNode.Succeed, maximumDepth: 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => BehaviorNode.Action(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => BehaviorNode.Condition(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => BehaviorNode.Repeat(BehaviorNode.Succeed, 0));
        Assert.Throws<ArgumentNullException>(() => BehaviorNode.Condition((ConditionProgram)null!));
        Assert.Throws<ArgumentNullException>(() => BehaviorNode.Sequence(null!));
        Assert.Throws<ArgumentException>(() => BehaviorNode.Sequence(new BehaviorNode[] { null! }));
        var shared = BehaviorNode.Action(0);
        var children = new[] { shared, shared };
        var expression = BehaviorNode.Sequence(children);
        children[0] = BehaviorNode.Fail;
        var definition = new BehaviorTreeDefinition(expression);
        Assert.Equal(3, definition.NodeCount); Assert.Equal(2, definition.MaximumDepth);
        Assert.Equal(BehaviorNodeKind.Action, definition.GetNode(1).Kind);
        Assert.Equal(0, definition.GetNode(2).ActionId);
        Assert.Throws<ArgumentOutOfRangeException>(() => definition.GetNode(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => definition.GetNode(3));
        Assert.Throws<ArgumentException>(() => new BehaviorTreeDefinition(expression, maximumNodes: 2));
        Assert.Throws<ArgumentException>(() => new BehaviorTreeDefinition(expression, maximumDepth: 1));
    }

    [Theory]
    [InlineData(0, BehaviorStatus.Success)]
    [InlineData(1, BehaviorStatus.Failure)]
    [InlineData(2, BehaviorStatus.Success)]
    [InlineData(3, BehaviorStatus.Failure)]
    public void EmptyCompositesHaveConventionalResults(int kind, BehaviorStatus expected)
    {
        BehaviorNode node = kind switch { 0 => BehaviorNode.Sequence(), 1 => BehaviorNode.Selector(), 2 => BehaviorNode.ReactiveSequence(), _ => BehaviorNode.ReactiveSelector() };
        var runner = Run(node, out var context, out var handler);
        Assert.Equal(expected, Tick(runner, ref context, handler).Status);
        Assert.Empty(handler.Log);
    }

    [Fact]
    public void MemorySequenceDoesNotReevaluateCompletedConditionsOrActions()
    {
        var runner = Run(BehaviorNode.Sequence(BehaviorNode.Condition(0), BehaviorNode.Action(0), BehaviorNode.Action(1)), out var context, out var handler);
        context.Predicates[0] = true; context.Results[0] = BehaviorStatus.Success;
        Assert.Equal(BehaviorStatus.Running, Tick(runner, ref context, handler).Status);
        context.Predicates[0] = false;
        for (int i = 0; i < 5; i++) Assert.Equal(BehaviorStatus.Running, Tick(runner, ref context, handler).Status);
        Assert.Equal(1, handler.Predicates[0]); Assert.Equal(1, handler.Enters[0]); Assert.Equal(1, handler.Exits[0]);
        Assert.Equal(1, handler.Enters[1]); Assert.Equal(6, handler.Ticks[1]);
        context.Results[1] = BehaviorStatus.Success;
        Assert.Equal(BehaviorStatus.Success, Tick(runner, ref context, handler).Status);
        Assert.Equal(1, handler.Exits[1]);
        Assert.Equal(0, Tick(runner, ref context, handler).ConsumedVisits);
    }

    [Fact]
    public void MemorySelectorDoesNotRetryFailedHigherPriorityBranches()
    {
        var runner = Run(BehaviorNode.Selector(BehaviorNode.Condition(0), BehaviorNode.Action(1)), out var context, out var handler);
        Tick(runner, ref context, handler);
        context.Predicates[0] = true;
        Tick(runner, ref context, handler);
        Assert.Equal(1, handler.Predicates[0]); Assert.Equal(2, handler.Ticks[1]);
        runner.Stop(ref context, handler);
        Assert.Equal(1, handler.Aborts[1]);
    }

    [Fact]
    public void ReactiveSequenceRechecksGuardsButPreservesItsMemoryBranch()
    {
        var branch = BehaviorNode.Sequence(BehaviorNode.Action(0), BehaviorNode.Action(1));
        var runner = Run(BehaviorNode.ReactiveSequence(BehaviorNode.Condition(0), branch), out var context, out var handler);
        context.Predicates[0] = true; context.Results[0] = BehaviorStatus.Success;
        Tick(runner, ref context, handler); Tick(runner, ref context, handler);
        Assert.Equal(2, handler.Predicates[0]); Assert.Equal(1, handler.Enters[0]); Assert.Equal(1, handler.Ticks[0]);
        Assert.Equal(1, handler.Enters[1]); Assert.Equal(2, handler.Ticks[1]); Assert.Equal(0, handler.Aborts[1]);
        context.Predicates[0] = false;
        Assert.Equal(BehaviorStatus.Failure, Tick(runner, ref context, handler).Status);
        Assert.Equal(1, handler.Aborts[1]); Assert.False(runner.IsActive(handler.LastToken));
    }

    [Fact]
    public void ANewRepeatIterationDoesNotRestoreThePreviousReactiveContinuation()
    {
        var runner = Run(BehaviorNode.ReactiveSequence(BehaviorNode.Condition(0),
            BehaviorNode.Repeat(BehaviorNode.Sequence(BehaviorNode.Condition(1), BehaviorNode.Action(2)), 2)),
            out var context, out var handler);
        context.Predicates[0] = context.Predicates[1] = true;
        Assert.Equal(BehaviorStatus.Running, Tick(runner, ref context, handler).Status);
        context.Predicates[1] = false; context.Results[2] = BehaviorStatus.Success;
        Assert.Equal(BehaviorStatus.Failure, Tick(runner, ref context, handler).Status);
        Assert.Equal(2, handler.Predicates[1]); Assert.Equal(1, handler.Enters[2]); Assert.Equal(1, handler.Exits[2]);
        Assert.Equal(2, handler.Ticks[2]);
    }

    [Fact]
    public void ReactiveSelectorPreemptsBeforeEnteringANewAction()
    {
        var root = BehaviorNode.ReactiveSelector(
            BehaviorNode.Sequence(BehaviorNode.Condition(0), BehaviorNode.Action(0)), BehaviorNode.Action(1));
        var runner = Run(root, out var context, out var handler);
        Tick(runner, ref context, handler);
        BehaviorActivation old = handler.LastToken;
        context.Predicates[0] = true;
        Tick(runner, ref context, handler);
        Assert.Equal(1, handler.Aborts[1]); Assert.Equal(1, handler.Enters[0]);
        Assert.True(handler.Log.IndexOf("abort:1") < handler.Log.IndexOf("enter:0"));
        Assert.False(runner.IsActive(old)); Assert.True(runner.IsActive(handler.LastToken));
    }

    [Fact]
    public void ReactiveHigherPriorityActionAttemptResetsItsInterruptedFallback()
    {
        var root = BehaviorNode.ReactiveSelector(BehaviorNode.Sequence(BehaviorNode.Condition(0), BehaviorNode.Action(0)),
            BehaviorNode.Sequence(BehaviorNode.Action(1), BehaviorNode.Action(2)));
        var runner = Run(root, out var context, out var handler);
        context.Results[1] = BehaviorStatus.Success;
        Tick(runner, ref context, handler);
        context.Predicates[0] = true; context.Results[0] = BehaviorStatus.Failure;
        Tick(runner, ref context, handler);
        Assert.Equal(1, handler.Aborts[2]); Assert.Equal(2, handler.Enters[1]); Assert.Equal(2, handler.Enters[2]);
    }

    [Fact]
    public void MemoryAncestorsOutsideAReactiveRegionRemainCompleted()
    {
        var runner = Run(BehaviorNode.Sequence(BehaviorNode.Condition(0),
            BehaviorNode.ReactiveSequence(BehaviorNode.Condition(1), BehaviorNode.Action(0))), out var context, out var handler);
        context.Predicates[0] = context.Predicates[1] = true;
        Tick(runner, ref context, handler); Tick(runner, ref context, handler);
        Assert.Equal(1, handler.Predicates[0]); Assert.Equal(2, handler.Predicates[1]);
    }

    [Fact]
    public void MinimumBudgetMakesProgressThroughDeepMemoryPathsWithoutRevisitingAncestors()
    {
        BehaviorNode expression = BehaviorNode.Action(0);
        for (int i = 0; i < 1_000; i++) expression = BehaviorNode.Sequence(expression);
        var definition = new BehaviorTreeDefinition(expression, maximumDepth: 1_001);
        var runner = new BehaviorTreeRunner<Context>(definition);
        var context = new Context(); var handler = new Handler { Runner = runner }; runner.Start();
        int spent = 0;
        for (int i = 0; i < 1_000; i++)
        {
            var result = Tick(runner, ref context, handler, 1); spent += result.ConsumedVisits;
            Assert.True(result.BudgetExhausted); Assert.Equal(0, handler.Ticks[0]);
        }
        var leaf = Tick(runner, ref context, handler, 1); spent += leaf.ConsumedVisits;
        Assert.False(leaf.BudgetExhausted); Assert.Equal(1, handler.Ticks[0]); Assert.Equal(1_001, spent);
        Assert.Equal(1, Tick(runner, ref context, handler, 1).ConsumedVisits);
    }

    [Fact]
    public void MinimumBudgetAlsoCompletesReactiveReevaluationCycles()
    {
        var runner = Run(BehaviorNode.ReactiveSequence(BehaviorNode.Condition(0), BehaviorNode.Action(0)), out var context, out var handler);
        context.Predicates[0] = true;
        for (int i = 0; i < 30; i++) Tick(runner, ref context, handler, 1);
        Assert.Equal(10, handler.Ticks[0]); Assert.Equal(10, handler.Predicates[0]); Assert.Equal(1, handler.Enters[0]);
        Assert.Equal(0, handler.Aborts[0]);
    }

    [Fact]
    public void ZeroBudgetDoesNotSkipTheNextReactiveCheck()
    {
        var runner = Run(BehaviorNode.ReactiveSequence(BehaviorNode.Condition(0), BehaviorNode.Action(0)), out var context, out var handler);
        context.Predicates[0] = true; Tick(runner, ref context, handler);
        context.Predicates[0] = false;
        Assert.True(Tick(runner, ref context, handler, 0).BudgetExhausted);
        Assert.Equal(BehaviorStatus.Failure, Tick(runner, ref context, handler).Status);
        Assert.Equal(1, handler.Ticks[0]); Assert.Equal(1, handler.Aborts[0]);
    }

    [Fact]
    public void RepeatsAreFiniteAndChargeEachChildActivation()
    {
        var runner = Run(BehaviorNode.Repeat(BehaviorNode.Action(0), 7), out var context, out var handler);
        context.Results[0] = BehaviorStatus.Success;
        int spent = 0;
        for (int i = 0; runner.Status == BehaviorStatus.Running; i++)
        {
            Assert.True(i < 9);
            spent += Tick(runner, ref context, handler, 1).ConsumedVisits;
        }
        Assert.Equal(8, spent); Assert.Equal(7, handler.Enters[0]); Assert.Equal(7, handler.Exits[0]);
        Assert.Equal(7, handler.Tokens.Select(t => t.Serial).Distinct().Count());
    }

    [Fact]
    public void RepeatedSubtreesHaveIndependentOccurrenceIdsAndMemory()
    {
        var sub = new BehaviorTreeDefinition(BehaviorNode.Sequence(BehaviorNode.Condition(0), BehaviorNode.Action(0)));
        var runner = Run(BehaviorNode.Sequence(BehaviorNode.Subtree(sub), BehaviorNode.Subtree(sub)), out var context, out var handler);
        context.Predicates[0] = true;
        Tick(runner, ref context, handler);
        int firstNode = handler.LastNodeId;
        context.Results[0] = BehaviorStatus.Success;
        Assert.Equal(BehaviorStatus.Success, Tick(runner, ref context, handler).Status);
        Assert.Equal(2, handler.Predicates[0]); Assert.Equal(2, handler.Enters[0]);
        Assert.NotEqual(firstNode, handler.LastNodeId);
    }

    [Fact]
    public void ActivationTokensRejectForeignStaleAndCompletedScopes()
    {
        var runner = Run(BehaviorNode.Action(0), out var context, out var handler);
        Tick(runner, ref context, handler); var first = handler.LastToken;
        Assert.True(runner.IsActive(first)); Assert.False(runner.IsActive(default));
        var foreign = Run(BehaviorNode.Action(0), out var c2, out var h2); Tick(foreign, ref c2, h2);
        Assert.False(foreign.IsActive(first));
        runner.Stop(ref context, handler); runner.Stop(ref context, handler); Assert.Equal(1, handler.Aborts[0]);
        runner.Start(); Tick(runner, ref context, handler);
        Assert.False(runner.IsActive(first)); Assert.True(runner.IsActive(handler.LastToken));
        context.Results[0] = BehaviorStatus.Success;
        Tick(runner, ref context, handler); Assert.False(runner.IsActive(handler.LastToken));
        Assert.False(handler.SawLiveExitOrAbort);
    }

    [Fact]
    public void ExhaustedSerialsKeepTheCurrentActionAliveButRejectAnotherActivation()
    {
        var runner = Run(BehaviorNode.Action(0), out var context, out var handler);
        typeof(BehaviorTreeRunner<Context>).GetField("_serial", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .SetValue(runner, ulong.MaxValue - 1);
        Tick(runner, ref context, handler);
        Assert.Equal(ulong.MaxValue, handler.LastToken.Serial);
        Tick(runner, ref context, handler);
        Assert.Equal(1, handler.Enters[0]); Assert.True(runner.IsActive(handler.LastToken));
        runner.Restart(ref context, handler);
        Assert.Throws<InvalidOperationException>(() => Tick(runner, ref context, handler));
        Assert.Equal(1, handler.Enters[0]); Assert.Equal(BehaviorStatus.Faulted, runner.Status);
        Assert.Equal(-1, runner.ActiveNodeId);
    }

    [Theory]
    [InlineData("enter")]
    [InlineData("tick")]
    [InlineData("exit")]
    public void CallbackFailuresFaultAndCleanUpAtMostOnce(string phase)
    {
        var runner = Run(BehaviorNode.Action(0), out var context, out var handler);
        handler.ThrowPhase = phase; context.Results[0] = BehaviorStatus.Success;
        Assert.Throws<Failure>(() => Tick(runner, ref context, handler));
        Assert.Equal(BehaviorStatus.Faulted, runner.Status); Assert.False(runner.IsActive(handler.LastToken));
        Assert.Equal(phase == "exit" ? 0 : 1, handler.Aborts[0]);
        Assert.Throws<InvalidOperationException>(() => Tick(runner, ref context, handler));
        handler.ThrowPhase = null; runner.Stop(ref context, handler); runner.Start();
        Assert.Equal(BehaviorStatus.Success, Tick(runner, ref context, handler).Status);
    }

    [Fact]
    public void AbortFailureInvalidatesBeforeCallbackAndCanBeStoppedAgain()
    {
        var runner = Run(BehaviorNode.Action(0), out var context, out var handler);
        Tick(runner, ref context, handler); handler.ThrowPhase = "abort";
        Assert.Throws<Failure>(() => runner.Stop(ref context, handler));
        Assert.False(runner.IsActive(handler.LastToken)); Assert.Equal(BehaviorStatus.Faulted, runner.Status);
        runner.Stop(ref context, handler); Assert.Equal(1, handler.Aborts[0]); Assert.Equal(BehaviorStatus.Inactive, runner.Status);
    }

    [Fact]
    public void OriginalAndCleanupFailuresRemainVisible()
    {
        var runner = Run(BehaviorNode.Action(0), out var context, out var handler);
        handler.ThrowPhase = "tick"; handler.ThrowCleanup = true;
        var failure = Assert.Throws<AggregateException>(() => Tick(runner, ref context, handler));
        Assert.Equal(2, failure.InnerExceptions.Count); Assert.Equal(1, handler.Aborts[0]);
    }

    [Theory]
    [InlineData(BehaviorStatus.Inactive)]
    [InlineData(BehaviorStatus.Faulted)]
    [InlineData((BehaviorStatus)99)]
    public void InvalidActionResultsFaultAndAbort(BehaviorStatus status)
    {
        var runner = Run(BehaviorNode.Action(0), out var context, out var handler); context.Results[0] = status;
        Assert.Throws<InvalidOperationException>(() => Tick(runner, ref context, handler)); Assert.Equal(1, handler.Aborts[0]);
    }

    [Fact]
    public void ReentrancyIsRejectedAndOutstandingActionIsCleaned()
    {
        var runner = Run(BehaviorNode.Action(0), out var context, out var handler); handler.Reenter = true;
        Assert.Throws<InvalidOperationException>(() => Tick(runner, ref context, handler)); Assert.Equal(1, handler.Aborts[0]);
    }

    [Fact]
    public void GuardFailureAbortsTheOldReactiveAction()
    {
        var runner = Run(BehaviorNode.ReactiveSequence(BehaviorNode.Condition(0), BehaviorNode.Action(0)), out var context, out var handler);
        context.Predicates[0] = true; Tick(runner, ref context, handler); handler.ThrowPredicate = true;
        Assert.Throws<Failure>(() => Tick(runner, ref context, handler)); Assert.Equal(1, handler.Aborts[0]);
    }

    [Fact]
    public void CompiledConditionsShortCircuitAndOwnTheirScratch()
    {
        var program = ConditionExpression.Any(ConditionExpression.Predicate(0), ConditionExpression.Predicate(1)).Compile();
        var runner = Run(BehaviorNode.Condition(program), out var context, out var handler);
        context.Predicates[0] = true;
        Assert.Equal(BehaviorStatus.Success, Tick(runner, ref context, handler).Status);
        Assert.Equal(1, handler.Predicates[0]); Assert.Equal(0, handler.Predicates[1]);
    }

    [Fact]
    public void ReusedSubtreesKeepTheirCompiledConditionPrograms()
    {
        var program = ConditionExpression.All(ConditionExpression.Predicate(0), ConditionExpression.Predicate(1)).Compile();
        var source = new BehaviorTreeDefinition(BehaviorNode.Condition(program));
        var runner = Run(BehaviorNode.Sequence(BehaviorNode.Subtree(source), BehaviorNode.Subtree(source)), out var context, out var handler);
        context.Predicates[0] = context.Predicates[1] = true;
        Assert.Equal(program.RequiredStackSize, runner.Definition.RequiredConditionStackSize);
        Assert.Equal(BehaviorStatus.Success, Tick(runner, ref context, handler).Status);
        Assert.Equal(2, handler.Predicates[0]); Assert.Equal(2, handler.Predicates[1]);
        Assert.Equal(-1, runner.Definition.GetNode(2).Parameter);
    }

    [Fact]
    public void PerRunnerStorageDependsOnDepthRatherThanWideInactiveSiblings()
    {
        var shallow = new BehaviorTreeDefinition(BehaviorNode.Sequence(BehaviorNode.Action(0)));
        var wide = new BehaviorTreeDefinition(BehaviorNode.Sequence(Enumerable.Repeat(BehaviorNode.Action(0), 10_000).ToArray()));
        _ = new BehaviorTreeRunner<Context>(shallow);
        long before = GC.GetAllocatedBytesForCurrentThread(); var one = new BehaviorTreeRunner<Context>(shallow);
        long first = GC.GetAllocatedBytesForCurrentThread() - before;
        before = GC.GetAllocatedBytesForCurrentThread(); var many = new BehaviorTreeRunner<Context>(wide);
        long second = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Equal(first, second); Assert.Equal(2, many.FrameCapacity); GC.KeepAlive(one); GC.KeepAlive(many);
    }

    [Fact]
    public void AShallowReactiveBranchDoesNotReserveSnapshotsForADeepMemorySibling()
    {
        BehaviorNode deep = BehaviorNode.Action(1);
        for (int i = 0; i < 100; i++) deep = BehaviorNode.Sequence(deep);
        var root = BehaviorNode.Selector(BehaviorNode.ReactiveSequence(BehaviorNode.Condition(0), BehaviorNode.Action(0)), deep);
        var runner = Run(root, out var context, out var handler);
        Assert.Equal(102, runner.FrameCapacity);
        Assert.Equal(3, runner.Definition.MaximumReactiveDepth);
        Assert.Equal(3, runner.ReactiveFrameCapacity);
        context.Predicates[0] = true;
        Tick(runner, ref context, handler); Tick(runner, ref context, handler);
        context.Predicates[0] = false;
        Tick(runner, ref context, handler, 500);
        Assert.Equal(1, handler.Aborts[0]); Assert.Equal(1, handler.Enters[1]);
        Tick(runner, ref context, handler);
        context.Results[1] = BehaviorStatus.Success;
        Assert.Equal(BehaviorStatus.Success, Tick(runner, ref context, handler).Status);
        runner.Restart(ref context, handler); context.Predicates[0] = true;
        Tick(runner, ref context, handler); Tick(runner, ref context, handler);
        Assert.Equal(2, handler.Enters[0]);
        var plain = new BehaviorTreeRunner<Context>(new BehaviorTreeDefinition(deep));
        Assert.Equal(0, plain.ReactiveFrameCapacity);
    }

    [Fact]
    public void DeepPureReactiveChecksNeedOnlyTheShallowActionsSnapshot()
    {
        BehaviorNode deep = BehaviorNode.Condition(0);
        for (int i = 0; i < 100; i++) deep = BehaviorNode.Sequence(deep);
        var runner = Run(BehaviorNode.ReactiveSelector(deep, BehaviorNode.Action(0)), out var context, out var handler);
        Assert.Equal(2, runner.ReactiveFrameCapacity);
        Tick(runner, ref context, handler, 500);
        for (int i = 0; i < 300; i++) Tick(runner, ref context, handler, 1);
        Assert.True(handler.Ticks[0] >= 3); Assert.Equal(1, handler.Enters[0]); Assert.Equal(0, handler.Aborts[0]);
        var pure = new BehaviorTreeRunner<Context>(new BehaviorTreeDefinition(BehaviorNode.ReactiveSequence(deep)));
        Assert.True(pure.Definition.HasReactiveNodes); Assert.Equal(0, pure.ReactiveFrameCapacity);
    }

    [Fact]
    public void IndependentRunnersCanShareOneDefinitionAndExecuteConcurrently()
    {
        var definition = new BehaviorTreeDefinition(BehaviorNode.Sequence(BehaviorNode.Condition(0), BehaviorNode.Action(0)));
        Parallel.For(0, 32, _ =>
        {
            var runner = new BehaviorTreeRunner<Context>(definition); var context = new Context(); var handler = new Handler { Runner = runner };
            context.Predicates[0] = true; context.Results[0] = BehaviorStatus.Success;
            for (int i = 0; i < 100; i++) { runner.Start(); Tick(runner, ref context, handler); runner.Stop(ref context, handler); }
            Assert.Equal(100, handler.Exits[0]);
        });
    }

    [Fact]
    public void StructHandlersAndWarmTicksAllocateNoManagedMemory()
    {
        var runner = new BehaviorTreeRunner<int>(new BehaviorTreeDefinition(BehaviorNode.ReactiveSequence(BehaviorNode.Condition(0), BehaviorNode.Action(0))));
        var handler = new ValueHandler(); int context = 0; runner.Start();
        for (int i = 0; i < 1_000; i++) { var b = new BehaviorBudget(10); runner.Tick(ref context, ref handler, ref b); }
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 100_000; i++) { var b = new BehaviorBudget(10); runner.Tick(ref context, ref handler, ref b); }
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
        Assert.True(context > 0);
    }

    [Theory]
    [InlineData(817)]
    [InlineData(91003)]
    [InlineData(14437)]
    [InlineData(918751)]
    public void BoundedTerminalTreesMatchAnIndependentInterpreterWithRandomBudgets(int seed)
    {
        var random = new Random(seed);
        for (int sample = 0; sample < 300; sample++)
        {
            Spec spec = Generate(random, 5);
            var runner = Run(Build(spec), out var context, out var handler);
            for (int i = 0; i < 8; i++)
            { context.Predicates[i] = random.Next(2) != 0; context.Results[i] = random.Next(2) == 0 ? BehaviorStatus.Success : BehaviorStatus.Failure; }
            var expectedLog = new List<string>();
            int expectedVisits = 0;
            bool expected = Interpret(spec, context, expectedLog, ref expectedVisits);
            int actualVisits = 0, updates = 0;
            while (runner.Status == BehaviorStatus.Running)
            {
                actualVisits += Tick(runner, ref context, handler, random.Next(1, 5)).ConsumedVisits;
                Assert.True(++updates <= expectedVisits + 1);
            }
            Assert.Equal(expected ? BehaviorStatus.Success : BehaviorStatus.Failure, runner.Status);
            Assert.Equal(expectedVisits, actualVisits);
            Assert.Equal(expectedLog, handler.Log.Where(item => item.StartsWith("condition:", StringComparison.Ordinal) || item.StartsWith("tick:", StringComparison.Ordinal)).ToList());
            Assert.Equal(handler.Enters.Sum(), handler.Exits.Sum());
            Assert.Equal(0, handler.Aborts.Sum());
        }
    }

    [Fact]
    public void InversionPreservesRunningAndFiniteRepeatEndsAtTheFirstFailure()
    {
        var runner = Run(BehaviorNode.Invert(BehaviorNode.Action(0)), out var context, out var handler);
        Assert.Equal(BehaviorStatus.Running, Tick(runner, ref context, handler).Status);
        context.Results[0] = BehaviorStatus.Success;
        Assert.Equal(BehaviorStatus.Failure, Tick(runner, ref context, handler).Status);
        var repeated = Run(BehaviorNode.Repeat(BehaviorNode.Action(0), 100), out context, out handler);
        context.Results[0] = BehaviorStatus.Failure;
        Assert.Equal(BehaviorStatus.Failure, Tick(repeated, ref context, handler).Status);
        Assert.Equal(1, handler.Ticks[0]);
    }

    [Fact]
    public void NoTicksVisitInactiveSiblingsBeyondARunningAction()
    {
        var children = new BehaviorNode[10_000];
        children[0] = BehaviorNode.Action(0);
        Array.Fill(children, BehaviorNode.Condition(1), 1, children.Length - 1);
        var runner = Run(BehaviorNode.Sequence(children), out var context, out var handler);
        for (int i = 0; i < 100; i++) Tick(runner, ref context, handler);
        Assert.Equal(100, handler.Ticks[0]); Assert.Equal(0, handler.Predicates[1]);
    }

    private static Spec Generate(Random random, int depth)
    {
        int kind = random.Next(depth == 0 ? 4 : 11);
        return kind switch
        {
            0 => new(BehaviorNodeKind.Action, random.Next(8), Array.Empty<Spec>()),
            1 => new(BehaviorNodeKind.Condition, random.Next(8), Array.Empty<Spec>()),
            2 => new(BehaviorNodeKind.Succeed, 0, Array.Empty<Spec>()),
            3 => new(BehaviorNodeKind.Fail, 0, Array.Empty<Spec>()),
            4 => new(BehaviorNodeKind.Sequence, 0, Enumerable.Range(0, random.Next(4)).Select(_ => Generate(random, depth - 1)).ToArray()),
            5 => new(BehaviorNodeKind.Selector, 0, Enumerable.Range(0, random.Next(4)).Select(_ => Generate(random, depth - 1)).ToArray()),
            6 => new(BehaviorNodeKind.ReactiveSequence, 0, Enumerable.Range(0, random.Next(4)).Select(_ => Generate(random, depth - 1)).ToArray()),
            7 => new(BehaviorNodeKind.ReactiveSelector, 0, Enumerable.Range(0, random.Next(4)).Select(_ => Generate(random, depth - 1)).ToArray()),
            8 => new(BehaviorNodeKind.Invert, 0, new[] { Generate(random, depth - 1) }),
            9 => new(BehaviorNodeKind.Repeat, random.Next(1, 5), new[] { Generate(random, depth - 1) }),
            _ => new(BehaviorNodeKind.Subtree, 0, new[] { Generate(random, depth - 1) })
        };
    }
    private static BehaviorNode Build(Spec spec) => spec.Kind switch
    {
        BehaviorNodeKind.Action => BehaviorNode.Action(spec.Parameter),
        BehaviorNodeKind.Condition => BehaviorNode.Condition(spec.Parameter),
        BehaviorNodeKind.Succeed => BehaviorNode.Succeed,
        BehaviorNodeKind.Fail => BehaviorNode.Fail,
        BehaviorNodeKind.Sequence => BehaviorNode.Sequence(spec.Children.Select(Build).ToArray()),
        BehaviorNodeKind.Selector => BehaviorNode.Selector(spec.Children.Select(Build).ToArray()),
        BehaviorNodeKind.ReactiveSequence => BehaviorNode.ReactiveSequence(spec.Children.Select(Build).ToArray()),
        BehaviorNodeKind.ReactiveSelector => BehaviorNode.ReactiveSelector(spec.Children.Select(Build).ToArray()),
        BehaviorNodeKind.Invert => BehaviorNode.Invert(Build(spec.Children[0])),
        BehaviorNodeKind.Repeat => BehaviorNode.Repeat(Build(spec.Children[0]), spec.Parameter),
        _ => BehaviorNode.Subtree(new BehaviorTreeDefinition(Build(spec.Children[0])))
    };
    private static bool Interpret(Spec spec, Context context, List<string> log, ref int visits)
    {
        visits++;
        switch (spec.Kind)
        {
            case BehaviorNodeKind.Action: log.Add("tick:" + spec.Parameter); return context.Results[spec.Parameter] == BehaviorStatus.Success;
            case BehaviorNodeKind.Condition: log.Add("condition:" + spec.Parameter); return context.Predicates[spec.Parameter];
            case BehaviorNodeKind.Succeed: return true;
            case BehaviorNodeKind.Fail: return false;
            case BehaviorNodeKind.Sequence:
            case BehaviorNodeKind.ReactiveSequence:
                foreach (var child in spec.Children) if (!Interpret(child, context, log, ref visits)) return false;
                return true;
            case BehaviorNodeKind.Selector:
            case BehaviorNodeKind.ReactiveSelector:
                foreach (var child in spec.Children) if (Interpret(child, context, log, ref visits)) return true;
                return false;
            case BehaviorNodeKind.Invert: return !Interpret(spec.Children[0], context, log, ref visits);
            case BehaviorNodeKind.Repeat:
                for (int iteration = 0; iteration < spec.Parameter; iteration++) if (!Interpret(spec.Children[0], context, log, ref visits)) return false;
                return true;
            default: return Interpret(spec.Children[0], context, log, ref visits);
        }
    }
    private sealed record Spec(BehaviorNodeKind Kind, int Parameter, Spec[] Children);

    private static BehaviorTreeRunner<Context> Run(BehaviorNode node, out Context context, out Handler handler)
    {
        var runner = new BehaviorTreeRunner<Context>(new BehaviorTreeDefinition(node)); context = new Context(); handler = new Handler { Runner = runner }; runner.Start(); return runner;
    }
    private static BehaviorTickResult Tick(BehaviorTreeRunner<Context> runner, ref Context context, Handler handler, int allowance = 100)
    { var budget = new BehaviorBudget(allowance); return runner.Tick(ref context, handler, ref budget); }
    private sealed class Context
    {
        internal bool[] Predicates = new bool[8];
        internal BehaviorStatus[] Results = Enumerable.Repeat(BehaviorStatus.Running, 8).ToArray();
    }
    private sealed class Failure : Exception { }
    private sealed class Handler : IBehaviorTreeHandler<Context>
    {
        internal BehaviorTreeRunner<Context>? Runner;
        internal readonly List<string> Log = new();
        internal readonly List<BehaviorActivation> Tokens = new();
        internal int[] Enters = new int[8], Ticks = new int[8], Exits = new int[8], Aborts = new int[8], Predicates = new int[8];
        internal BehaviorActivation LastToken;
        internal int LastNodeId;
        internal string? ThrowPhase;
        internal bool ThrowCleanup, ThrowPredicate, Reenter, SawLiveExitOrAbort;
        public bool Evaluate(int id, in Context context)
        { Predicates[id]++; Log.Add("condition:" + id); if (ThrowPredicate) throw new Failure(); return context.Predicates[id]; }
        public void Enter(int node, int action, BehaviorActivation activation, ref Context context)
        { Enters[action]++; LastNodeId = node; LastToken = activation; Tokens.Add(activation); Log.Add("enter:" + action); Assert.True(Runner!.IsActive(activation)); if (Reenter) Runner.Start(); if (ThrowPhase == "enter") throw new Failure(); }
        public BehaviorStatus Tick(int node, int action, BehaviorActivation activation, ref Context context, ref BehaviorBudget budget)
        { Ticks[action]++; Log.Add("tick:" + action); if (ThrowPhase == "tick") throw new Failure(); return context.Results[action]; }
        public void Exit(int node, int action, BehaviorActivation activation, BehaviorStatus result, ref Context context)
        { Exits[action]++; SawLiveExitOrAbort |= Runner!.IsActive(activation); Log.Add("exit:" + action); if (ThrowPhase == "exit") throw new Failure(); }
        public void Abort(int node, int action, BehaviorActivation activation, ref Context context)
        { Aborts[action]++; SawLiveExitOrAbort |= Runner!.IsActive(activation); Log.Add("abort:" + action); if (ThrowPhase == "abort" || ThrowCleanup) throw new Failure(); }
    }
    private struct ValueHandler : IBehaviorTreeHandler<int>
    {
        public bool Evaluate(int id, in int context) => true;
        public void Enter(int node, int action, BehaviorActivation token, ref int context) { }
        public BehaviorStatus Tick(int node, int action, BehaviorActivation token, ref int context, ref BehaviorBudget budget) { context++; return BehaviorStatus.Running; }
        public void Exit(int node, int action, BehaviorActivation token, BehaviorStatus result, ref int context) { }
        public void Abort(int node, int action, BehaviorActivation token, ref int context) { }
    }
}
