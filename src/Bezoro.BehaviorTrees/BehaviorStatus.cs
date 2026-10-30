namespace Bezoro.BehaviorTrees;

/// <summary>Describes an execution's lifecycle or a leaf action's result.</summary>
public enum BehaviorStatus
{
    /// <summary>The runner has not started or has been stopped.</summary>
    Inactive,
    /// <summary>Execution requires another update.</summary>
    Running,
    /// <summary>Execution completed successfully.</summary>
    Success,
    /// <summary>Execution completed unsuccessfully.</summary>
    Failure,
    /// <summary>A handler failed. Stop before starting again.</summary>
    Faulted
}
