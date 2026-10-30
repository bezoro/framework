using BenchmarkDotNet.Attributes;

namespace Bezoro.StateMachines.Benchmarks;

[MemoryDiagnoser]
public class StateMachineBenchmarks
{
    private StateMachineDefinition _definition = null!;
    private StateId _query;

    [Params(16, 1_024, 65_536)]
    public int TransitionCount { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        int states = TransitionCount / 4;
        var transitions = new StateTransition[TransitionCount];
        for (int state = 0; state < states; state++)
        for (int signal = 0; signal < 4; signal++)
            transitions[state * 4 + signal] = new(new StateId(state), new SignalId(signal),
                new StateId(signal == 3 ? state : (state + 1) % states));
        _definition = new(states, 8, transitions);
        _query = new(states / 2);
    }

    [Benchmark]
    public StateStep MatchedStep() => _definition.Step(_query, new SignalId(1));

    [Benchmark]
    public StateStep MissingStep() => _definition.Step(_query, new SignalId(7));

    [Benchmark]
    public StateStep SelfTransition() => _definition.Step(_query, new SignalId(3));

    [Benchmark(OperationsPerInvoke = 1_024)]
    public StateId StepSequence()
    {
        var state = _definition.InitialState;
        for (int index = 0; index < 1_024; index++) state = _definition.Step(state, default).CurrentState;
        return state;
    }
}
