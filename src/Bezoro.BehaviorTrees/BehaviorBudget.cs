namespace Bezoro.BehaviorTrees;

/// <summary>A mutable visit allowance shared by nested behavior executions.</summary>
/// <remarks>Visits bound evaluator work, not elapsed time or arbitrary handler cost.</remarks>
public struct BehaviorBudget
{
    private readonly int _maximum;
    private int _remaining;
    /// <summary>Creates an allowance. Zero is a valid exhausted budget.</summary>
    public BehaviorBudget(int maximumVisits)
    {
        if (maximumVisits < 0) throw new ArgumentOutOfRangeException(nameof(maximumVisits));
        _maximum = maximumVisits;
        _remaining = maximumVisits;
    }
    /// <summary>Gets the remaining visit allowance.</summary>
    public readonly int Remaining => _remaining;
    /// <summary>Gets the allowance already consumed, including nested executions.</summary>
    public readonly int Consumed => _maximum - _remaining;
    /// <summary>Consumes one visit, or returns false without changing an exhausted budget.</summary>
    public bool TryConsume()
    {
        if (_remaining == 0) return false;
        _remaining--;
        return true;
    }
}
