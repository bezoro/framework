namespace Bezoro.ECS.Types;

/// <summary>
///     Snapshot record for one resource value.
/// </summary>
/// <param name="ResourceType">Resource runtime type.</param>
/// <param name="Value">Boxed resource value.</param>
public readonly record struct SnapshotResourceRecord(Type ResourceType, object Value);
