namespace Bezoro.Simulation.Attributes;

/// <summary>Pure, allocation-free evaluation of ordered attribute modifiers.</summary>
public static class StatCalculator
{
    /// <summary>
    /// Evaluates (base + sum of additions) * (1 + sum of fractional percentages) * product of factors.
    /// The highest-priority override replaces that result; ties use the last input modifier. Bounds apply last.
    /// Each overflowing arithmetic operation saturates to the corresponding finite double limit.
    /// </summary>
    public static StatEvaluation Evaluate(StatDefinition definition, ReadOnlySpan<StatModifier> modifiers)
    {
        if (!definition.Id.IsValid)
            throw new ArgumentException("The stat definition must be initialized.", nameof(definition));
        var accumulator = new Accumulator();
        foreach (ref readonly var modifier in modifiers) accumulator.Add(modifier);
        return accumulator.Complete(definition);
    }

    internal struct Accumulator
    {
        private double _addition;
        private double _percentage;
        private double _factor;
        private double _overrideValue;
        private int _overridePriority;
        private bool _hasFactor;
        private bool _hasOverride;
        private bool _wasSaturated;

        internal void Add(StatModifier modifier)
        {
            switch (modifier.Operation)
            {
                case StatModifierOperation.Add:
                    _addition = AddFinite(_addition, modifier.Value);
                    break;
                case StatModifierOperation.AddPercent:
                    _percentage = AddFinite(_percentage, modifier.Value);
                    break;
                case StatModifierOperation.Multiply:
                    _factor = MultiplyFinite(_hasFactor ? _factor : 1, modifier.Value);
                    _hasFactor = true;
                    break;
                case StatModifierOperation.Override:
                    if (!_hasOverride || modifier.Priority >= _overridePriority)
                    {
                        _overrideValue = modifier.Value;
                        _overridePriority = modifier.Priority;
                        _hasOverride = true;
                    }
                    break;
            }
        }

        internal StatEvaluation Complete(StatDefinition definition)
        {
            double raw;
            if (_hasOverride) raw = _overrideValue;
            else
            {
                raw = AddFinite(definition.BaseValue, _addition);
                raw = MultiplyFinite(raw, AddFinite(1, _percentage));
                if (_hasFactor) raw = MultiplyFinite(raw, _factor);
            }
            return new(Math.Min(definition.Maximum, Math.Max(definition.Minimum, raw)), raw,
                !_hasOverride && _wasSaturated);
        }

        private double AddFinite(double left, double right) => KeepFinite(left + right);
        private double MultiplyFinite(double left, double right) => KeepFinite(left * right);

        private double KeepFinite(double value)
        {
            if (!double.IsInfinity(value)) return value;
            _wasSaturated = true;
            return value > 0 ? double.MaxValue : double.MinValue;
        }
    }
}
