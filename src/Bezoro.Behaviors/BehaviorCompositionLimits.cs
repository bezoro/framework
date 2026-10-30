namespace Bezoro.Behaviors;

/// <summary>Bounds composition before a runtime adopts any child.</summary>
public readonly struct BehaviorCompositionLimits
{
    /// <summary>Gets the supported nesting ceiling, which bounds managed adapter recursion.</summary>
    public const int SupportedDepth = 64;

    /// <summary>Creates explicit nesting and component-count limits.</summary>
    public BehaviorCompositionLimits(int maximumDepth, int maximumComponents = 4096)
    {
        if (maximumDepth <= 0 || maximumDepth > SupportedDepth)
            throw new ArgumentOutOfRangeException(nameof(maximumDepth));
        if (maximumComponents <= 0) throw new ArgumentOutOfRangeException(nameof(maximumComponents));
        MaximumDepth = maximumDepth;
        MaximumComponents = maximumComponents;
    }

    /// <summary>Gets the maximum nested runtime depth. A default value selects 32.</summary>
    public int MaximumDepth { get; }
    /// <summary>Gets the maximum number of composed runtime objects. A default value selects 4096.</summary>
    public int MaximumComponents { get; }
    internal int DepthLimit => MaximumDepth == 0 ? 32 : MaximumDepth;
    internal int ComponentLimit => MaximumComponents == 0 ? 4096 : MaximumComponents;
}
