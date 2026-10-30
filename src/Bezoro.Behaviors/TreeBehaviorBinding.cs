namespace Bezoro.Behaviors;

/// <summary>Binds a child runtime to one action occurrence in a compiled behavior tree.</summary>
public readonly struct TreeBehaviorBinding<TContext>
{
    /// <summary>Creates a binding. Node IDs identify compiled occurrences, not shared action identifiers.</summary>
    public TreeBehaviorBinding(int nodeId, Behavior<TContext> child)
    {
        if (nodeId < 0) throw new ArgumentOutOfRangeException(nameof(nodeId));
        Child = child ?? throw new ArgumentNullException(nameof(child));
        NodeId = nodeId;
    }
    /// <summary>Gets the action occurrence's compiled node ID.</summary>
    public int NodeId { get; }
    /// <summary>Gets the exclusively owned child runtime.</summary>
    public Behavior<TContext> Child { get; }
}
