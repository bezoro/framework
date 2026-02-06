namespace Bezoro.ECS.Types;

/// <summary>
/// Defines the ordered execution stages for systems.
/// </summary>
public enum Stage
{
    Input,
    PreUpdate,
    Update,
    PostUpdate,
    Render
}
