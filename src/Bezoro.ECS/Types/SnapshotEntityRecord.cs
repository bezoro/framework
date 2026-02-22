namespace Bezoro.ECS.Types;

/// <summary>
///     Snapshot record for one entity and its component payloads.
/// </summary>
/// <param name="Entity">Original entity handle captured from the source world.</param>
/// <param name="Components">Component payloads present on this entity.</param>
public readonly record struct SnapshotEntityRecord(Entity Entity, SnapshotComponentRecord[] Components);
