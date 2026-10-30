using System.Runtime.CompilerServices;
using Xunit;

namespace Bezoro.StateMachines.Tests;

public sealed class StateMachineDefinitionTests
{
    [Fact]
    public void DefaultIdentifiersRepresentZeroAndRemainCompact()
    {
        Assert.Equal(0, default(StateId).Value);
        Assert.Equal(0, default(SignalId).Value);
        Assert.Equal(new StateId(0), default);
        Assert.Equal(new SignalId(0), default);
        Assert.Equal(4, Unsafe.SizeOf<StateId>());
        Assert.Equal(4, Unsafe.SizeOf<SignalId>());
        Assert.Equal(12, Unsafe.SizeOf<StateTransition>());
        Assert.Equal(16, Unsafe.SizeOf<StateStep>());
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    public void IdentifiersRejectNegativeValues(int value)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new StateId(value));
        Assert.Throws<ArgumentOutOfRangeException>(() => new SignalId(value));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(int.MaxValue)]
    public void IdentifierEqualityAndFormattingUseTheInteger(int value)
    {
        var state = new StateId(value);
        var signal = new SignalId(value);
        Assert.Equal(value, state.Value);
        Assert.Equal(value, signal.Value);
        Assert.True(state == new StateId(value));
        Assert.False(state != new StateId(value));
        Assert.True(signal == new SignalId(value));
        Assert.False(signal != new SignalId(value));
        Assert.Equal(value, state.GetHashCode());
        Assert.Equal(value, signal.GetHashCode());
        Assert.Equal(value.ToString(System.Globalization.CultureInfo.InvariantCulture), state.ToString());
        Assert.Equal(value.ToString(System.Globalization.CultureInfo.InvariantCulture), signal.ToString());
        Assert.False(state.Equals((object)signal));
    }

    [Fact]
    public void TransitionsCompareAllThreeIdentifiers()
    {
        var transition = Transition(1, 2, 3);
        Assert.Equal(transition, Transition(1, 2, 3));
        Assert.Equal(transition.GetHashCode(), Transition(1, 2, 3).GetHashCode());
        Assert.True(transition == Transition(1, 2, 3));
        Assert.True(transition != Transition(1, 2, 4));
        Assert.NotEqual(transition, Transition(0, 2, 3));
        Assert.NotEqual(transition, Transition(1, 0, 3));
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(-1, 1)]
    [InlineData(int.MinValue, 1)]
    [InlineData(1, -1)]
    public void InvalidCountsAreRejected(int states, int signals)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new StateMachineDefinition(states, signals, Array.Empty<StateTransition>()));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(int.MaxValue)]
    public void InitialStateMustBelongToTheDefinition(int initial)
    {
        var error = Assert.Throws<ArgumentOutOfRangeException>(() =>
            new StateMachineDefinition(1, 1, Array.Empty<StateTransition>(), new StateId(initial)));
        Assert.Equal("initialState", error.ParamName);
    }

    [Theory]
    [InlineData(2, 0, 0)]
    [InlineData(0, 2, 0)]
    [InlineData(0, 0, 2)]
    [InlineData(int.MaxValue, 0, 0)]
    [InlineData(0, int.MaxValue, 0)]
    [InlineData(0, 0, int.MaxValue)]
    public void EveryTransitionIdentifierMustBelongToTheDefinition(int source, int signal, int target)
    {
        var transitions = new[] { Transition(source, signal, target) };
        var error = Assert.Throws<ArgumentException>(() => new StateMachineDefinition(2, 2, transitions));
        Assert.Equal("transitions", error.ParamName);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void DuplicateSourceSignalPairsAreRejectedEvenWhenTargetsAgree(int target)
    {
        var transitions = new[] { Transition(0, 0, 0), Transition(1, 0, 1), Transition(0, 0, target) };
        var error = Assert.Throws<ArgumentException>(() => new StateMachineDefinition(2, 1, transitions));
        Assert.Equal("transitions", error.ParamName);
    }

    [Fact]
    public void ConstructionCopiesAndSortsWithoutMutatingTheInput()
    {
        var input = new[] { Transition(2, 1, 0), Transition(0, 1, 2), Transition(2, 0, 1), Transition(0, 0, 1) };
        var original = input.ToArray();
        var machine = new StateMachineDefinition(3, 2, input, new StateId(2));

        Assert.Equal(original, input);
        Assert.Equal(new StateId(2), machine.InitialState);
        Assert.Equal(3, machine.StateCount);
        Assert.Equal(2, machine.SignalCount);
        Assert.Equal(4, machine.TransitionCount);
        Assert.Equal(new[] { original[3], original[1], original[2], original[0] }, machine.Transitions.ToArray());

        input[0] = Transition(1, 1, 1);
        Assert.Equal(original[0], machine.Transitions[^1]);
        Assert.Equal(new StateId(0), machine.Step(new StateId(2), new SignalId(1)).CurrentState);
    }

    [Fact]
    public void MissingTransitionAndExplicitSelfTransitionAreDistinct()
    {
        var machine = new StateMachineDefinition(3, 3,
            new[] { Transition(0, 0, 1), Transition(1, 1, 1) });

        var changed = machine.Step(default, default);
        Assert.Equal(new StateId(0), changed.PreviousState);
        Assert.Equal(new StateId(1), changed.CurrentState);
        Assert.Equal(new SignalId(0), changed.Signal);
        Assert.True(changed.HasTransition);
        Assert.True(changed.HasStateChanged);

        var self = machine.Step(new StateId(1), new SignalId(1));
        Assert.Equal(new StateId(1), self.CurrentState);
        Assert.True(self.HasTransition);
        Assert.False(self.HasStateChanged);

        var missing = machine.Step(new StateId(1), new SignalId(2));
        Assert.Equal(new StateId(1), missing.PreviousState);
        Assert.Equal(new StateId(1), missing.CurrentState);
        Assert.Equal(new SignalId(2), missing.Signal);
        Assert.False(missing.HasTransition);
        Assert.False(missing.HasStateChanged);
        Assert.False(machine.TryGetTransition(new StateId(1), new SignalId(2), out var absent));
        Assert.Equal(default, absent);
    }

    [Fact]
    public void EvaluationDoesNotResetTheCallerToTheInitialState()
    {
        var machine = new StateMachineDefinition(3, 1,
            new[] { Transition(0, 0, 1), Transition(2, 0, 0) }, new StateId(2));
        var result = machine.Step(new StateId(0), default);
        Assert.Equal(new StateId(1), result.CurrentState);
        Assert.Equal(new StateId(2), machine.InitialState);
        Assert.Equal(new StateId(1), machine.Step(new StateId(0), default).CurrentState);
    }

    [Fact]
    public void NoSignalMachineHasNoLookupOrTransition()
    {
        var machine = new StateMachineDefinition(3, 0, Array.Empty<StateTransition>(), new StateId(2));
        Assert.Empty(machine.Transitions.ToArray());
        Assert.Equal(0, machine.TransitionCount);
        Assert.Throws<ArgumentOutOfRangeException>(() => machine.Step(default, default));
        Assert.Throws<ArgumentException>(() => new StateMachineDefinition(3, 0, new[] { Transition(0, 0, 1) }));
    }

    [Fact]
    public void EmptyTransitionTablePreservesAnyValidState()
    {
        var machine = new StateMachineDefinition(3, 2, Array.Empty<StateTransition>());
        for (int state = 0; state < 3; state++)
        for (int signal = 0; signal < 2; signal++)
        {
            var result = machine.Step(new StateId(state), new SignalId(signal));
            Assert.Equal(new StateId(state), result.CurrentState);
            Assert.False(result.HasTransition);
        }
    }

    [Fact]
    public void LargeDeclaredRangesDoNotRequireADenseTable()
    {
        var maximum = new StateId(int.MaxValue - 1);
        var signal = new SignalId(int.MaxValue - 1);
        var machine = new StateMachineDefinition(int.MaxValue, int.MaxValue,
            new[] { new StateTransition(maximum, signal, default) }, maximum);
        Assert.Equal(1, machine.TransitionCount);
        Assert.Equal(default, machine.Step(maximum, signal).CurrentState);
        Assert.False(machine.Step(default, default).HasTransition);
    }

    [Theory]
    [InlineData(2, 0, "currentState")]
    [InlineData(0, 2, "signal")]
    [InlineData(int.MaxValue, 0, "currentState")]
    [InlineData(0, int.MaxValue, "signal")]
    public void InvalidQueriesThrowInsteadOfBehavingLikeMissingTransitions(int state, int signal, string parameter)
    {
        var machine = new StateMachineDefinition(2, 2, Array.Empty<StateTransition>());
        var step = Assert.Throws<ArgumentOutOfRangeException>(() => machine.Step(new StateId(state), new SignalId(signal)));
        var lookup = Assert.Throws<ArgumentOutOfRangeException>(() => machine.TryGetTransition(new StateId(state), new SignalId(signal), out _));
        Assert.Equal(parameter, step.ParamName);
        Assert.Equal(parameter, lookup.ParamName);
    }

    [Fact]
    public void IdentifiersHaveNoOwnerAndCanMapToAnotherDefinitionsRanges()
    {
        var first = new StateMachineDefinition(2, 1, new[] { Transition(0, 0, 1) });
        var second = new StateMachineDefinition(2, 1, new[] { Transition(1, 0, 0) });
        var intermediate = first.Step(first.InitialState, default).CurrentState;
        Assert.Equal(new StateId(0), second.Step(intermediate, default).CurrentState);
    }

    [Fact]
    public void SeededSequencesMatchAnIndependentDictionaryModel()
    {
        var random = new Random(0x5A714);
        for (int trial = 0; trial < 100; trial++)
        {
            int states = random.Next(1, 65);
            int signals = random.Next(1, 17);
            var reference = new Dictionary<(int State, int Signal), int>();
            var transitions = new List<StateTransition>();
            for (int state = 0; state < states; state++)
            for (int signal = 0; signal < signals; signal++)
            {
                if (random.Next(4) != 0) continue;
                int target = random.Next(states);
                reference.Add((state, signal), target);
                transitions.Add(Transition(state, signal, target));
            }

            for (int index = transitions.Count - 1; index > 0; index--)
            {
                int other = random.Next(index + 1);
                (transitions[index], transitions[other]) = (transitions[other], transitions[index]);
            }

            int current = random.Next(states);
            var machine = new StateMachineDefinition(states, signals, transitions.ToArray(), new StateId(current));
            for (int step = 0; step < 1_000; step++)
            {
                int signal = random.Next(signals);
                bool matched = reference.TryGetValue((current, signal), out int target);
                if (!matched) target = current;
                var result = machine.Step(new StateId(current), new SignalId(signal));
                Assert.Equal(current, result.PreviousState.Value);
                Assert.Equal(signal, result.Signal.Value);
                Assert.Equal(matched, result.HasTransition);
                Assert.Equal(target != current, result.HasStateChanged);
                Assert.Equal(target, result.CurrentState.Value);
                Assert.Equal(matched, machine.TryGetTransition(new StateId(current), new SignalId(signal), out var transition));
                if (matched) Assert.Equal(target, transition.Target.Value);
                current = target;
            }
        }
    }

    [Fact]
    public async Task SharedDefinitionSupportsIndependentConcurrentAgents()
    {
        var machine = new StateMachineDefinition(3, 1,
            new[] { Transition(0, 0, 1), Transition(1, 0, 2), Transition(2, 0, 0) });
        var tasks = Enumerable.Range(0, 32).Select(worker => Task.Run(() =>
        {
            var state = new StateId(worker % 3);
            for (int step = 0; step < 10_000; step++) state = machine.Step(state, default).CurrentState;
            return state.Value;
        })).ToArray();
        var results = await Task.WhenAll(tasks);
        for (int worker = 0; worker < results.Length; worker++) Assert.Equal((worker + 10_000) % 3, results[worker]);
        Assert.Equal(default, machine.InitialState);
        Assert.Equal(3, machine.TransitionCount);
    }

    [Fact]
    public void WarmStepAndLookupAllocateNoManagedBytes()
    {
        var transitions = Enumerable.Range(0, 128).SelectMany(state => Enumerable.Range(0, 8)
            .Select(signal => Transition(state, signal, (state + signal + 1) % 128))).ToArray();
        var machine = new StateMachineDefinition(128, 8, transitions);
        var current = machine.InitialState;
        for (int index = 0; index < 4_096; index++) current = machine.Step(current, new SignalId(index % 8)).CurrentState;

        int checksum = 0;
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int index = 0; index < 100_000; index++)
        {
            var signal = new SignalId(index % 8);
            var result = machine.Step(current, signal);
            if (machine.TryGetTransition(current, signal, out var transition)) checksum += transition.Target.Value;
            current = result.CurrentState;
        }
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.True(checksum > 0);
        Assert.Equal(0, allocated);
    }

    private static StateTransition Transition(int source, int signal, int target) =>
        new(new StateId(source), new SignalId(signal), new StateId(target));
}
