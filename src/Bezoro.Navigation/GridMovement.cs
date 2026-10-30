namespace Bezoro.Navigation;

/// <summary>Specifies the edges available between neighboring cells.</summary>
public enum GridMovement
{
    /// <summary>Moves horizontally or vertically at a base cost of 1000.</summary>
    FourWay = 0,
    /// <summary>Also permits diagonal steps at a base cost of 1414.</summary>
    EightWay = 1
}
