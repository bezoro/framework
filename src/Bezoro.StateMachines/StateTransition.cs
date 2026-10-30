namespace Bezoro.StateMachines;

/// <summary>Maps one source-state and signal pair to a target state.</summary>
/// <remarks>The definition validates membership and rejects duplicate source/signal pairs.</remarks>
public readonly struct StateTransition : IEquatable<StateTransition>
{
    /// <summary>Creates a transition, including a self transition when source and target are equal.</summary>
    public StateTransition(StateId source, SignalId signal, StateId target)
    {
        Source = source;
        Signal = signal;
        Target = target;
    }

    /// <summary>Gets the source state.</summary>
    public StateId Source { get; }
    /// <summary>Gets the input signal.</summary>
    public SignalId Signal { get; }
    /// <summary>Gets the target state.</summary>
    public StateId Target { get; }

    /// <inheritdoc />
    public bool Equals(StateTransition other) => Source == other.Source && Signal == other.Signal && Target == other.Target;
    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is StateTransition other && Equals(other);
    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(Source, Signal, Target);
    /// <summary>Compares transitions.</summary>
    public static bool operator ==(StateTransition left, StateTransition right) => left.Equals(right);
    /// <summary>Compares transitions.</summary>
    public static bool operator !=(StateTransition left, StateTransition right) => !left.Equals(right);
}
