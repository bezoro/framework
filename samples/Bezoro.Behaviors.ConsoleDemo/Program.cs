using Bezoro.Behaviors;
using Bezoro.BehaviorTrees;
using Bezoro.Conditions;
using Bezoro.Simulation.Timing;
using Bezoro.Statecharts;
using Bezoro.StateMachines;
using Bezoro.UnityApiSmoke;

StateId patrolling = new(0), combat = new(1), finished = new(2);
SignalId threatSeen = new(0), combatFinished = new(1);
var context = new NpcContext();
var actions = new NpcActions();
var patrolDefinition = new BehaviorTreeDefinition(BehaviorNode.Sequence(
    BehaviorNode.Condition(0), BehaviorNode.Action(10)));
var patrol = new TreeBehavior<NpcContext>(patrolDefinition, actions);

// A flat FSM runs inside a tree action, and owns another tree while aiming.
StateId aiming = new(0), ready = new(1);
var aimingTree = new TreeBehavior<NpcContext>(new BehaviorTreeDefinition(BehaviorNode.Action(20)), actions);
var aimingMachine = new StateMachineBehavior<NpcContext>(
    new StateMachineDefinition(2, 1, new[] { new StateTransition(aiming, default, ready) }),
    new[] { new StateBehaviorBinding<NpcContext>(aiming, aimingTree, successSignal: new SignalId(0)) },
    successStates: new[] { ready });
var attackDefinition = new BehaviorTreeDefinition(BehaviorNode.Sequence(
    BehaviorNode.Condition(1), BehaviorNode.Action(30)));
var combatDefinition = new BehaviorTreeDefinition(BehaviorNode.Sequence(
    BehaviorNode.Action(99), BehaviorNode.Subtree(attackDefinition)));
var combatTree = new TreeBehavior<NpcContext>(combatDefinition, actions,
    new[] { new TreeBehaviorBinding<NpcContext>(1, aimingMachine) });
var modeDefinition = new StatechartDefinition(
    new[] { new ChartState(default), new ChartState(default), new ChartState(default) },
    new[] { new ChartRegion(patrolling) },
    new[] { new ChartTransition(patrolling, threatSeen, combat, ConditionExpression.Predicate(0).Compile()),
        new ChartTransition(combat, combatFinished, finished) }, 2);
var perception = new Perception();
var modes = new StatechartBehavior<NpcContext>(modeDefinition, perception,
    new[] { new StateBehaviorBinding<NpcContext>(patrolling, patrol),
        new StateBehaviorBinding<NpcContext>(combat, combatTree, successSignal: combatFinished) },
    successStates: new[] { finished });
// The outer tree demonstrates a statechart as a BT node as well.
var root = new TreeBehavior<NpcContext>(new BehaviorTreeDefinition(BehaviorNode.Action(100)),
    bindings: new[] { new TreeBehaviorBinding<NpcContext>(0, modes) });
// A tree action enters during its first budgeted tick. Start the nested chart before reading its token.
root.Start(ref context);
var startupBudget = new BehaviorBudget(16);
root.Tick(ref context, ref startupBudget);
StateActivation patrolActivation = modes.GetActivation(patrolling);
var scheduler = new TickScheduler<PollSignal>(1);
scheduler.Schedule(new PollSignal(patrolActivation, threatSeen), TimeSpan.FromMilliseconds(500).Ticks);
Span<ScheduledItem<PollSignal>> due = stackalloc ScheduledItem<PollSignal>[1];
for (int frame = 1; frame <= 60 && root.Status == BehaviorStatus.Running; frame++)
{
    long now = TimeSpan.FromMilliseconds(frame * 10).Ticks;
    SchedulerDrainResult batch = scheduler.AdvanceTo(now, due);
    for (int i = 0; i < batch.Count; i++)
    {
        PollSignal poll = due[i].Value;
        // This stands in for an application-owned perception snapshot at the scheduled instant.
        context.HasTarget = true;
        modes.TryDispatch(poll.Activation, poll.Signal, ref context);
    }
    var budget = new BehaviorBudget(16);
    root.Tick(ref context, ref budget);
    if (frame < 50) Require(perception.SightChecks == 0, "Perception must wait for its scheduled signal.");
}
Require(root.Status == BehaviorStatus.Success, "The complete graph must finish combat.");
Require(actions.PatrolGuards == 1, "The memory sequence must not repeat its successful prefix.");
Require(context.WalkTicks > 0 && context.WalkAborts == 1 && context.AimTicks == 2 && context.Attacks == 1,
    "The active paths and interruption lifecycle must match the selected mode.");
Require(perception.SightChecks == 1 && !modes.IsActive(patrolActivation), "The old patrol activation must be invalid.");
root.Stop(ref context);
Require(BehaviorConsumer.ExerciseDataApis() == 2, "The C# 9 behavior consumer must execute successfully.");
Console.WriteLine($"Patrol guard {actions.PatrolGuards}x, walking {context.WalkTicks} ticks, abort {context.WalkAborts}x.");
Console.WriteLine($"Perception {perception.SightChecks}x at 500 ms; aiming {context.AimTicks} ticks; attacks {context.Attacks}.");
Console.WriteLine("Nested BT -> statechart -> BT -> flat FSM -> BT completed; C# 9 consumer passed.");

static void Require(bool condition, string message)
{ if (!condition) throw new InvalidOperationException(message); }

readonly record struct PollSignal(StateActivation Activation, SignalId Signal);
sealed class NpcContext
{
    public bool HasTarget = false;
    public int WalkTicks = 0;
    public int WalkAborts = 0;
    public int AimTicks = 0;
    public int Attacks = 0;
}
sealed class Perception : StatechartHandler<NpcContext>
{
    public int SightChecks { get; private set; }
    public override bool EvaluateGuard(int predicateId, StatechartView configuration, in NpcContext context)
    {
        SightChecks++;
        return context.HasTarget;
    }
}
sealed class NpcActions : IBehaviorTreeHandler<NpcContext>
{
    public int PatrolGuards { get; private set; }
    public bool Evaluate(int predicateId, in NpcContext context)
    {
        if (predicateId == 0) { PatrolGuards++; return true; }
        return context.HasTarget;
    }
    public void Enter(int nodeId, int actionId, BehaviorActivation activation, ref NpcContext context) { }
    public BehaviorStatus Tick(int nodeId, int actionId, BehaviorActivation activation, ref NpcContext context, ref BehaviorBudget budget)
    {
        switch (actionId)
        {
            case 10: context.WalkTicks++; return BehaviorStatus.Running;
            case 20: return ++context.AimTicks < 2 ? BehaviorStatus.Running : BehaviorStatus.Success;
            case 30: context.Attacks++; return BehaviorStatus.Success;
            default: throw new InvalidOperationException("Unknown host action.");
        }
    }
    public void Exit(int nodeId, int actionId, BehaviorActivation activation, BehaviorStatus result, ref NpcContext context) { }
    public void Abort(int nodeId, int actionId, BehaviorActivation activation, ref NpcContext context)
    { if (actionId == 10) context.WalkAborts++; }
}
