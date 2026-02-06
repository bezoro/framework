using Bezoro.ECS.Abstractions;

namespace Bezoro.ECS.Types;

/// <summary>
/// Marker used with observer registration for component add events.
/// </summary>
public readonly struct OnAdd<T> where T : struct, IComponent
{
}
