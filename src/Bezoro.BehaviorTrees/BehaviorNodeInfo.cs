namespace Bezoro.BehaviorTrees;

/// <summary>Describes one occurrence in a compiled immutable tree.</summary>
public readonly struct BehaviorNodeInfo
{
    internal BehaviorNodeInfo(BehaviorNodeKind kind, int parameter, int childCount, int endIndex)
    { Kind = kind; Parameter = parameter; ChildCount = childCount; SubtreeEndIndex = endIndex; }
    /// <summary>Gets the operation kind.</summary>
    public BehaviorNodeKind Kind { get; }
    /// <summary>Gets the action/predicate ID or repeat count, depending on Kind; -1 identifies a compiled condition program.</summary>
    public int Parameter { get; }
    /// <summary>Gets the action ID, or -1 for other kinds.</summary>
    public int ActionId => Kind == BehaviorNodeKind.Action ? Parameter : -1;
    /// <summary>Gets the number of direct children.</summary>
    public int ChildCount { get; }
    /// <summary>Gets the inclusive final node index in this occurrence's subtree.</summary>
    public int SubtreeEndIndex { get; }
}
