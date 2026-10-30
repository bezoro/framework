using Bezoro.Conditions;

namespace Bezoro.BehaviorTrees;

/// <summary>Connects typed application data and actions to an engine-independent tree.</summary>
/// <remarks>
/// Guard evaluation must not mutate the runner. Callbacks are synchronous; handlers own any asynchronous jobs.
/// A handler can use activation tokens to reject late results after completion, preemption or restart.
/// </remarks>
public interface IBehaviorTreeHandler<TContext> : IConditionEvaluator<TContext>
{
    /// <summary>Begins one action activation.</summary>
    void Enter(int nodeId, int actionId, BehaviorActivation activation, ref TContext context);
    /// <summary>Updates an active action. Return Running, Success or Failure only.</summary>
    BehaviorStatus Tick(int nodeId, int actionId, BehaviorActivation activation, ref TContext context, ref BehaviorBudget budget);
    /// <summary>Ends an action after Success or Failure. Its activation is already invalid.</summary>
    void Exit(int nodeId, int actionId, BehaviorActivation activation, BehaviorStatus result, ref TContext context);
    /// <summary>Interrupts an action. Its activation is already invalid.</summary>
    void Abort(int nodeId, int actionId, BehaviorActivation activation, ref TContext context);
}
