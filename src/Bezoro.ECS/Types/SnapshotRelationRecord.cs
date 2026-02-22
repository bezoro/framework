namespace Bezoro.ECS.Types;

/// <summary>
///     Snapshot record for one relation edge.
/// </summary>
/// <param name="RelationType">Relation tag runtime type.</param>
/// <param name="Source">Source entity from snapshot payload.</param>
/// <param name="Target">Target entity from snapshot payload.</param>
public readonly record struct SnapshotRelationRecord(Type RelationType, Entity Source, Entity Target);
