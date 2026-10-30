using System;
using Bezoro.BehaviorTrees;
using Bezoro.Behaviors;
using Bezoro.Conditions;
using Bezoro.Statecharts;
using Bezoro.StateMachines;

namespace Bezoro.UnityApiSmoke
{
    public static class BehaviorConsumer
    {
        public static int ExerciseDataApis()
        {
            var context = new Context { Allowed = true, Ticks = 0 };
            var evaluator = new PredicateEvaluator();
            ConditionProgram guard = ConditionExpression.All(ConditionExpression.Predicate(0),
                ConditionExpression.Not(ConditionExpression.False)).Compile();
            Span<bool> scratch = stackalloc bool[guard.RequiredStackSize];
            if (!guard.Evaluate(in context, ref evaluator, scratch))
                throw new InvalidOperationException("Typed condition failed.");

            StateId grounded = new StateId(0), inAir = new StateId(1), normal = new StateId(2), hit = new StateId(3);
            var chart = new StatechartDefinition(new[] { new ChartState(new RegionId(0)), new ChartState(new RegionId(0)),
                new ChartState(new RegionId(1)), new ChartState(new RegionId(1)) },
                new[] { new ChartRegion(grounded), new ChartRegion(normal) },
                new[] { new ChartTransition(normal, default, hit, ConditionExpression.Predicate(0).Compile()) }, 1);
            var stateHandler = new ChartHandler(grounded);
            var stateRunner = new StatechartRunner<Context>(chart, stateHandler);
            stateRunner.Start(ref context);
            StateActivation old = stateRunner.GetActivation(normal);
            stateRunner.Dispatch(default, ref context);
            if (!stateRunner.IsActive(grounded) || !stateRunner.IsActive(hit) || stateRunner.IsActive(old))
                throw new InvalidOperationException("Orthogonal statechart failed.");
            stateRunner.Stop(ref context);

            var handler = new Actions();
            var tree = new BehaviorTreeDefinition(BehaviorNode.Sequence(BehaviorNode.Condition(0), BehaviorNode.Action(7)));
            var treeRunner = new BehaviorTreeRunner<Context>(tree);
            treeRunner.Start();
            var budget = new BehaviorBudget(16);
            if (treeRunner.Tick(ref context, handler, ref budget).Status != BehaviorStatus.Running)
                throw new InvalidOperationException("The first action update must remain running.");
            budget = new BehaviorBudget(16);
            if (treeRunner.Tick(ref context, handler, ref budget).Status != BehaviorStatus.Success)
                throw new InvalidOperationException("The second action update must finish.");
            treeRunner.Stop(ref context, handler);

            // BT -> flat FSM -> BT, using one shared visit allowance through every level.
            context.Ticks = 0;
            StateId work = new StateId(0), done = new StateId(1);
            var flat = new StateMachineDefinition(2, 1, new[] { new StateTransition(work, default, done) });
            var child = new TreeBehavior<Context>(new BehaviorTreeDefinition(BehaviorNode.Action(7)), handler);
            var machine = new StateMachineBehavior<Context>(flat,
                new[] { new StateBehaviorBinding<Context>(work, child, successSignal: new SignalId(0)) },
                successStates: new[] { done });
            var outer = new TreeBehavior<Context>(new BehaviorTreeDefinition(BehaviorNode.Action(99)),
                bindings: new[] { new TreeBehaviorBinding<Context>(0, machine) });
            outer.Start(ref context);
            for (int update = 0; update < 32 && outer.Status == BehaviorStatus.Running; update++)
            {
                budget = new BehaviorBudget(1);
                outer.Tick(ref context, ref budget);
                if (budget.Consumed > 1) throw new InvalidOperationException("Nested behavior exceeded the shared allowance.");
            }
            if (outer.Status != BehaviorStatus.Success || context.Ticks != 2)
                throw new InvalidOperationException("Nested composition did not advance with a one-visit budget.");
            outer.Stop(ref context);
            return context.Ticks;
        }

        private struct Context { public bool Allowed; public int Ticks; }
        private struct PredicateEvaluator : IConditionEvaluator<Context>
        {
            public bool Evaluate(int predicateId, in Context context) { return context.Allowed; }
        }
        private sealed class ChartHandler : StatechartHandler<Context>
        {
            private readonly StateId _grounded;
            public ChartHandler(StateId grounded) { _grounded = grounded; }
            public override bool EvaluateGuard(int predicateId, StatechartView configuration, in Context context)
            { return configuration.IsActive(_grounded) && context.Allowed; }
        }
        private sealed class Actions : IBehaviorTreeHandler<Context>
        {
            public bool Evaluate(int predicateId, in Context context) { return context.Allowed; }
            public void Enter(int nodeId, int actionId, BehaviorActivation activation, ref Context context) { }
            public BehaviorStatus Tick(int nodeId, int actionId, BehaviorActivation activation, ref Context context, ref BehaviorBudget budget)
            { return ++context.Ticks == 1 ? BehaviorStatus.Running : BehaviorStatus.Success; }
            public void Exit(int nodeId, int actionId, BehaviorActivation activation, BehaviorStatus result, ref Context context) { }
            public void Abort(int nodeId, int actionId, BehaviorActivation activation, ref Context context) { }
        }
    }
}
