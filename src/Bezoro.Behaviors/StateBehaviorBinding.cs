using Bezoro.StateMachines;

namespace Bezoro.Behaviors;

/// <summary>Runs an owned child while a particular state activation is present.</summary>
public readonly struct StateBehaviorBinding<TContext>
{
    /// <summary>Creates a state binding with optional one-shot completion signals.</summary>
    public StateBehaviorBinding(StateId state, Behavior<TContext> child, SignalId? successSignal = null, SignalId? failureSignal = null)
    {
        State = state;
        Child = child ?? throw new ArgumentNullException(nameof(child));
        SuccessSignal = successSignal;
        FailureSignal = failureSignal;
    }
    /// <summary>Gets the state that owns this child activation.</summary>
    public StateId State { get; }
    /// <summary>Gets the exclusively owned child runtime.</summary>
    public Behavior<TContext> Child { get; }
    /// <summary>Gets the signal emitted once when the child succeeds, or null to retain the terminal result until exit.</summary>
    public SignalId? SuccessSignal { get; }
    /// <summary>Gets the signal emitted once when the child fails, or null to retain the terminal result until exit.</summary>
    public SignalId? FailureSignal { get; }
}
