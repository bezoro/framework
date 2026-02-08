namespace Bezoro.ECS.Types;

/// <summary>
///     Marker used with observer registration for component remove events.
/// </summary>
public readonly struct OnRemove<T> where T : struct { }
