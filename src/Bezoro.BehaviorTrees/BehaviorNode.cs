using Bezoro.Conditions;

namespace Bezoro.BehaviorTrees;

/// <summary>An immutable construction expression. Compile it once and share the resulting definition.</summary>
public sealed class BehaviorNode
{
    internal readonly BehaviorNode[] Children;
    internal readonly int Parameter;
    internal readonly ConditionProgram? Program;
    internal readonly BehaviorTreeDefinition? SubtreeDefinition;
    internal BehaviorNodeKind Kind { get; }

    private BehaviorNode(BehaviorNodeKind kind, int parameter = 0, BehaviorNode[]? children = null,
        ConditionProgram? program = null, BehaviorTreeDefinition? subtree = null, bool ownsChildren = false)
    {
        Kind = kind; Parameter = parameter; Program = program; SubtreeDefinition = subtree;
        if (children is null || children.Length == 0) { Children = Array.Empty<BehaviorNode>(); return; }
        Children = ownsChildren ? children : (BehaviorNode[])children.Clone();
        foreach (var child in Children)
            if (child is null) throw new ArgumentException("Children cannot contain null.", nameof(children));
    }
    /// <summary>Creates an action using a nonnegative application identifier.</summary>
    public static BehaviorNode Action(int actionId)
    {
        if (actionId < 0) throw new ArgumentOutOfRangeException(nameof(actionId));
        return new(BehaviorNodeKind.Action, actionId);
    }
    /// <summary>Creates a predicate leaf using a nonnegative application identifier.</summary>
    public static BehaviorNode Condition(int predicateId)
    {
        if (predicateId < 0) throw new ArgumentOutOfRangeException(nameof(predicateId));
        return new(BehaviorNodeKind.Condition, predicateId);
    }
    /// <summary>Creates a leaf that evaluates a complete bounded logical program atomically.</summary>
    public static BehaviorNode Condition(ConditionProgram program) =>
        new(BehaviorNodeKind.Condition, program: program ?? throw new ArgumentNullException(nameof(program)));
    /// <summary>Creates a sequence that resumes its running child.</summary>
    public static BehaviorNode Sequence(params BehaviorNode[] children) => Composite(BehaviorNodeKind.Sequence, children);
    /// <summary>Creates a selector that resumes its running child.</summary>
    public static BehaviorNode Selector(params BehaviorNode[] children) => Composite(BehaviorNodeKind.Selector, children);
    /// <summary>Creates a sequence that explicitly rechecks prior children each evaluation cycle.</summary>
    public static BehaviorNode ReactiveSequence(params BehaviorNode[] children) => Composite(BehaviorNodeKind.ReactiveSequence, children);
    /// <summary>Creates a selector that explicitly rechecks prior children each evaluation cycle.</summary>
    public static BehaviorNode ReactiveSelector(params BehaviorNode[] children) => Composite(BehaviorNodeKind.ReactiveSelector, children);
    /// <summary>Creates an inverter.</summary>
    public static BehaviorNode Invert(BehaviorNode child) => new(BehaviorNodeKind.Invert, children: One(child), ownsChildren: true);
    /// <summary>Repeats a successful child exactly repeatCount times, ending early on failure.</summary>
    public static BehaviorNode Repeat(BehaviorNode child, int repeatCount)
    {
        if (repeatCount <= 0) throw new ArgumentOutOfRangeException(nameof(repeatCount));
        return new(BehaviorNodeKind.Repeat, repeatCount, One(child), ownsChildren: true);
    }
    /// <summary>Creates an independently executed copy of another compiled tree.</summary>
    public static BehaviorNode Subtree(BehaviorTreeDefinition definition) =>
        new(BehaviorNodeKind.Subtree, subtree: definition ?? throw new ArgumentNullException(nameof(definition)));
    /// <summary>Gets an immutable successful leaf.</summary>
    public static BehaviorNode Succeed { get; } = new(BehaviorNodeKind.Succeed);
    /// <summary>Gets an immutable unsuccessful leaf.</summary>
    public static BehaviorNode Fail { get; } = new(BehaviorNodeKind.Fail);
    private static BehaviorNode Composite(BehaviorNodeKind kind, BehaviorNode[] children) =>
        new(kind, children: children ?? throw new ArgumentNullException(nameof(children)));
    private static BehaviorNode[] One(BehaviorNode child) => new[] { child ?? throw new ArgumentNullException(nameof(child)) };
}
