namespace Bezoro.Navigation;

/// <summary>Controls topology and the maximum number of nodes whose outgoing edges are expanded.</summary>
/// <remarks>The default value selects four-way movement, forbids corner cutting, and has no practical expansion limit.</remarks>
public readonly struct GridPathOptions
{
    private readonly bool _configured;
    private readonly int _maxExpandedNodes;

    /// <summary>Creates search options. A zero expansion budget still permits the trivial start-equals-goal path.</summary>
    public GridPathOptions(GridMovement movement = GridMovement.FourWay, bool allowCornerCutting = false,
        int maxExpandedNodes = int.MaxValue)
    {
        if (movement != GridMovement.FourWay && movement != GridMovement.EightWay)
            throw new ArgumentOutOfRangeException(nameof(movement));
        if (maxExpandedNodes < 0) throw new ArgumentOutOfRangeException(nameof(maxExpandedNodes));
        Movement = movement;
        AllowCornerCutting = allowCornerCutting;
        _maxExpandedNodes = maxExpandedNodes;
        _configured = true;
    }

    /// <summary>Gets the movement topology.</summary>
    public GridMovement Movement { get; }

    /// <summary>Gets whether diagonal steps may pass between blocked orthogonal neighbors.</summary>
    public bool AllowCornerCutting { get; }

    /// <summary>Gets the maximum number of nodes whose outgoing edges may be examined.</summary>
    public int MaxExpandedNodes => _configured ? _maxExpandedNodes : int.MaxValue;
}
