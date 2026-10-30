namespace Bezoro.Conditions;

/// <summary>Builds an immutable Boolean expression to compile once and share between agents.</summary>
/// <remarks>Construction copies child collections. Evaluation belongs to <see cref="ConditionProgram" />.</remarks>
public sealed class ConditionExpression
{
    internal ConditionKind Kind { get; }
    internal int PredicateId { get; }
    internal ConditionExpression[] Children { get; }

    private ConditionExpression(ConditionKind kind, int predicateId, ConditionExpression[] children)
    {
        Kind = kind;
        PredicateId = predicateId;
        Children = children;
    }

    /// <summary>Gets the constant true expression.</summary>
    public static ConditionExpression True { get; } = new(ConditionKind.True, 0, Array.Empty<ConditionExpression>());
    /// <summary>Gets the constant false expression.</summary>
    public static ConditionExpression False { get; } = new(ConditionKind.False, 0, Array.Empty<ConditionExpression>());

    /// <summary>References a nonnegative application predicate. Its meaning is supplied by the evaluator.</summary>
    public static ConditionExpression Predicate(int predicateId)
    {
        if (predicateId < 0) throw new ArgumentOutOfRangeException(nameof(predicateId));
        return new(ConditionKind.Predicate, predicateId, Array.Empty<ConditionExpression>());
    }

    /// <summary>Requires every child, left to right, stopping at the first false result. An empty list is true.</summary>
    public static ConditionExpression All(params ConditionExpression[] expressions)
    {
        if (expressions is null) throw new ArgumentNullException(nameof(expressions));
        return Group(ConditionKind.All, expressions);
    }

    /// <summary>Requires every child, left to right, stopping at the first false result. An empty span is true.</summary>
    public static ConditionExpression All(ReadOnlySpan<ConditionExpression> expressions) => Group(ConditionKind.All, expressions);

    /// <summary>Requires any child, left to right, stopping at the first true result. An empty list is false.</summary>
    public static ConditionExpression Any(params ConditionExpression[] expressions)
    {
        if (expressions is null) throw new ArgumentNullException(nameof(expressions));
        return Group(ConditionKind.Any, expressions);
    }

    /// <summary>Requires any child, left to right, stopping at the first true result. An empty span is false.</summary>
    public static ConditionExpression Any(ReadOnlySpan<ConditionExpression> expressions) => Group(ConditionKind.Any, expressions);

    /// <summary>Negates one child.</summary>
    public static ConditionExpression Not(ConditionExpression expression)
    {
        if (expression is null) throw new ArgumentNullException(nameof(expression));
        if (expression.Kind == ConditionKind.True) return False;
        if (expression.Kind == ConditionKind.False) return True;
        return new(ConditionKind.Not, 0, new[] { expression });
    }
    /// <summary>Evaluates both children and succeeds when their results differ.</summary>
    public static ConditionExpression Xor(ConditionExpression left, ConditionExpression right)
    {
        ValidatePair(left, right);
        if (left.Kind == ConditionKind.False) return right;
        if (right.Kind == ConditionKind.False) return left;
        if (left.Kind == ConditionKind.True) return Not(right);
        if (right.Kind == ConditionKind.True) return Not(left);
        return new(ConditionKind.Xor, 0, new[] { left, right });
    }
    /// <summary>Evaluates both children and succeeds when their results match.</summary>
    public static ConditionExpression Equal(ConditionExpression left, ConditionExpression right)
    {
        ValidatePair(left, right);
        if (left.Kind == ConditionKind.True) return right;
        if (right.Kind == ConditionKind.True) return left;
        if (left.Kind == ConditionKind.False) return Not(right);
        if (right.Kind == ConditionKind.False) return Not(left);
        return new(ConditionKind.Equal, 0, new[] { left, right });
    }

    /// <summary>Evaluates the test and only the selected branch, like the C# conditional operator.</summary>
    public static ConditionExpression Choose(ConditionExpression test, ConditionExpression whenTrue, ConditionExpression whenFalse)
    {
        if (test is null) throw new ArgumentNullException(nameof(test));
        if (whenTrue is null) throw new ArgumentNullException(nameof(whenTrue));
        if (whenFalse is null) throw new ArgumentNullException(nameof(whenFalse));
        if (test.Kind == ConditionKind.True) return whenTrue;
        if (test.Kind == ConditionKind.False) return whenFalse;
        return new(ConditionKind.Choose, 0, new[] { test, whenTrue, whenFalse });
    }

    /// <summary>Builds a short-circuit conjunction with this expression first.</summary>
    public ConditionExpression And(ConditionExpression other) => All(this, other);
    /// <summary>Builds a short-circuit disjunction with this expression first.</summary>
    public ConditionExpression Or(ConditionExpression other) => Any(this, other);

    /// <summary>Compiles using explicit occurrence and instruction limits, without recursive traversal.</summary>
    /// <remarks>
    /// Shared expression objects are expanded per occurrence. Both limits include constants and predicates.
    /// Limits must be positive. Exceeding either limit throws before evaluation can begin.
    /// </remarks>
    public ConditionProgram Compile(int maximumNodes = 4096, int maximumInstructions = 16384) =>
        ConditionProgram.Compile(this, maximumNodes, maximumInstructions);

    private static ConditionExpression Group(ConditionKind kind, ReadOnlySpan<ConditionExpression> expressions)
    {
        for (int index = 0; index < expressions.Length; index++)
            if (expressions[index] is null) throw new ArgumentException("Child expressions cannot be null.", nameof(expressions));

        ConditionKind identity = kind == ConditionKind.All ? ConditionKind.True : ConditionKind.False;
        ConditionKind absorber = kind == ConditionKind.All ? ConditionKind.False : ConditionKind.True;
        int count = 0;
        ConditionExpression? first = null;
        foreach (var child in expressions)
        {
            if (child.Kind == identity) continue;
            first ??= child;
            count++;
            if (child.Kind == absorber) break;
        }
        if (count == 0) return kind == ConditionKind.All ? True : False;
        if (count == 1) return first!;

        var children = new ConditionExpression[count];
        int destination = 0;
        foreach (var child in expressions)
        {
            if (child.Kind == identity) continue;
            children[destination++] = child;
            if (child.Kind == absorber) break;
        }
        return new(kind, 0, children);
    }

    private static void ValidatePair(ConditionExpression left, ConditionExpression right)
    {
        if (left is null) throw new ArgumentNullException(nameof(left));
        if (right is null) throw new ArgumentNullException(nameof(right));
    }

}

internal enum ConditionKind : byte
{
    False, True, Predicate, All, Any, Not, Xor, Equal, Choose
}
