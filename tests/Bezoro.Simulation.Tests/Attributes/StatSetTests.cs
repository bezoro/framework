using System.Reflection;
using Bezoro.Simulation.Attributes;
using Xunit;

namespace Bezoro.Simulation.Tests.Attributes;

public sealed class StatSetTests
{
    private static readonly StatId Damage = new(1);
    private static readonly StatId Speed = new(2);

    [Fact]
    public void Constructor_CopiesDefinitionsAndRejectsInvalidConfiguration()
    {
        StatDefinition[] definitions = [new(Damage, 10), new(Speed, 5)];
        var set = new StatSet(definitions, 2);
        definitions[0] = new(Damage, 1000);

        Assert.Equal(10, set.GetValue(Damage));
        Assert.Equal(2, set.StatCount);
        Assert.Equal(2, set.ModifierCapacity);
        Assert.Equal(2, set.AvailableModifierSlots);
        Assert.Throws<ArgumentOutOfRangeException>(() => new StatSet([], -1));
        Assert.Throws<ArgumentException>(() => new StatSet(new StatDefinition[1], 0));
        Assert.Throws<ArgumentException>(() => new StatSet(new StatDefinition[] { new(Damage, 1), new(Damage, 2) }, 0));
    }

    [Fact]
    public void EmptySetAndZeroModifierCapacityHaveExplicitBehavior()
    {
        var empty = new StatSet([], 0);
        Assert.Equal(0, empty.StatCount);
        Assert.False(empty.Contains(default));
        Assert.Equal(0, empty.ClearModifiers());
        Assert.Throws<KeyNotFoundException>(() => empty.GetValue(Damage));

        var set = Create(0);
        Assert.Equal(10, set.GetValue(Damage));
        Assert.False(set.TryAddModifier(Damage, default, out var handle));
        Assert.True(handle.IsDefault);
        Assert.Throws<InvalidOperationException>(() => set.AddModifier(Damage, default));
    }

    [Fact]
    public void AddUpdateAndRemoveChangeOnlyTheirTargetAttribute()
    {
        var set = Create(2);
        var damage = set.AddModifier(Damage, new(StatModifierOperation.Add, 5));
        var speed = set.AddModifier(Speed, new(StatModifierOperation.Multiply, 2));

        Assert.Equal(15, set.GetValue(Damage));
        Assert.Equal(10, set.GetValue(Speed));
        Assert.True(set.TryUpdateModifier(damage, new(StatModifierOperation.AddPercent, 0.5)));
        Assert.Equal(15, set.GetValue(Damage));
        Assert.True(set.TryRemoveModifier(damage));
        Assert.Equal(10, set.GetValue(Damage));
        Assert.Equal(10, set.GetValue(Speed));
        Assert.Equal(1, set.GetModifierCount(Speed));
        Assert.True(set.TryGetModifier(speed, out var id, out var modifier));
        Assert.Equal(Speed, id);
        Assert.Equal(2, modifier.Value);
    }

    [Fact]
    public void CapacityFailureLeavesValuesHandlesAndCountsUntouched()
    {
        var set = Create(1);
        var existing = set.AddModifier(Damage, new(StatModifierOperation.Add, 7));

        Assert.False(set.TryAddModifier(Speed, default, out var rejected));
        Assert.True(rejected.IsDefault);
        Assert.Throws<InvalidOperationException>(() => set.AddModifier(Speed, default));
        Assert.True(set.TryGetModifier(existing, out _, out _));
        Assert.Equal(17, set.GetValue(Damage));
        Assert.Equal(5, set.GetValue(Speed));
        Assert.Equal(1, set.ModifierCount);
        Assert.Equal(0, set.AvailableModifierSlots);
    }

    [Fact]
    public void ForeignDefaultAndRemovedHandlesCannotAccessOrChangeModifiers()
    {
        var first = Create(1);
        var second = Create(1);
        var old = first.AddModifier(Damage, default);
        var foreign = second.AddModifier(Damage, default);

        Assert.False(first.TryRemoveModifier(default));
        Assert.False(first.TryRemoveModifier(foreign));
        Assert.False(first.TryUpdateModifier(foreign, default));
        Assert.False(first.TryGetModifier(foreign, out _, out _));
        Assert.True(first.TryRemoveModifier(old));
        var replacement = first.AddModifier(Damage, new(StatModifierOperation.Add, 12));
        Assert.NotEqual(old, replacement);
        Assert.False(first.TryRemoveModifier(old));
        Assert.False(first.TryUpdateModifier(old, default));
        Assert.False(first.TryGetModifier(old, out var invalidId, out _));
        Assert.False(invalidId.IsValid);
        Assert.True(first.TryGetModifier(replacement, out _, out _));
        Assert.Equal(22, first.GetValue(Damage));
    }

    [Fact]
    public void SlotReuseAndUpdatesPreserveInsertionOrderForOverrideTies()
    {
        var set = Create(3);
        var first = set.AddModifier(Damage, new(StatModifierOperation.Override, 11, 3));
        var middle = set.AddModifier(Damage, new(StatModifierOperation.Override, 12, 3));
        var last = set.AddModifier(Damage, new(StatModifierOperation.Override, 13, 3));
        Assert.True(set.TryRemoveModifier(middle));
        var newest = set.AddModifier(Damage, new(StatModifierOperation.Override, 14, 3));

        Assert.Equal(14, set.GetValue(Damage));
        Assert.True(set.TryUpdateModifier(first, new(StatModifierOperation.Override, 99, 3)));
        Assert.Equal(14, set.GetValue(Damage));
        Assert.True(set.TryRemoveModifier(newest));
        Assert.Equal(13, set.GetValue(Damage));
        Assert.True(set.TryRemoveModifier(last));
        Assert.Equal(99, set.GetValue(Damage));
    }

    [Fact]
    public void CopyModifiersUsesInsertionOrderAndRejectsSmallDestinationBeforeWriting()
    {
        var set = Create(3);
        var first = set.AddModifier(Damage, new(StatModifierOperation.Add, 1));
        set.AddModifier(Damage, new(StatModifierOperation.Add, 2));
        Assert.True(set.TryRemoveModifier(first));
        set.AddModifier(Damage, new(StatModifierOperation.Add, 3));
        var small = new[] { new StatModifier(StatModifierOperation.Add, 999) };
        Assert.Throws<ArgumentException>(() => set.CopyModifiersTo(Damage, small));
        Assert.Equal(999, small[0].Value);

        Span<StatModifier> copied = stackalloc StatModifier[3];
        Assert.Equal(2, set.CopyModifiersTo(Damage, copied));
        Assert.Equal(2, copied[0].Value);
        Assert.Equal(3, copied[1].Value);
    }

    [Fact]
    public void ClearOneOrAllAttributesInvalidatesHandlesAndRestoresBaseValues()
    {
        var set = Create(4);
        var first = set.AddModifier(Damage, new(StatModifierOperation.Add, 1));
        var second = set.AddModifier(Damage, new(StatModifierOperation.Add, 2));
        var speed = set.AddModifier(Speed, new(StatModifierOperation.Add, 3));

        Assert.Equal(2, set.ClearModifiers(Damage));
        Assert.False(set.TryGetModifier(first, out _, out _));
        Assert.False(set.TryGetModifier(second, out _, out _));
        Assert.True(set.TryGetModifier(speed, out _, out _));
        Assert.Equal(10, set.GetValue(Damage));
        Assert.Equal(8, set.GetValue(Speed));
        Assert.Equal(1, set.ClearModifiers());
        Assert.False(set.TryRemoveModifier(speed));
        Assert.Equal(0, set.ModifierCount);
        Assert.Equal(4, set.AvailableModifierSlots);
        Assert.Equal(5, set.GetValue(Speed));
        Assert.Equal(0, set.ClearModifiers());
    }

    [Fact]
    public void InvalidMutationDoesNotChangeThePreviousDefinitionOrModifiers()
    {
        var set = Create(2);
        var handle = set.AddModifier(Damage, new(StatModifierOperation.Add, 1));
        Assert.Equal(11, set.GetValue(Damage));

        Assert.Throws<ArgumentOutOfRangeException>(() => set.SetBaseValue(Damage, double.NaN));
        Assert.Throws<ArgumentException>(() => set.SetDefinition(default));
        Assert.Throws<KeyNotFoundException>(() => set.SetDefinition(new(new(3), 2)));
        Assert.Throws<KeyNotFoundException>(() => set.AddModifier(new(3), default));
        Assert.Throws<ArgumentException>(() => set.AddModifier(default, default));
        Assert.Equal(11, set.GetValue(Damage));
        Assert.Equal(10, set.GetDefinition(Damage).BaseValue);
        Assert.True(set.TryGetModifier(handle, out _, out _));
        Assert.Equal(1, set.ModifierCount);
    }

    [Fact]
    public void DefinitionAndBaseChangesInvalidateTheirCachedResult()
    {
        var set = Create(2);
        set.AddModifier(Damage, new(StatModifierOperation.Multiply, 2));
        for (int iteration = 0; iteration < 1000; iteration++) Assert.Equal(20, set.Evaluate(Damage).Value);
        set.SetBaseValue(Damage, 20);
        Assert.Equal(40, set.GetValue(Damage));
        set.SetDefinition(new(Damage, 20, 0, 25));
        Assert.Equal(25, set.GetValue(Damage));
        Assert.True(set.Evaluate(Damage).WasClamped);
        Assert.Equal(5, set.GetValue(Speed));
    }

    [Fact]
    public void OverflowDoesNotPoisonCachedValueAndRemovalRestoresFiniteBase()
    {
        var set = Create(3);
        var first = set.AddModifier(Damage, new(StatModifierOperation.Multiply, double.MaxValue));
        Assert.True(set.Evaluate(Damage).WasSaturated);
        Assert.Equal(double.MaxValue, set.GetValue(Damage));
        var second = set.AddModifier(Damage, new(StatModifierOperation.Multiply, double.MaxValue));
        var zero = set.AddModifier(Damage, new(StatModifierOperation.Multiply, 0));
        Assert.Equal(0, set.GetValue(Damage));
        Assert.True(set.Evaluate(Damage).WasSaturated);
        Assert.True(set.TryRemoveModifier(first));
        Assert.Equal(0, set.GetValue(Damage));
        Assert.False(set.Evaluate(Damage).WasSaturated);
        Assert.True(set.TryRemoveModifier(second));
        Assert.True(set.TryRemoveModifier(zero));
        Assert.Equal(10, set.GetValue(Damage));
    }

    [Fact]
    public void ExhaustedSlotGenerationIsRetiredWithoutResurrectingAHandle()
    {
        var set = Create(1);
        var original = set.AddModifier(Damage, default);
        // Exercise the final generation without billions of mutations. Handles remain opaque in the public API.
        var slots = (Array)typeof(StatSet).GetField("_slots", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(set)!;
        object slot = slots.GetValue(0)!;
        slot.GetType().GetField("Generation", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(slot, uint.MaxValue);
        slots.SetValue(slot, 0);
        var owner = typeof(StatModifierHandle).GetProperty("Owner", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(original);
        var finalHandle = (StatModifierHandle)Activator.CreateInstance(typeof(StatModifierHandle),
            BindingFlags.NonPublic | BindingFlags.Instance, null, [owner, 0, uint.MaxValue], null)!;

        Assert.False(set.TryRemoveModifier(original));
        Assert.True(set.TryRemoveModifier(finalHandle));
        Assert.False(set.TryAddModifier(Damage, default, out _));
        Assert.Equal(0, set.ModifierCount);
        Assert.Equal(0, set.AvailableModifierSlots);
        Assert.Equal(10, set.GetValue(Damage));
    }

    [Fact]
    public void RepeatedRandomMutationsMatchPureEvaluationOfLiveInsertionOrder()
    {
        var random = new Random(31337);
        var set = Create(24);
        var live = new List<(StatModifierHandle Handle, StatId Id, StatModifier Modifier)>();
        for (int step = 0; step < 1000; step++)
        {
            var id = random.Next(2) == 0 ? Damage : Speed;
            switch (random.Next(4))
            {
                case 0:
                    var addition = new StatModifier((StatModifierOperation)random.Next(4), random.Next(0, 9) / 4.0, random.Next(3));
                    if (set.TryAddModifier(id, addition, out var handle)) live.Add((handle, id, addition));
                    break;
                case 1 when live.Count > 0:
                    int remove = random.Next(live.Count);
                    Assert.True(set.TryRemoveModifier(live[remove].Handle));
                    live.RemoveAt(remove);
                    break;
                case 2 when live.Count > 0:
                    int update = random.Next(live.Count);
                    var replacement = new StatModifier(StatModifierOperation.Add, random.Next(-20, 21));
                    Assert.True(set.TryUpdateModifier(live[update].Handle, replacement));
                    live[update] = (live[update].Handle, live[update].Id, replacement);
                    break;
                case 3:
                    set.SetBaseValue(id, random.Next(-100, 101));
                    break;
            }

            foreach (var stat in new[] { Damage, Speed })
            {
                var expected = live.Where(item => item.Id == stat).Select(item => item.Modifier).ToArray();
                Assert.Equal(StatCalculator.Evaluate(set.GetDefinition(stat), expected).Value, set.GetValue(stat));
                Assert.Equal(expected.Length, set.GetModifierCount(stat));
            }
            Assert.Equal(live.Count, set.ModifierCount);
        }
    }

    [Fact]
    public async Task IndependentSetsCanBeCreatedAndUsedConcurrently()
    {
        var sets = await Task.WhenAll(Enumerable.Range(0, 16).Select(index => Task.Run(() =>
        {
            var set = Create(2);
            var handle = set.AddModifier(Damage, new(StatModifierOperation.Add, index));
            Assert.Equal(10 + index, set.GetValue(Damage));
            return (set, handle);
        })));

        for (int i = 0; i < sets.Length; i++)
            for (int j = 0; j < sets.Length; j++)
                Assert.Equal(i == j, sets[i].set.TryGetModifier(sets[j].handle, out _, out _));
    }

    private static StatSet Create(int capacity) => new(new StatDefinition[] { new(Damage, 10), new(Speed, 5) }, capacity);
}
