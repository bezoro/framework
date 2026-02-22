namespace Bezoro.ECS.Types;

/// <summary>
///     Snapshot record for one component value.
/// </summary>
/// <param name="ComponentType">Component runtime type.</param>
/// <param name="Value">Boxed component value.</param>
public readonly record struct SnapshotComponentRecord(Type ComponentType, object Value);
