using Bezoro.StateMachines;

namespace Bezoro.Statecharts;

/// <summary>Declares a region's initial state and optional containing state.</summary>
public readonly struct ChartRegion
{
    /// <summary>Creates a root region when parentState is null, otherwise a child region of that state.</summary>
    public ChartRegion(StateId initialState, StateId? parentState = null) { InitialState = initialState; ParentState = parentState; }
    /// <summary>Gets the initial state, which must belong to this region.</summary>
    public StateId InitialState { get; }
    /// <summary>Gets the containing state, or null for an always-enabled root region.</summary>
    public StateId? ParentState { get; }
}
