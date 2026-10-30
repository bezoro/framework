namespace Bezoro.Conditions;

/// <summary>Evaluates application predicates against a typed context.</summary>
/// <typeparam name="TContext">The application-owned context.</typeparam>
/// <remarks>
/// Predicates should read the supplied context without changing the condition program or its execution scratch.
/// They may use ordinary C# comparisons, methods and expressions. The program does not cache their results.
/// </remarks>
public interface IConditionEvaluator<TContext>
{
    /// <summary>Evaluates one nonnegative application-defined predicate identifier.</summary>
    bool Evaluate(int predicateId, in TContext context);
}
