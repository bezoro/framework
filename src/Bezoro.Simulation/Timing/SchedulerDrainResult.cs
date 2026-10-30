namespace Bezoro.Simulation.Timing;

/// <summary>Describes the populated prefix of a drain buffer and whether due work remains.</summary>
public readonly record struct SchedulerDrainResult
{
    internal SchedulerDrainResult(int count, bool hasMoreDue)
    {
        Count = count;
        HasMoreDue = hasMoreDue;
    }

    /// <summary>Gets the number of items written to the supplied buffer.</summary>
    public int Count { get; }

    /// <summary>Gets whether the scheduler still contains items due at its current tick.</summary>
    public bool HasMoreDue { get; }
}
