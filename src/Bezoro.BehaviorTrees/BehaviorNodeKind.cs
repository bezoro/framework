namespace Bezoro.BehaviorTrees;

/// <summary>Identifies a compiled control-flow or leaf operation.</summary>
public enum BehaviorNodeKind
{
    /// <summary>Runs one application action.</summary>
    Action,
    /// <summary>Evaluates a predicate or compiled logical program.</summary>
    Condition,
    /// <summary>Continues after successful children, retaining its current child.</summary>
    Sequence,
    /// <summary>Continues after failed children, retaining its current child.</summary>
    Selector,
    /// <summary>Rechecks prior children before continuing its running child.</summary>
    ReactiveSequence,
    /// <summary>Rechecks higher-priority children before continuing its running child.</summary>
    ReactiveSelector,
    /// <summary>Swaps success and failure.</summary>
    Invert,
    /// <summary>Repeats a successful child a finite number of times; failure ends the repeat.</summary>
    Repeat,
    /// <summary>Executes one copied occurrence of another immutable definition.</summary>
    Subtree,
    /// <summary>Completes successfully.</summary>
    Succeed,
    /// <summary>Completes unsuccessfully.</summary>
    Fail
}
