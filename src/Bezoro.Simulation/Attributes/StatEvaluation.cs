namespace Bezoro.Simulation.Attributes;

/// <summary>The finite result of evaluating one attribute.</summary>
public readonly struct StatEvaluation
{
    internal StatEvaluation(double value, double unclampedValue, bool wasSaturated)
    {
        Value = value;
        UnclampedValue = unclampedValue;
        WasSaturated = wasSaturated;
    }

    /// <summary>Gets the value after the definition's bounds are applied.</summary>
    public double Value { get; }

    /// <summary>Gets the finite formula or override value before the definition's bounds are applied.</summary>
    public double UnclampedValue { get; }

    /// <summary>Gets whether the definition's bounds changed the computed value.</summary>
    public bool WasClamped => Value != UnclampedValue;

    /// <summary>Gets whether arithmetic overflow was saturated while computing a non-overridden formula.</summary>
    public bool WasSaturated { get; }
}
