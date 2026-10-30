using Bezoro.StateMachines;

namespace Bezoro.Statecharts;

/// <summary>A borrowed view of the active configuration supplied to a guard.</summary>
/// <remarks>Valid only during that guard call. The runner keeps it unchanged during guard selection.</remarks>
public readonly ref struct StatechartView
{
    private readonly StatechartDefinition _definition;
    internal StatechartView(StatechartDefinition definition, ReadOnlySpan<StateId> states) { _definition = definition; ActiveStates = states; }
    /// <summary>Gets active states in compiled parent-before-descendant order.</summary>
    public ReadOnlySpan<StateId> ActiveStates { get; }
    /// <summary>Tests whether a state is active; identifiers outside the definition return false.</summary>
    public bool IsActive(StateId state) => _definition is not null && _definition.FindActive(ActiveStates, state) >= 0;
}
