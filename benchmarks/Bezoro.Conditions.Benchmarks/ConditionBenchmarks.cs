using BenchmarkDotNet.Attributes;

namespace Bezoro.Conditions.Benchmarks;

/// <summary>Measures evaluation of precompiled rules with reusable storage.</summary>
[MemoryDiagnoser]
public class ConditionBenchmarks
{
    private ConditionProgram _predicate = null!;
    private ConditionProgram _all = null!;
    private ConditionProgram _choose = null!;
    private bool[] _scratch = null!;
    private Flags _allTrue = new(63);
    private Flags _firstFalse = new(62);
    private Flags _selectedBranch = new(5);
    private Evaluator _evaluator;

    /// <summary>Compiles rules and reserves scratch outside measurement.</summary>
    [GlobalSetup]
    public void Setup()
    {
        _predicate = ConditionExpression.Predicate(0).Compile();
        _all = ConditionExpression.All(Enumerable.Range(0, 6).Select(ConditionExpression.Predicate).ToArray()).Compile();
        _choose = ConditionExpression.Choose(ConditionExpression.Predicate(0),
            ConditionExpression.Xor(ConditionExpression.Predicate(1), ConditionExpression.Predicate(2)),
            ConditionExpression.All(ConditionExpression.Predicate(3), ConditionExpression.Predicate(4))).Compile();
        _scratch = new bool[Math.Max(_all.RequiredStackSize, _choose.RequiredStackSize)];
    }

    /// <summary>Evaluates one predicate with no scratch.</summary>
    [Benchmark]
    public bool PredicateFastPath() => _predicate.Evaluate(in _allTrue, ref _evaluator, Span<bool>.Empty);

    /// <summary>Evaluates all six predicates in a successful conjunction.</summary>
    [Benchmark]
    public bool AllSixTrue() => _all.Evaluate(in _allTrue, ref _evaluator, _scratch);

    /// <summary>Stops at the first predicate while later instructions remain unvisited.</summary>
    [Benchmark]
    public bool FirstFalseShortCircuit() => _all.Evaluate(in _firstFalse, ref _evaluator, _scratch);

    /// <summary>Evaluates the selected conditional branch only.</summary>
    [Benchmark]
    public bool ConditionalBranch() => _choose.Evaluate(in _selectedBranch, ref _evaluator, _scratch);

    private readonly record struct Flags(ulong Value);
    private readonly struct Evaluator : IConditionEvaluator<Flags>
    {
        public bool Evaluate(int predicateId, in Flags context) => (context.Value & (1UL << predicateId)) != 0;
    }
}
