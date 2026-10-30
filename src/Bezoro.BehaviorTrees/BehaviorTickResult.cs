namespace Bezoro.BehaviorTrees;

/// <summary>Reports the resulting status and allowance consumed by one update.</summary>
public readonly struct BehaviorTickResult
{
    internal BehaviorTickResult(BehaviorStatus status, int consumedVisits, bool budgetExhausted)
    { Status = status; ConsumedVisits = consumedVisits; BudgetExhausted = budgetExhausted; }
    /// <summary>Gets the runner's status after this update.</summary>
    public BehaviorStatus Status { get; }
    /// <summary>Gets visits consumed, including nested work charged to the same allowance.</summary>
    public int ConsumedVisits { get; }
    /// <summary>Gets whether evaluation yielded because the visit allowance was exhausted.</summary>
    public bool BudgetExhausted { get; }
}
