using BenchmarkDotNet.Attributes;
using Bezoro.BehaviorTrees;
using Bezoro.StateMachines;

namespace Bezoro.Behaviors.Benchmarks;

/// <summary>Measures warmed composed execution with fixed contexts and a shared work allowance.</summary>
[MemoryDiagnoser]
public class CompositionBenchmarks
{
    private TreeBehavior<Context> _nested = null!;
    private Context _context;

    /// <summary>Gets or sets the configured runtime depth.</summary>
    [Params(1, 8, 32)]
    public int Depth { get; set; }

    /// <summary>Builds graphs once before timing.</summary>
    [GlobalSetup]
    public void Setup()
    {
        var definition = new BehaviorTreeDefinition(BehaviorNode.Action(0));
        var handler = new Handler();
        var limits = new BehaviorCompositionLimits(64, 256);
        _nested = new TreeBehavior<Context>(definition, handler);
        for (int i = 1; i < Depth; i++)
            _nested = new TreeBehavior<Context>(definition,
                bindings: new[] { new TreeBehaviorBinding<Context>(0, _nested) }, limits: limits);
        _nested.Start(ref _context);

    }

    /// <summary>Advances one quantum; visits include every nested evaluator charged to the allowance.</summary>
    [Benchmark]
    public int NestedBudgetOne()
    {
        var budget = new BehaviorBudget(1);
        _nested.Tick(ref _context, ref budget);
        return budget.Consumed;
    }

    /// <summary>Advances a composed running path with enough allowance for this configured depth.</summary>
    [Benchmark]
    public int NestedFullPath()
    {
        var budget = new BehaviorBudget(128);
        _nested.Tick(ref _context, ref budget);
        return budget.Consumed;
    }

    /// <summary>Stops the graph after measurement.</summary>
    [GlobalCleanup]
    public void Cleanup()
    { _nested.Stop(ref _context); }

}

/// <summary>Measures explicit transitions in a fixed two-state composed machine.</summary>
[MemoryDiagnoser]
public class StateMachineCompositionBenchmarks
{
    private StateMachineBehavior<Context> _machine = null!;
    private Context _context;

    [GlobalSetup]
    public void Setup()
    {
        var definition = new BehaviorTreeDefinition(BehaviorNode.Action(0));
        var handler = new Handler();
        var flat = new StateMachineDefinition(2, 1, new[]
        {
            new StateTransition(new StateId(0), new SignalId(0), new StateId(1)),
            new StateTransition(new StateId(1), new SignalId(0), new StateId(0))
        });
        _machine = new StateMachineBehavior<Context>(flat, new[]
        {
            new StateBehaviorBinding<Context>(new StateId(0), new TreeBehavior<Context>(definition, handler)),
            new StateBehaviorBinding<Context>(new StateId(1), new TreeBehavior<Context>(definition, handler))
        });
        _machine.Start(ref _context);
    }

    /// <summary>Transitions between two owned trees and updates the new state once.</summary>
    [Benchmark]
    public long TransitionAndTick()
    {
        _machine.Dispatch(new SignalId(0), ref _context);
        var budget = new BehaviorBudget(4);
        _machine.Tick(ref _context, ref budget);
        return _context.Ticks;
    }

    [GlobalCleanup]
    public void Cleanup() => _machine.Stop(ref _context);
}

internal struct Context { internal long Ticks; }
internal sealed class Handler : IBehaviorTreeHandler<Context>
{
    public bool Evaluate(int predicateId, in Context context) => true;
    public void Enter(int nodeId, int actionId, BehaviorActivation activation, ref Context context) { }
    public BehaviorStatus Tick(int nodeId, int actionId, BehaviorActivation activation, ref Context context, ref BehaviorBudget budget)
    { context.Ticks++; return BehaviorStatus.Running; }
    public void Exit(int nodeId, int actionId, BehaviorActivation activation, BehaviorStatus result, ref Context context) { }
    public void Abort(int nodeId, int actionId, BehaviorActivation activation, ref Context context) { }
}
