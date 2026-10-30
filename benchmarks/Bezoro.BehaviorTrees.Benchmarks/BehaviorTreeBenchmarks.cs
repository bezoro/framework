using BenchmarkDotNet.Attributes;

namespace Bezoro.BehaviorTrees.Benchmarks;

/// <summary>Compares cached paths and explicit priority reevaluation with reusable runners.</summary>
[MemoryDiagnoser]
public class BehaviorTreeBenchmarks
{
    private BehaviorTreeRunner<int> _memory = null!;
    private BehaviorTreeRunner<int> _reactive = null!;
    private Handler _handler;
    private int _context;
    /// <summary>Gets or sets inactive siblings or explicitly reevaluated priority conditions.</summary>
    [Params(16, 16_384)]
    public int SiblingCount { get; set; }
    /// <summary>Compiles both definitions and starts their action before measurement.</summary>
    [GlobalSetup]
    public void Setup()
    {
        var memory = new BehaviorNode[SiblingCount + 1];
        Array.Fill(memory, BehaviorNode.Condition(0)); memory[0] = BehaviorNode.Action(0);
        _memory = new(new BehaviorTreeDefinition(BehaviorNode.Sequence(memory), maximumNodes: SiblingCount + 2));
        var reactive = new BehaviorNode[SiblingCount + 1];
        Array.Fill(reactive, BehaviorNode.Condition(0)); reactive[SiblingCount] = BehaviorNode.Action(0);
        _reactive = new(new BehaviorTreeDefinition(BehaviorNode.ReactiveSelector(reactive), maximumNodes: SiblingCount + 2));
        _memory.Start(); _reactive.Start();
        var budget = new BehaviorBudget(SiblingCount + 2); _memory.Tick(ref _context, ref _handler, ref budget);
        budget = new BehaviorBudget(SiblingCount + 2); _reactive.Tick(ref _context, ref _handler, ref budget);
    }
    /// <summary>Updates one cached action without visiting inactive siblings.</summary>
    [Benchmark]
    public BehaviorTickResult MemoryRunningAction()
    { var budget = new BehaviorBudget(8); return _memory.Tick(ref _context, ref _handler, ref budget); }
    /// <summary>Explicitly rechecks all higher-priority conditions before resuming the action.</summary>
    [Benchmark]
    public BehaviorTickResult ReactivePriorityChecks()
    { var budget = new BehaviorBudget(SiblingCount + 2); return _reactive.Tick(ref _context, ref _handler, ref budget); }
    /// <summary>Stops and starts an execution using retained path storage.</summary>
    [Benchmark]
    public BehaviorStatus RestartPair()
    { _memory.Restart(ref _context, ref _handler); var budget = new BehaviorBudget(2); return _memory.Tick(ref _context, ref _handler, ref budget).Status; }
    private struct Handler : IBehaviorTreeHandler<int>
    {
        public bool Evaluate(int predicateId, in int context) => false;
        public void Enter(int node, int action, BehaviorActivation token, ref int context) { }
        public BehaviorStatus Tick(int node, int action, BehaviorActivation token, ref int context, ref BehaviorBudget budget) => BehaviorStatus.Running;
        public void Exit(int node, int action, BehaviorActivation token, BehaviorStatus result, ref int context) { }
        public void Abort(int node, int action, BehaviorActivation token, ref int context) { }
    }
}
