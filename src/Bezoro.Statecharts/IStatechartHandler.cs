using Bezoro.StateMachines;

namespace Bezoro.Statecharts;

/// <summary>Supplies pure guards and synchronous lifecycle effects for one owned runner.</summary>
/// <remarks>Callbacks must not reenter the runner. Guards must not mutate context or the chart.</remarks>
public interface IStatechartHandler<TContext>
{
    /// <summary>Evaluates a predicate against the unchanged pre-dispatch configuration.</summary>
    bool EvaluateGuard(int predicateId, StatechartView configuration, in TContext context);
    /// <summary>Runs after the state and its new token become active, before its descendants enter.</summary>
    void OnEnter(StateId state, StateActivation activation, ref TContext context);
    /// <summary>Runs after the state and token become inactive. Every exit is attempted at most once.</summary>
    void OnExit(StateId state, StateActivation activation, ref TContext context);
    /// <summary>Runs after all selected exits and before any selected entries.</summary>
    void OnTransition(int transitionIndex, in ChartTransition transition, ref TContext context);
}

/// <summary>Convenience handler with no lifecycle effects and an explicit error for an unhandled guard.</summary>
public abstract class StatechartHandler<TContext> : IStatechartHandler<TContext>
{
    /// <inheritdoc />
    public virtual bool EvaluateGuard(int predicateId, StatechartView configuration, in TContext context) =>
        throw new InvalidOperationException("The guard predicate has no evaluator.");
    /// <inheritdoc />
    public virtual void OnEnter(StateId state, StateActivation activation, ref TContext context) { }
    /// <inheritdoc />
    public virtual void OnExit(StateId state, StateActivation activation, ref TContext context) { }
    /// <inheritdoc />
    public virtual void OnTransition(int transitionIndex, in ChartTransition transition, ref TContext context) { }
}
