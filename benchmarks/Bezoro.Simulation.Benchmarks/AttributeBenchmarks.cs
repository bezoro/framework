using BenchmarkDotNet.Attributes;
using Bezoro.Simulation.Attributes;

namespace Bezoro.Simulation.Benchmarks;

[MemoryDiagnoser]
public class AttributeBenchmarks
{
    private readonly StatId _damage = new(1);
    private StatSet _set = null!;
    private StatModifier[] _modifiers = null!;
    private double _baseValue;

    [Params(0, 8, 64)]
    public int ModifierCount { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _set = new(new StatDefinition[] { new(_damage, 100, 0, 100_000) }, ModifierCount);
        _modifiers = new StatModifier[ModifierCount];
        for (int i = 0; i < ModifierCount; i++)
        {
            _modifiers[i] = i % 2 == 0
                ? new(StatModifierOperation.Add, 0.5)
                : new(StatModifierOperation.AddPercent, 0.01);
            _set.AddModifier(_damage, _modifiers[i]);
        }
        _set.GetValue(_damage);
        _baseValue = 100;
    }

    [Benchmark(Baseline = true)]
    public double CachedRead() => _set.GetValue(_damage);

    [Benchmark]
    public double ChangeBaseAndEvaluate()
    {
        _baseValue = _baseValue == 100 ? 101 : 100;
        _set.SetBaseValue(_damage, _baseValue);
        return _set.GetValue(_damage);
    }

    [Benchmark]
    public StatEvaluation PureSpanEvaluation() =>
        StatCalculator.Evaluate(new(_damage, 100, 0, 100_000), _modifiers);
}
