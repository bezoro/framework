using Bezoro.BehaviorTrees;

namespace Bezoro.Behaviors;

/// <summary>Runs one explicitly owned behavior with caller-supplied context and work budget.</summary>
/// <typeparam name="TContext">The application's mutable context type.</typeparam>
public interface IBehavior<TContext>
{
    /// <summary>Gets the current lifecycle status. Terminal results remain latched until stopped.</summary>
    BehaviorStatus Status { get; }
    /// <summary>Gets whether the last Tick deferred unfinished evaluation specifically because its shared allowance was exhausted.</summary>
    bool IsBudgetSuspended { get; }
    /// <summary>Starts an inactive behavior. Starting does not perform a tick.</summary>
    void Start(ref TContext context);
    /// <summary>Advances a started behavior using the same budget across its nested components.</summary>
    BehaviorStatus Tick(ref TContext context, ref BehaviorBudget budget);
    /// <summary>Stops a behavior and its started descendants. Stopping an inactive behavior is a no-op.</summary>
    void Stop(ref TContext context);
}
