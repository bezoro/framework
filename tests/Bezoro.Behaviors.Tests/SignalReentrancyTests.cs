using Bezoro.BehaviorTrees;
using Bezoro.Conditions;
using Bezoro.Statecharts;
using Bezoro.StateMachines;
using Xunit;

namespace Bezoro.Behaviors.Tests;

public sealed class SignalReentrancyTests
{
    [Theory]
    [InlineData(Operation.StopRoot)]
    [InlineData(Operation.TickRoot)]
    public void NestedGuard_CannotMutateItsRootBeforeRejectingReentrancy(Operation operation)
    {
        var context = new Context();
        var resource = new Resource();
        var source = CreateChart(new CallbackHandler(), resource);
        var root = new Group(new Group(source));
        context.Root = root;
        root.Start(ref context);
        StateActivation activation = source.GetActivation(new StateId(0));
        context.Phase = Phase.Guard;
        context.Operation = operation;

        Assert.Throws<InvalidOperationException>(() => source.Dispatch(new SignalId(0), ref context));

        Assert.Equal(BehaviorStatus.Running, root.Status);
        Assert.Equal(BehaviorStatus.Running, source.Status);
        Assert.Equal(BehaviorStatus.Running, resource.Status);
        Assert.True(source.IsActive(activation));
        Assert.Equal(1, context.OpenResources);
        Assert.Equal(0, resource.Stops);

        // A thrown guard must release the entire reservation, including the intermediate parent.
        context.Phase = Phase.None;
        var budget = new BehaviorBudget(8);
        root.Tick(ref context, ref budget);
        source.Dispatch(new SignalId(0), ref context);
        root.Stop(ref context);
        Assert.Equal(0, context.OpenResources);
        Assert.Equal(1, resource.Stops);
    }

    [Theory]
    [InlineData(Phase.Guard, Operation.StopRoot)]
    [InlineData(Phase.Guard, Operation.TickRoot)]
    [InlineData(Phase.Guard, Operation.SignalSibling)]
    [InlineData(Phase.Enter, Operation.StopRoot)]
    [InlineData(Phase.Enter, Operation.TickRoot)]
    [InlineData(Phase.Enter, Operation.SignalSibling)]
    [InlineData(Phase.Exit, Operation.StopRoot)]
    [InlineData(Phase.Exit, Operation.TickRoot)]
    [InlineData(Phase.Exit, Operation.SignalSibling)]
    public void DescendantSignal_ReservesTheGraphWhileAllowingItsOwnChildLifecycle(Phase phase, Operation operation)
    {
        var context = new Context { CatchRejection = true };
        var first = new Resource();
        var next = new Resource();
        var siblingResource = new Resource();
        var source = CreateChart(new CallbackHandler(), first, next);
        var sibling = CreateChart(null, siblingResource);
        var root = new Group(new Group(source), sibling);
        context.Root = root;
        context.Sibling = sibling;
        root.Start(ref context);
        StateActivation siblingActivation = sibling.GetActivation(new StateId(0));
        context.Phase = phase;
        context.Operation = operation;
        // A rejected sibling Begin must not clear the source operation's ancestor reservation.
        context.CheckRootAfterSibling = operation == Operation.SignalSibling;

        source.Dispatch(new SignalId(0), ref context);

        Assert.Equal(operation == Operation.SignalSibling ? 2 : 1, context.Rejections);
        Assert.Equal(BehaviorStatus.Running, root.Status);
        Assert.Equal(BehaviorStatus.Running, source.Status);
        Assert.True(source.IsActive(new StateId(1)));
        Assert.True(sibling.IsActive(siblingActivation));
        Assert.Equal(1, first.Stops);
        Assert.Equal(BehaviorStatus.Running, next.Status);
        Assert.Equal(2, context.OpenResources);

        context.Phase = Phase.None;
        sibling.Dispatch(new SignalId(0), ref context);
        Assert.False(sibling.IsActive(siblingActivation));
        var budget = new BehaviorBudget(16);
        root.Tick(ref context, ref budget);
        root.Stop(ref context);
        Assert.Equal(0, context.OpenResources);
        Assert.Equal(1, first.Stops);
        Assert.Equal(1, next.Stops);
        Assert.Equal(1, siblingResource.Stops);
    }

    [Theory]
    [InlineData(Phase.Enter)]
    [InlineData(Phase.Exit)]
    public void ThrowingLifecycleCallback_ReleasesAncestorReservationsAndKeepsRootCleanupAvailable(Phase phase)
    {
        var context = new Context();
        var first = new Resource();
        var next = new Resource();
        var siblingResource = new Resource();
        var source = CreateChart(new CallbackHandler(), first, next);
        var sibling = CreateChart(null, siblingResource);
        var root = new Group(new Group(source), sibling);
        context.Root = root;
        context.Sibling = sibling;
        root.Start(ref context);
        context.Phase = phase;
        context.Operation = Operation.StopRoot;

        Assert.ThrowsAny<Exception>(() => source.Dispatch(new SignalId(0), ref context));

        Assert.Equal(BehaviorStatus.Running, root.Status);
        Assert.Equal(BehaviorStatus.Faulted, source.Status);
        Assert.Equal(BehaviorStatus.Running, sibling.Status);
        Assert.Equal(1, context.OpenResources);
        context.Phase = Phase.None;
        root.Stop(ref context);
        Assert.Equal(0, context.OpenResources);
        Assert.Equal(1, first.Stops);
        Assert.Equal(1, siblingResource.Stops);
        root.Start(ref context);
        root.Stop(ref context);
        Assert.Equal(0, context.OpenResources);
    }

    [Theory]
    [InlineData(Operation.StopRoot)]
    [InlineData(Operation.TickRoot)]
    public void NestedFlatSignal_CannotReenterItsRootFromCancellation(Operation operation)
    {
        var context = new Context { CatchRejection = true };
        var resource = new Resource { CallbackOnStop = true };
        var next = new Resource();
        var flat = new StateMachineBehavior<Context>(new StateMachineDefinition(2, 1,
            new[] { new StateTransition(new StateId(0), new SignalId(0), new StateId(1)) }),
            new[] { new StateBehaviorBinding<Context>(new StateId(0), resource),
                new StateBehaviorBinding<Context>(new StateId(1), next) });
        var root = new Group(new Group(flat));
        context.Root = root;
        root.Start(ref context);
        context.Phase = Phase.Exit;
        context.Operation = operation;

        flat.Dispatch(new SignalId(0), ref context);

        Assert.Equal(1, context.Rejections);
        Assert.Equal(BehaviorStatus.Running, root.Status);
        Assert.Equal(new StateId(1), flat.CurrentState);
        Assert.Equal(1, resource.Stops);
        Assert.Equal(1, context.OpenResources);
        context.Phase = Phase.None;
        root.Stop(ref context);
        Assert.Equal(1, next.Stops);
        Assert.Equal(0, context.OpenResources);
    }

    [Fact]
    public void ThrowingFlatCancellation_ReleasesAncestorReservationBeforeTheHostStopsTheRoot()
    {
        var context = new Context();
        var resource = new Resource { CallbackOnStop = true };
        var flat = new StateMachineBehavior<Context>(new StateMachineDefinition(2, 1,
            new[] { new StateTransition(new StateId(0), new SignalId(0), new StateId(1)) }),
            new[] { new StateBehaviorBinding<Context>(new StateId(0), resource) });
        var root = new Group(new Group(flat));
        context.Root = root;
        root.Start(ref context);
        context.Phase = Phase.Exit;
        context.Operation = Operation.StopRoot;

        Assert.Throws<InvalidOperationException>(() => flat.Dispatch(new SignalId(0), ref context));

        Assert.Equal(BehaviorStatus.Running, root.Status);
        Assert.Equal(BehaviorStatus.Faulted, flat.Status);
        Assert.Equal(0, context.OpenResources);
        context.Phase = Phase.None;
        root.Stop(ref context);
        Assert.Equal(1, resource.Stops);
    }

    [Fact]
    public void SignalReservations_PreserveUnchangedSiblingTokenLiveness()
    {
        var context = new Context { CatchRejection = true };
        var source = CreateChart(new CallbackHandler(), new Resource());
        var sibling = CreateChart(null, new Resource());
        var root = new Group(source, sibling);
        context.Root = root;
        context.Sibling = sibling;
        root.Start(ref context);
        context.SiblingActivation = sibling.GetActivation(new StateId(0));
        context.Phase = Phase.Guard;
        context.Operation = Operation.ObserveSibling;

        source.Dispatch(new SignalId(0), ref context);

        Assert.True(context.SiblingTokenStayedLive);
        Assert.Equal(0, context.Rejections);
        root.Stop(ref context);
    }

    private static StatechartBehavior<Context> CreateChart(CallbackHandler? handler, Resource first, Resource? next = null)
    {
        ConditionProgram? guard = handler is null ? null : ConditionExpression.Predicate(0).Compile();
        var definition = new StatechartDefinition(
            new[] { new ChartState(new RegionId(0)), new ChartState(new RegionId(0)) },
            new[] { new ChartRegion(new StateId(0)) },
            new[] { new ChartTransition(new StateId(0), new SignalId(0), new StateId(1), guard) }, 1);
        StateBehaviorBinding<Context>[] bindings = next is null
            ? new[] { new StateBehaviorBinding<Context>(new StateId(0), first) }
            : new[] { new StateBehaviorBinding<Context>(new StateId(0), first), new StateBehaviorBinding<Context>(new StateId(1), next) };
        return new StatechartBehavior<Context>(definition, handler, bindings);
    }

    private static void Attempt(Context context)
    {
        try { Mutate(context, context.Operation); }
        catch (InvalidOperationException) when (context.CatchRejection) { context.Rejections++; }
        if (context.CheckRootAfterSibling && context.Operation == Operation.SignalSibling)
        {
            try { Mutate(context, Operation.StopRoot); }
            catch (InvalidOperationException) when (context.CatchRejection) { context.Rejections++; }
        }
    }

    private static void Mutate(Context context, Operation operation)
    {
        switch (operation)
        {
            case Operation.StopRoot: context.Root.Stop(ref context); break;
            case Operation.TickRoot:
                var budget = new BehaviorBudget(16);
                context.Root.Tick(ref context, ref budget);
                break;
            case Operation.SignalSibling: context.Sibling.Dispatch(new SignalId(0), ref context); break;
            case Operation.ObserveSibling:
                context.SiblingTokenStayedLive = context.Sibling.IsActive(context.SiblingActivation);
                break;
            default: throw new ArgumentOutOfRangeException(nameof(operation));
        }
    }

    public enum Phase { None, Guard, Enter, Exit }
    public enum Operation { StopRoot, TickRoot, SignalSibling, ObserveSibling }
    private sealed class Context
    {
        internal Behavior<Context> Root = null!;
        internal StatechartBehavior<Context> Sibling = null!;
        internal StateActivation SiblingActivation;
        internal Phase Phase;
        internal Operation Operation;
        internal bool CatchRejection;
        internal bool CheckRootAfterSibling;
        internal bool SiblingTokenStayedLive;
        internal int Rejections;
        internal int OpenResources;
    }
    private sealed class CallbackHandler : StatechartHandler<Context>
    {
        public override bool EvaluateGuard(int predicateId, StatechartView configuration, in Context context)
        { if (context.Phase == Phase.Guard) Attempt(context); return true; }
        public override void OnEnter(StateId state, StateActivation activation, ref Context context)
        { if (context.Phase == Phase.Enter) Attempt(context); }
        public override void OnExit(StateId state, StateActivation activation, ref Context context)
        { if (context.Phase == Phase.Exit) Attempt(context); }
    }
    private sealed class Resource : Behavior<Context>
    {
        internal bool CallbackOnStop;
        internal int Stops;
        protected override void OnStart(ref Context context) => context.OpenResources++;
        protected override BehaviorStatus OnTick(ref Context context, ref BehaviorBudget budget)
        { budget.TryConsume(); return BehaviorStatus.Running; }
        protected override void OnStop(ref Context context)
        {
            Stops++;
            context.OpenResources--;
            if (CallbackOnStop && context.Phase == Phase.Exit) Attempt(context);
        }
    }
    private sealed class Group : Behavior<Context>
    {
        internal Group(params Behavior<Context>[] children) : base(children, default) { }
        protected override void OnStart(ref Context context)
        { foreach (var child in Children) StartChild(child, ref context); }
        protected override BehaviorStatus OnTick(ref Context context, ref BehaviorBudget budget)
        { foreach (var child in Children) TickChild(child, ref context, ref budget); return BehaviorStatus.Running; }
        protected override void OnStop(ref Context context) { }
    }
}
