namespace Bezoro.StateMachines;

/// <summary>Describes the result of evaluating one signal without mutating an agent or definition.</summary>
public readonly struct StateStep
{
    internal StateStep(StateId previousState, StateId currentState, SignalId signal, bool hasTransition)
    {
        PreviousState = previousState;
        CurrentState = currentState;
        Signal = signal;
        HasTransition = hasTransition;
    }

    /// <summary>Gets the state supplied by the caller.</summary>
    public StateId PreviousState { get; }
    /// <summary>Gets the target state, or the supplied state when no transition matches.</summary>
    public StateId CurrentState { get; }
    /// <summary>Gets the evaluated signal.</summary>
    public SignalId Signal { get; }
    /// <summary>Gets whether a transition matched, including an explicit self transition.</summary>
    public bool HasTransition { get; }
    /// <summary>Gets whether the target differs from the previous state. Self transitions return false.</summary>
    public bool HasStateChanged => PreviousState != CurrentState;
}
