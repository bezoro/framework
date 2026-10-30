using Bezoro.StateMachines;

namespace Bezoro.Behaviors;

internal static class BindingValidation<TContext>
{
    internal static void ValidateCount(int count, BehaviorCompositionLimits limits)
    {
        if (count >= limits.ComponentLimit)
            throw new ArgumentOutOfRangeException(nameof(limits), "The bindings exceed the composition's component limit.");
    }

    internal static StateBehaviorBinding<TContext>[] Copy(ReadOnlySpan<StateBehaviorBinding<TContext>> bindings, int stateCount, int signalCount)
    {
        var result = bindings.ToArray();
        Array.Sort(result, BindingOrder.Instance);
        for (int i = 0; i < result.Length; i++)
        {
            var binding = result[i];
            if (binding.Child is null || binding.State.Value < 0 || binding.State.Value >= stateCount ||
                (i > 0 && result[i - 1].State == binding.State) ||
                (binding.SuccessSignal.HasValue && binding.SuccessSignal.Value.Value >= signalCount) ||
                (binding.FailureSignal.HasValue && binding.FailureSignal.Value.Value >= signalCount))
                throw new ArgumentException("Bindings require unique in-range states, non-null children, and in-range completion signals.", nameof(bindings));
        }
        return result;
    }

    internal static Behavior<TContext>[] Children(StateBehaviorBinding<TContext>[] bindings)
    {
        if (bindings.Length == 0) return Array.Empty<Behavior<TContext>>();
        var result = new Behavior<TContext>[bindings.Length];
        for (int i = 0; i < result.Length; i++) result[i] = bindings[i].Child;
        return result;
    }

    internal static StateId[] CopyStates(ReadOnlySpan<StateId> states, int stateCount, string parameterName)
    {
        var result = states.ToArray();
        Array.Sort(result, StateOrder.Instance);
        for (int i = 0; i < result.Length; i++)
            if (result[i].Value < 0 || result[i].Value >= stateCount || (i > 0 && result[i] == result[i - 1]))
                throw new ArgumentException("Terminal state IDs must be unique and in range.", parameterName);
        return result;
    }

    private sealed class BindingOrder : IComparer<StateBehaviorBinding<TContext>>
    {
        internal static readonly BindingOrder Instance = new();
        public int Compare(StateBehaviorBinding<TContext> left, StateBehaviorBinding<TContext> right) => left.State.Value.CompareTo(right.State.Value);
    }
    private sealed class StateOrder : IComparer<StateId>
    {
        internal static readonly StateOrder Instance = new();
        public int Compare(StateId left, StateId right) => left.Value.CompareTo(right.Value);
    }
}
