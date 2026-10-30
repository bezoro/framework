namespace Bezoro.Statecharts;

/// <summary>Reports the number of transitions and state lifecycle changes caused by one signal.</summary>
public readonly struct StatechartDispatchResult
{
    internal StatechartDispatchResult(int transitions, int exited, int entered)
    { TransitionCount = transitions; ExitedCount = exited; EnteredCount = entered; }
    /// <summary>Gets the number of selected, nonconflicting transitions.</summary>
    public int TransitionCount { get; }
    /// <summary>Gets the number of exited states.</summary>
    public int ExitedCount { get; }
    /// <summary>Gets the number of entered states, including initial descendants.</summary>
    public int EnteredCount { get; }
    /// <summary>Gets whether any transition matched, including a self transition.</summary>
    public bool HasTransition => TransitionCount != 0;
}
