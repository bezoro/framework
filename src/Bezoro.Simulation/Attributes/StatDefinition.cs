namespace Bezoro.Simulation.Attributes;

/// <summary>Defines an attribute's base value and finite inclusive output bounds.</summary>
public readonly struct StatDefinition
{
    /// <summary>Creates an attribute definition. Bounds apply after modifiers and overrides.</summary>
    public StatDefinition(StatId id, double baseValue, double minimum = double.MinValue, double maximum = double.MaxValue)
    {
        if (!id.IsValid) throw new ArgumentException("A stat identifier must be initialized.", nameof(id));
        RequireFinite(baseValue, nameof(baseValue));
        RequireFinite(minimum, nameof(minimum));
        RequireFinite(maximum, nameof(maximum));
        if (minimum > maximum) throw new ArgumentException("Minimum must not exceed maximum.", nameof(minimum));
        Id = id;
        BaseValue = baseValue;
        Minimum = minimum;
        Maximum = maximum;
    }

    /// <summary>Gets the attribute identifier.</summary>
    public StatId Id { get; }

    /// <summary>Gets the unmodified value. It may lie outside the final output bounds.</summary>
    public double BaseValue { get; }

    /// <summary>Gets the inclusive lower bound of the evaluated value.</summary>
    public double Minimum { get; }

    /// <summary>Gets the inclusive upper bound of the evaluated value.</summary>
    public double Maximum { get; }

    /// <summary>Creates a definition with the same identifier and bounds and a different base value.</summary>
    public StatDefinition WithBaseValue(double baseValue) => new(Id, baseValue, Minimum, Maximum);

    internal static void RequireFinite(double value, string name)
    {
        if (double.IsNaN(value) || double.IsInfinity(value))
            throw new ArgumentOutOfRangeException(name, "The value must be finite.");
    }
}
