namespace Bezoro.Conditions;

/// <summary>An immutable, bounded Boolean program with short-circuit control flow.</summary>
/// <remarks>
/// Evaluation reads only this program. Each concurrent evaluation owns its scratch and evaluator state.
/// Single predicates and constants need no scratch; composite expressions use the exact compiled stack bound.
/// Evaluation is atomic with respect to the caller's scheduling budget and never caches predicate results.
/// </remarks>
public sealed class ConditionProgram
{
    private readonly Instruction[] _instructions;

    private ConditionProgram(Instruction[] instructions, int requiredStackSize, int expressionNodeCount)
    {
        _instructions = instructions;
        RequiredStackSize = instructions.Length == 1 ? 0 : requiredStackSize;
        ExpressionNodeCount = expressionNodeCount;
        foreach (var instruction in instructions)
            if (instruction.Code == OpCode.Predicate) PredicateCount++;
    }

    /// <summary>Gets the number of temporary Boolean values required by a composite evaluation.</summary>
    public int RequiredStackSize { get; }
    /// <summary>Gets the compiled instruction count. Unselected short-circuit branches are skipped.</summary>
    public int InstructionCount => _instructions.Length;
    /// <summary>Gets the syntactic predicate count, an upper bound that includes unselected branches.</summary>
    public int PredicateCount { get; }
    /// <summary>Gets the number of expression occurrences compiled, including shared expressions at each use.</summary>
    public int ExpressionNodeCount { get; }

    /// <summary>Evaluates with a reference evaluator or an already boxed interface value.</summary>
    /// <remarks>Use the generic ref overload with struct evaluators to avoid boxing at the call site.</remarks>
    /// <exception cref="ArgumentNullException">The evaluator is null.</exception>
    /// <exception cref="ArgumentException">The scratch span is smaller than <see cref="RequiredStackSize" />.</exception>
    public bool Evaluate<TContext>(in TContext context, IConditionEvaluator<TContext> evaluator, Span<bool> scratch)
        => Evaluate<TContext, IConditionEvaluator<TContext>>(in context, ref evaluator, scratch);

    /// <summary>Evaluates without boxing a struct evaluator and preserves changes to its own bookkeeping fields.</summary>
    /// <remarks>
    /// Predicates execute in source order. All/Any and Choose skip unselected branches; Xor/Equal evaluate both operands.
    /// A predicate exception propagates. Scratch contents after evaluation or failure are unspecified.
    /// The context and program are never changed by the interpreter; application evaluators own their own effects.
    /// </remarks>
    /// <exception cref="ArgumentNullException">A reference evaluator is null.</exception>
    /// <exception cref="ArgumentException">The scratch span is smaller than <see cref="RequiredStackSize" />.</exception>
    public bool Evaluate<TContext, TEvaluator>(in TContext context, ref TEvaluator evaluator, Span<bool> scratch)
        where TEvaluator : IConditionEvaluator<TContext>
    {
        if (EvaluatorType<TEvaluator>.IsReferenceType && evaluator is null)
            throw new ArgumentNullException(nameof(evaluator));
        if (scratch.Length < RequiredStackSize)
            throw new ArgumentException("The scratch span is smaller than the compiled stack requirement.", nameof(scratch));

        if (_instructions.Length == 1)
        {
            var instruction = _instructions[0];
            return instruction.Code == OpCode.Predicate
                ? evaluator.Evaluate(instruction.Operand, in context)
                : instruction.Operand != 0;
        }

        int instructionIndex = 0;
        int stackCount = 0;
        while (instructionIndex < _instructions.Length)
        {
            var instruction = _instructions[instructionIndex];
            switch (instruction.Code)
            {
                case OpCode.Constant:
                    scratch[stackCount++] = instruction.Operand != 0;
                    instructionIndex++;
                    break;
                case OpCode.Predicate:
                    scratch[stackCount++] = evaluator.Evaluate(instruction.Operand, in context);
                    instructionIndex++;
                    break;
                case OpCode.Not:
                    scratch[stackCount - 1] = !scratch[stackCount - 1];
                    instructionIndex++;
                    break;
                case OpCode.Xor:
                    scratch[stackCount - 2] ^= scratch[stackCount - 1];
                    stackCount--;
                    instructionIndex++;
                    break;
                case OpCode.Equal:
                    scratch[stackCount - 2] = scratch[stackCount - 2] == scratch[stackCount - 1];
                    stackCount--;
                    instructionIndex++;
                    break;
                case OpCode.BranchFalse:
                case OpCode.BranchTrue:
                    bool shouldBranch = scratch[stackCount - 1] == (instruction.Code == OpCode.BranchTrue);
                    if (shouldBranch) instructionIndex = instruction.Operand;
                    else { stackCount--; instructionIndex++; }
                    break;
                case OpCode.BranchFalsePop:
                    instructionIndex = scratch[--stackCount] ? instructionIndex + 1 : instruction.Operand;
                    break;
                case OpCode.Jump:
                    instructionIndex = instruction.Operand;
                    break;
                default:
                    throw new InvalidOperationException("The compiled condition contains an unsupported instruction.");
            }
        }
        return scratch[0];
    }

    internal static ConditionProgram Compile(ConditionExpression expression, int maximumNodes, int maximumInstructions)
    {
        if (maximumNodes <= 0) throw new ArgumentOutOfRangeException(nameof(maximumNodes));
        if (maximumInstructions <= 0) throw new ArgumentOutOfRangeException(nameof(maximumInstructions));
        return new Compiler(maximumNodes, maximumInstructions).Compile(expression);
    }

    private static class EvaluatorType<TEvaluator>
    {
        // Keep value-type null checks out of the executed IL path even on a runtime
        // that does not eliminate the boxing implied by an unconstrained null test.
        internal static readonly bool IsReferenceType = !typeof(TEvaluator).IsValueType;
    }

    private enum OpCode : byte
    {
        Constant, Predicate, Not, Xor, Equal, BranchFalse, BranchTrue, BranchFalsePop, Jump
    }

    private readonly struct Instruction
    {
        internal Instruction(OpCode code, int operand = 0) { Code = code; Operand = operand; }
        internal OpCode Code { get; }
        internal int Operand { get; }
    }

    private struct Frame
    {
        internal Frame(ConditionExpression expression, int baseDepth)
        {
            Expression = expression;
            BaseDepth = baseDepth;
            Stage = 0;
            Child = 0;
            Branch = -1;
            End = -1;
        }
        internal ConditionExpression Expression;
        internal int BaseDepth;
        internal int Stage;
        internal int Child;
        internal int Branch;
        internal int End;
    }

    private sealed class Compiler
    {
        private readonly int _maximumNodes;
        private readonly int _maximumInstructions;
        private readonly List<Instruction> _instructions;
        private readonly Stack<Frame> _frames;
        private int _nodes;
        private int _depth;
        private int _maximumDepth;

        internal Compiler(int maximumNodes, int maximumInstructions)
        {
            _maximumNodes = maximumNodes;
            _maximumInstructions = maximumInstructions;
            _instructions = new List<Instruction>(Math.Min(64, maximumInstructions));
            _frames = new Stack<Frame>(Math.Min(32, maximumNodes));
        }

        internal ConditionProgram Compile(ConditionExpression expression)
        {
            _frames.Push(new Frame(expression, 0));
            while (_frames.Count > 0)
            {
                var frame = _frames.Pop();
                var node = frame.Expression;
                if (frame.Stage == 0)
                {
                    if (_nodes >= _maximumNodes)
                        throw new ArgumentException("The expression exceeds the configured node-occurrence limit.", nameof(expression));
                    _nodes++;
                    if (node.Kind == ConditionKind.Predicate)
                    {
                        Emit(OpCode.Predicate, node.PredicateId);
                        PushValue();
                        continue;
                    }
                    if (node.Kind is ConditionKind.True or ConditionKind.False || node.Children.Length == 0)
                    {
                        Emit(OpCode.Constant, node.Kind is ConditionKind.True or ConditionKind.All ? 1 : 0);
                        PushValue();
                        continue;
                    }
                    frame.Stage = 1;
                    _frames.Push(frame);
                    _frames.Push(new Frame(node.Children[0], _depth));
                    continue;
                }

                switch (node.Kind)
                {
                    case ConditionKind.All:
                    case ConditionKind.Any:
                        if (frame.Child + 1 < node.Children.Length)
                        {
                            // Pending branch targets form a linked patch list in their operands.
                            frame.Branch = Emit(node.Kind == ConditionKind.All ? OpCode.BranchFalse : OpCode.BranchTrue, frame.Branch);
                            _depth--;
                            frame.Child++;
                            _frames.Push(frame);
                            _frames.Push(new Frame(node.Children[frame.Child], _depth));
                        }
                        else PatchChain(frame.Branch, _instructions.Count);
                        break;
                    case ConditionKind.Not:
                        Emit(OpCode.Not);
                        break;
                    case ConditionKind.Xor:
                    case ConditionKind.Equal:
                        if (frame.Stage == 1)
                        {
                            frame.Stage = 2;
                            _frames.Push(frame);
                            _frames.Push(new Frame(node.Children[1], _depth));
                        }
                        else
                        {
                            Emit(node.Kind == ConditionKind.Xor ? OpCode.Xor : OpCode.Equal);
                            _depth--;
                        }
                        break;
                    case ConditionKind.Choose:
                        if (frame.Stage == 1)
                        {
                            frame.Branch = Emit(OpCode.BranchFalsePop);
                            _depth--;
                            frame.Stage = 2;
                            _frames.Push(frame);
                            _frames.Push(new Frame(node.Children[1], _depth));
                        }
                        else if (frame.Stage == 2)
                        {
                            frame.End = Emit(OpCode.Jump);
                            Patch(frame.Branch, _instructions.Count);
                            _depth = frame.BaseDepth;
                            frame.Stage = 3;
                            _frames.Push(frame);
                            _frames.Push(new Frame(node.Children[2], _depth));
                        }
                        else Patch(frame.End, _instructions.Count);
                        break;
                    default:
                        throw new InvalidOperationException("The expression compiler reached an unsupported node.");
                }
            }
            if (_depth != 1) throw new InvalidOperationException("The condition compiler produced an invalid stack shape.");
            return new ConditionProgram(_instructions.ToArray(), _maximumDepth, _nodes);
        }

        private void PushValue()
        {
            _depth++;
            if (_depth > _maximumDepth) _maximumDepth = _depth;
        }

        private int Emit(OpCode code, int operand = 0)
        {
            if (_instructions.Count >= _maximumInstructions)
                throw new ArgumentException("The expression exceeds the configured instruction limit.");
            int index = _instructions.Count;
            _instructions.Add(new Instruction(code, operand));
            return index;
        }

        private void Patch(int index, int target) =>
            _instructions[index] = new Instruction(_instructions[index].Code, target);

        private void PatchChain(int index, int target)
        {
            while (index >= 0)
            {
                int next = _instructions[index].Operand;
                Patch(index, target);
                index = next;
            }
        }
    }
}
