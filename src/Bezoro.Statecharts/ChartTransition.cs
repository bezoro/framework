using Bezoro.Conditions;
using Bezoro.StateMachines;

namespace Bezoro.Statecharts;

/// <summary>Declares an signal transition with explicit external, local, or internal lifecycle semantics.</summary>
public readonly struct ChartTransition
{
    /// <summary>Creates a signal transition with an optional immutable guard and explicit priority.</summary>
    public ChartTransition(StateId source, SignalId signal, StateId target, ConditionProgram? guard = null, int priority = 0, ChartTransitionKind kind = ChartTransitionKind.External)
    { Source = source; Signal = signal; Target = target; Guard = guard; Priority = priority; Kind = kind; }
    /// <summary>Gets the active source state.</summary>
    public StateId Source { get; }
    /// <summary>Gets the matching signal.</summary>
    public SignalId Signal { get; }
    /// <summary>Gets the target state.</summary>
    public StateId Target { get; }
    /// <summary>Gets the guard, or null for an unconditional transition.</summary>
    public ConditionProgram? Guard { get; }
    /// <summary>Gets the priority. Higher priorities win before source depth and declaration order.</summary>
    public int Priority { get; }
    /// <summary>Gets the lifecycle semantics of this transition.</summary>
    public ChartTransitionKind Kind { get; }
}
