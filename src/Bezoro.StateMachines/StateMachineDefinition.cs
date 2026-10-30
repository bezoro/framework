namespace Bezoro.StateMachines;

/// <summary>Compiles an immutable sparse transition table for pure state evaluation.</summary>
/// <remarks>
/// Construction copies and sorts transitions. Queries allocate no storage and use O(log transition-count) lookup.
/// Definitions can be shared between threads. Each caller owns its current state and applies returned changes explicitly.
/// </remarks>
public sealed class StateMachineDefinition
{
    private readonly StateTransition[] _transitions;

    /// <summary>Creates a machine with zero-based state and signal ranges and an explicit initial state.</summary>
    /// <param name="stateCount">Number of states, greater than zero.</param>
    /// <param name="signalCount">Number of signals, which may be zero for a machine accepting no input.</param>
    /// <param name="transitions">Transitions to copy. Each source/signal pair must be unique.</param>
    /// <param name="initialState">Initial state; defaults to state zero.</param>
    /// <exception cref="ArgumentOutOfRangeException">A count or initial state is outside its accepted range.</exception>
    /// <exception cref="ArgumentException">A transition refers outside the declared ranges, or a source/signal pair is duplicated.</exception>
    public StateMachineDefinition(int stateCount, int signalCount, ReadOnlySpan<StateTransition> transitions, StateId initialState = default)
    {
        if (stateCount <= 0) throw new ArgumentOutOfRangeException(nameof(stateCount));
        if (signalCount < 0) throw new ArgumentOutOfRangeException(nameof(signalCount));
        if ((uint)initialState.Value >= (uint)stateCount) throw new ArgumentOutOfRangeException(nameof(initialState));
        foreach (var transition in transitions)
        {
            if ((uint)transition.Source.Value >= (uint)stateCount ||
                (uint)transition.Target.Value >= (uint)stateCount ||
                (uint)transition.Signal.Value >= (uint)signalCount)
                throw new ArgumentException("A transition refers outside the declared state or signal range.", nameof(transitions));
        }

        StateCount = stateCount;
        SignalCount = signalCount;
        InitialState = initialState;
        _transitions = transitions.ToArray();
        Array.Sort(_transitions, TransitionOrder.Instance);
        for (int index = 1; index < _transitions.Length; index++)
        {
            if (_transitions[index - 1].Source == _transitions[index].Source &&
                _transitions[index - 1].Signal == _transitions[index].Signal)
                throw new ArgumentException("A source-state and signal pair must have exactly one target.", nameof(transitions));
        }
    }

    /// <summary>Gets the number of states. Valid state IDs are in [0, StateCount).</summary>
    public int StateCount { get; }
    /// <summary>Gets the number of signals. Valid signal IDs are in [0, SignalCount).</summary>
    public int SignalCount { get; }
    /// <summary>Gets the application's initial state. Evaluation does not implicitly reset callers to it.</summary>
    public StateId InitialState { get; }
    /// <summary>Gets the number of explicit transitions.</summary>
    public int TransitionCount => _transitions.Length;
    /// <summary>Gets the immutable transitions ordered by source state and then by signal.</summary>
    public ReadOnlySpan<StateTransition> Transitions => _transitions;

    /// <summary>Evaluates one signal, preserving the supplied state when no transition matches.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The state or signal is outside this definition's declared range.</exception>
    public StateStep Step(StateId currentState, SignalId signal)
    {
        bool matched = TryGetTransition(currentState, signal, out var transition);
        return new(currentState, matched ? transition.Target : currentState, signal, matched);
    }

    /// <summary>Finds a transition. On a miss, the output is default and must not be interpreted as a matched transition.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The state or signal is outside this definition's declared range.</exception>
    public bool TryGetTransition(StateId currentState, SignalId signal, out StateTransition transition)
    {
        if ((uint)currentState.Value >= (uint)StateCount) throw new ArgumentOutOfRangeException(nameof(currentState));
        if ((uint)signal.Value >= (uint)SignalCount) throw new ArgumentOutOfRangeException(nameof(signal));

        int low = 0;
        int high = _transitions.Length - 1;
        while (low <= high)
        {
            int middle = low + ((high - low) >> 1);
            var candidate = _transitions[middle];
            int comparison = candidate.Source.Value.CompareTo(currentState.Value);
            if (comparison == 0) comparison = candidate.Signal.Value.CompareTo(signal.Value);
            if (comparison < 0) low = middle + 1;
            else if (comparison > 0) high = middle - 1;
            else
            {
                transition = candidate;
                return true;
            }
        }

        transition = default;
        return false;
    }

    private sealed class TransitionOrder : IComparer<StateTransition>
    {
        internal static readonly TransitionOrder Instance = new();

        public int Compare(StateTransition left, StateTransition right)
        {
            int source = left.Source.Value.CompareTo(right.Source.Value);
            return source != 0 ? source : left.Signal.Value.CompareTo(right.Signal.Value);
        }
    }
}
