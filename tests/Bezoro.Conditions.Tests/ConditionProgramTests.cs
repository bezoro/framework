using Xunit;

namespace Bezoro.Conditions.Tests;

public sealed class ConditionProgramTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Constants_RequireNoScratchOrPredicates(bool expected)
    {
        var program = (expected ? ConditionExpression.True : ConditionExpression.False).Compile();
        var evaluator = new ThrowingEvaluator();
        int context = 0;
        Assert.Equal(0, program.RequiredStackSize);
        Assert.Equal(1, program.InstructionCount);
        Assert.Equal(0, program.PredicateCount);
        Assert.Equal(expected, program.Evaluate(in context, ref evaluator, Span<bool>.Empty));
    }

    [Fact]
    public void OnePredicate_UsesNoScratchAndAcceptsFullIntRange()
    {
        var program = ConditionExpression.Predicate(int.MaxValue).Compile(maximumNodes: 1, maximumInstructions: 1);
        var evaluator = new IdentityEvaluator();
        int context = int.MaxValue;
        Assert.Equal(0, program.RequiredStackSize);
        Assert.Equal(1, program.PredicateCount);
        Assert.True(program.Evaluate(in context, ref evaluator, Span<bool>.Empty));
        Assert.Equal(1, evaluator.Calls);
    }

    [Fact]
    public void Groups_CopyTheirInputsAndHaveMathematicalEmptyIdentities()
    {
        var children = new[] { ConditionExpression.Predicate(0), ConditionExpression.Predicate(1) };
        var expression = ConditionExpression.All(children);
        children[0] = ConditionExpression.False;
        var bits = new Bits(3);
        var evaluator = new BitEvaluator();
        var program = expression.Compile();
        Assert.True(program.Evaluate(in bits, ref evaluator, new bool[program.RequiredStackSize]));
        Assert.True(ConditionExpression.All().Compile().Evaluate(in bits, ref evaluator, Span<bool>.Empty));
        Assert.False(ConditionExpression.Any().Compile().Evaluate(in bits, ref evaluator, Span<bool>.Empty));
        Assert.True(ConditionExpression.All(ReadOnlySpan<ConditionExpression>.Empty).Compile()
            .Evaluate(in bits, ref evaluator, Span<bool>.Empty));
    }

    [Fact]
    public void ShortCircuit_SkipsThrowingChildrenAndUnselectedConditionalBranches()
    {
        var invalid = ConditionExpression.Predicate(999);
        var cases = new[]
        {
            (ConditionExpression.All(ConditionExpression.False, invalid), false),
            (ConditionExpression.Any(ConditionExpression.True, invalid), true),
            (ConditionExpression.Choose(ConditionExpression.True, ConditionExpression.True, invalid), true),
            (ConditionExpression.Choose(ConditionExpression.False, invalid, ConditionExpression.False), false)
        };
        var evaluator = new ThrowingEvaluator();
        int context = 0;
        foreach (var (expression, expected) in cases)
        {
            var program = expression.Compile();
            Assert.Equal(expected, program.Evaluate(in context, ref evaluator, new bool[program.RequiredStackSize]));
        }
    }

    [Fact]
    public void XorAndEqual_EvaluateBothOperandsInOrder()
    {
        var bits = new Bits(1);
        var evaluator = new RecordingEvaluator();
        var left = ConditionExpression.Predicate(0);
        var right = ConditionExpression.Predicate(1);
        var xor = ConditionExpression.Xor(left, right).Compile();
        Assert.True(xor.Evaluate(in bits, evaluator, new bool[xor.RequiredStackSize]));
        Assert.Equal(new[] { 0, 1 }, evaluator.Visited);
        evaluator.Visited.Clear();
        var equal = ConditionExpression.Equal(left, right).Compile();
        Assert.False(equal.Evaluate(in bits, evaluator, new bool[equal.RequiredStackSize]));
        Assert.Equal(new[] { 0, 1 }, evaluator.Visited);
    }

    [Fact]
    public void TruthTables_CoverAllOperatorsAndNestedBranchStackShapes()
    {
        var a = ConditionExpression.Predicate(0);
        var b = ConditionExpression.Predicate(1);
        var c = ConditionExpression.Predicate(2);
        var cases = new (ConditionExpression Expression, Func<bool, bool, bool, bool> Expected)[]
        {
            (a.And(b), (x,y,z) => x && y),
            (a.Or(b), (x,y,z) => x || y),
            (ConditionExpression.Not(a), (x,y,z) => !x),
            (ConditionExpression.Xor(a,b), (x,y,z) => x ^ y),
            (ConditionExpression.Equal(a,b), (x,y,z) => x == y),
            (ConditionExpression.Choose(a,b,c), (x,y,z) => x ? y : z),
            (ConditionExpression.Xor(a, ConditionExpression.Choose(b, c, ConditionExpression.Not(c))),
                (x,y,z) => x ^ (y ? z : !z)),
            (ConditionExpression.Choose(a, b.And(c), b.Or(c)), (x,y,z) => x ? y && z : y || z),
            (ConditionExpression.All(a, b, c), (x,y,z) => x && y && z),
            (ConditionExpression.Any(a, b, c), (x,y,z) => x || y || z)
        };
        foreach (var (expression, expected) in cases)
        {
            var program = expression.Compile();
            var scratch = new bool[program.RequiredStackSize];
            var evaluator = new BitEvaluator();
            for (ulong value = 0; value < 8; value++)
            {
                var bits = new Bits(value);
                Assert.Equal(expected((value & 1) != 0, (value & 2) != 0, (value & 4) != 0),
                    program.Evaluate(in bits, ref evaluator, scratch));
            }
        }
    }

    [Fact]
    public void Evaluation_DoesNotCachePredicatesOrContexts()
    {
        var predicate = ConditionExpression.Predicate(0);
        var program = ConditionExpression.All(predicate, predicate).Compile();
        var evaluator = new AlternatingEvaluator();
        int context = 0;
        Assert.False(program.Evaluate(in context, ref evaluator, new bool[program.RequiredStackSize]));
        Assert.Equal(2, evaluator.Calls);
        var bits = new Bits(1);
        var bitEvaluator = new BitEvaluator();
        var leaf = predicate.Compile();
        Assert.True(leaf.Evaluate(in bits, ref bitEvaluator, Span<bool>.Empty));
        bits = new Bits(0);
        Assert.False(leaf.Evaluate(in bits, ref bitEvaluator, Span<bool>.Empty));
    }

    [Fact]
    public void ScratchFailure_PrecedesPredicateCallsAndLeavesScratchUntouched()
    {
        var program = ConditionExpression.Xor(ConditionExpression.Predicate(0), ConditionExpression.Predicate(1)).Compile();
        Assert.Equal(2, program.RequiredStackSize);
        var scratch = new[] { true };
        var evaluator = new RecordingEvaluator();
        var context = new Bits(3);
        Assert.Throws<ArgumentException>(() => program.Evaluate(in context, evaluator, scratch));
        Assert.Empty(evaluator.Visited);
        Assert.True(scratch[0]);
    }

    [Fact]
    public void PredicateFailure_DoesNotPoisonFutureEvaluations()
    {
        var program = ConditionExpression.All(ConditionExpression.Predicate(0), ConditionExpression.Predicate(999)).Compile();
        var evaluator = new RecordingEvaluator();
        var bits = new Bits(1);
        var scratch = new bool[program.RequiredStackSize];
        Assert.Throws<InvalidOperationException>(() => program.Evaluate(in bits, evaluator, scratch));
        bits = new Bits(0);
        Assert.False(program.Evaluate(in bits, evaluator, scratch));
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(-1, 1)]
    [InlineData(1, 0)]
    [InlineData(1, -1)]
    public void Compile_RejectsNonpositiveLimits(int nodes, int instructions) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => ConditionExpression.True.Compile(nodes, instructions));

    [Fact]
    public void Compile_EnforcesOccurrenceAndInstructionLimitsOnSharedExpressions()
    {
        var leaf = ConditionExpression.Predicate(0);
        var expression = ConditionExpression.Xor(leaf, leaf);
        Assert.Throws<ArgumentException>(() => expression.Compile(maximumNodes: 2));
        Assert.Throws<ArgumentException>(() => expression.Compile(maximumInstructions: 2));
        var program = expression.Compile(maximumNodes: 3, maximumInstructions: 3);
        Assert.Equal(3, program.ExpressionNodeCount);
        Assert.Equal(3, program.InstructionCount);
        for (int i = 0; i < 32; i++) expression = ConditionExpression.Xor(expression, expression);
        Assert.Throws<ArgumentException>(() => expression.Compile(maximumNodes: 100));
    }

    [Fact]
    public void DeepCompilationAndEvaluation_AreIterativeAndUseAnExactStackBound()
    {
        ConditionExpression unary = ConditionExpression.Predicate(0);
        for (int i = 0; i < 20_000; i++) unary = ConditionExpression.Not(unary);
        var program = unary.Compile(maximumNodes: 20_001, maximumInstructions: 20_001);
        Assert.Equal(1, program.RequiredStackSize);
        var evaluator = new BitEvaluator();
        var context = new Bits(1);
        Assert.True(program.Evaluate(in context, ref evaluator, new bool[1]));

        ConditionExpression binary = ConditionExpression.Predicate(1);
        for (int i = 0; i < 4_000; i++) binary = ConditionExpression.Xor(ConditionExpression.Predicate(0), binary);
        program = binary.Compile(maximumNodes: 8_001, maximumInstructions: 8_001);
        Assert.Equal(4_001, program.RequiredStackSize);
        context = new Bits(0);
        Assert.False(program.Evaluate(in context, ref evaluator, new bool[program.RequiredStackSize]));
    }

    [Fact]
    public void StaticConstantNormalization_RemovesDeadStorageWithoutSkippingEarlierPredicates()
    {
        var predicate = ConditionExpression.Predicate(0);
        var expensive = ConditionExpression.Xor(predicate, predicate);
        var constant = ConditionExpression.All(ConditionExpression.False, expensive).Compile(1, 1);
        Assert.Equal(0, constant.RequiredStackSize);
        Assert.Equal(0, constant.PredicateCount);
        var selected = ConditionExpression.Choose(ConditionExpression.True, predicate, expensive).Compile(1, 1);
        Assert.Equal(0, selected.RequiredStackSize);
        Assert.Equal(1, selected.PredicateCount);
        Assert.Equal(1, ConditionExpression.All(ConditionExpression.True, predicate).Compile().InstructionCount);
        Assert.Equal(1, ConditionExpression.Any(ConditionExpression.False, predicate).Compile().InstructionCount);
        var program = ConditionExpression.All(predicate, ConditionExpression.False, expensive).Compile();
        var evaluator = new RecordingEvaluator();
        var bits = new Bits(1);
        Assert.False(program.Evaluate(in bits, evaluator, new bool[program.RequiredStackSize]));
        Assert.Equal(new[] { 0 }, evaluator.Visited);
        Assert.Equal(1, program.PredicateCount);
        // Invalid branches are still rejected while constructing the expression.
        Assert.Throws<ArgumentException>(() => ConditionExpression.All(ConditionExpression.False, null!));
    }

    [Fact]
    public void Factories_RejectInvalidPredicatesAndChildren()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ConditionExpression.Predicate(-1));
        Assert.Throws<ArgumentNullException>(() => ConditionExpression.All((ConditionExpression[])null!));
        Assert.Throws<ArgumentNullException>(() => ConditionExpression.Any((ConditionExpression[])null!));
        Assert.Throws<ArgumentException>(() => ConditionExpression.All(new ConditionExpression[] { null! }));
        Assert.Throws<ArgumentNullException>(() => ConditionExpression.Not(null!));
        Assert.Throws<ArgumentNullException>(() => ConditionExpression.Xor(null!, ConditionExpression.True));
        Assert.Throws<ArgumentNullException>(() => ConditionExpression.Equal(ConditionExpression.True, null!));
        Assert.Throws<ArgumentNullException>(() => ConditionExpression.Choose(null!, ConditionExpression.True, ConditionExpression.False));
        Assert.Throws<ArgumentNullException>(() => ConditionExpression.Choose(ConditionExpression.True, null!, ConditionExpression.False));
        Assert.Throws<ArgumentNullException>(() => ConditionExpression.Choose(ConditionExpression.True, ConditionExpression.False, null!));
    }

    [Fact]
    public void NullReferenceEvaluator_IsRejectedEvenForAConstantProgram()
    {
        var program = ConditionExpression.True.Compile();
        int context = 0;
        Assert.Throws<ArgumentNullException>(() => program.Evaluate(in context, (IConditionEvaluator<int>)null!, Array.Empty<bool>()));
    }

    [Fact]
    public void SeededPrograms_MatchIndependentRecursiveModelAndPredicateVisitOrder()
    {
        var random = new Random(90219);
        for (int sample = 0; sample < 400; sample++)
        {
            var model = GenerateModel(random, depth: 5);
            var program = model.Expression.Compile();
            var scratch = new bool[program.RequiredStackSize];
            var evaluator = new RecordingEvaluator();
            var expectedVisits = new List<int>();
            for (int input = 0; input < 32; input++)
            {
                var bits = new Bits((ulong)random.Next(0, 65536));
                expectedVisits.Clear();
                evaluator.Visited.Clear();
                bool expected = model.Evaluate(bits.Value, expectedVisits);
                bool actual = program.Evaluate(in bits, evaluator, scratch);
                Assert.Equal(expected, actual);
                Assert.Equal(expectedVisits, evaluator.Visited);
            }
        }
    }

    [Fact]
    public void SharedPrograms_SupportIndependentConcurrentEvaluation()
    {
        var expression = ConditionExpression.Predicate(0).And(ConditionExpression.Not(ConditionExpression.Predicate(1)))
            .Or(ConditionExpression.Xor(ConditionExpression.Predicate(2), ConditionExpression.Predicate(3)));
        var program = expression.Compile();
        Parallel.For(0, 32, worker =>
        {
            var scratch = new bool[program.RequiredStackSize];
            var evaluator = new BitEvaluator();
            for (int i = 0; i < 10_000; i++)
            {
                ulong value = (ulong)(worker * 10_000 + i);
                var bits = new Bits(value);
                bool expected = ((value & 1) != 0 && (value & 2) == 0) || (((value & 4) != 0) ^ ((value & 8) != 0));
                Assert.Equal(expected, program.Evaluate(in bits, ref evaluator, scratch));
            }
        });
    }

    [Fact]
    public void WarmEvaluation_AllocatesNoManagedMemoryForStructOrReferenceEvaluators()
    {
        var expression = ConditionExpression.Choose(ConditionExpression.Predicate(0),
            ConditionExpression.Predicate(1).And(ConditionExpression.Not(ConditionExpression.Predicate(2))),
            ConditionExpression.Xor(ConditionExpression.Predicate(3), ConditionExpression.Predicate(4)));
        var program = expression.Compile();
        var leaf = ConditionExpression.Predicate(0).Compile();
        var scratch = new bool[program.RequiredStackSize];
        var evaluator = new BitEvaluator();
        IConditionEvaluator<Bits> reference = new StatelessEvaluator();
        var bits = new Bits(123);
        for (int i = 0; i < 2_000; i++)
        {
            program.Evaluate(in bits, ref evaluator, scratch);
            program.Evaluate(in bits, reference, scratch);
            leaf.Evaluate(in bits, ref evaluator, Span<bool>.Empty);
        }
        long before = GC.GetAllocatedBytesForCurrentThread();
        bool result = false;
        for (int i = 0; i < 100_000; i++)
        {
            bits = new Bits((ulong)i);
            result ^= program.Evaluate(in bits, ref evaluator, scratch);
            result ^= program.Evaluate(in bits, reference, scratch);
            result ^= leaf.Evaluate(in bits, ref evaluator, Span<bool>.Empty);
        }
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Equal(0, allocated);
        GC.KeepAlive(result);
    }

    private readonly record struct Bits(ulong Value);

    private struct BitEvaluator : IConditionEvaluator<Bits>
    {
        public bool Evaluate(int predicateId, in Bits context) => (context.Value & (1UL << predicateId)) != 0;
    }

    private sealed class StatelessEvaluator : IConditionEvaluator<Bits>
    {
        public bool Evaluate(int predicateId, in Bits context) => (context.Value & (1UL << predicateId)) != 0;
    }

    private sealed class RecordingEvaluator : IConditionEvaluator<Bits>
    {
        internal readonly List<int> Visited = new();
        public bool Evaluate(int predicateId, in Bits context)
        {
            Visited.Add(predicateId);
            if (predicateId == 999) throw new InvalidOperationException("This predicate must not be reached.");
            return (context.Value & (1UL << predicateId)) != 0;
        }
    }

    private struct IdentityEvaluator : IConditionEvaluator<int>
    {
        internal int Calls;
        public bool Evaluate(int predicateId, in int context) { Calls++; return predicateId == context; }
    }

    private struct AlternatingEvaluator : IConditionEvaluator<int>
    {
        internal int Calls;
        public bool Evaluate(int predicateId, in int context) => ++Calls % 2 != 0;
    }

    private struct ThrowingEvaluator : IConditionEvaluator<int>
    {
        public bool Evaluate(int predicateId, in int context) => throw new InvalidOperationException("No predicate should run.");
    }

    private static Model GenerateModel(Random random, int depth)
    {
        if (depth == 0 || random.Next(5) == 0)
        {
            int id = random.Next(18);
            return id < 16 ? new Model(2, id) : new Model(id == 16 ? 0 : 1);
        }
        int kind = random.Next(3, 9);
        int count = kind is 3 or 4 ? random.Next(0, 5) : kind == 5 ? 1 : kind == 8 ? 3 : 2;
        var children = new Model[count];
        for (int i = 0; i < count; i++) children[i] = GenerateModel(random, depth - 1);
        return new Model(kind, children: children);
    }

    private sealed class Model
    {
        private readonly int _kind;
        private readonly int _id;
        private readonly Model[] _children;
        internal ConditionExpression Expression { get; }
        internal Model(int kind, int id = 0, Model[]? children = null)
        {
            _kind = kind; _id = id; _children = children ?? Array.Empty<Model>();
            var expressions = Array.ConvertAll(_children, child => child.Expression);
            Expression = kind switch
            {
                0 => ConditionExpression.False,
                1 => ConditionExpression.True,
                2 => ConditionExpression.Predicate(id),
                3 => ConditionExpression.All(expressions),
                4 => ConditionExpression.Any(expressions),
                5 => ConditionExpression.Not(expressions[0]),
                6 => ConditionExpression.Xor(expressions[0], expressions[1]),
                7 => ConditionExpression.Equal(expressions[0], expressions[1]),
                8 => ConditionExpression.Choose(expressions[0], expressions[1], expressions[2]),
                _ => throw new InvalidOperationException()
            };
        }
        internal bool Evaluate(ulong bits, List<int> visited)
        {
            switch (_kind)
            {
                case 0: return false;
                case 1: return true;
                case 2: visited.Add(_id); return (bits & (1UL << _id)) != 0;
                case 3:
                    foreach (var child in _children) if (!child.Evaluate(bits, visited)) return false;
                    return true;
                case 4:
                    foreach (var child in _children) if (child.Evaluate(bits, visited)) return true;
                    return false;
                case 5: return !_children[0].Evaluate(bits, visited);
                case 6: return _children[0].Evaluate(bits, visited) ^ _children[1].Evaluate(bits, visited);
                case 7: return _children[0].Evaluate(bits, visited) == _children[1].Evaluate(bits, visited);
                case 8: return _children[0].Evaluate(bits, visited) ? _children[1].Evaluate(bits, visited) : _children[2].Evaluate(bits, visited);
                default: throw new InvalidOperationException();
            }
        }
    }
}
