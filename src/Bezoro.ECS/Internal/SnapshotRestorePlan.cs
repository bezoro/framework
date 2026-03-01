using Bezoro.ECS.Types;

namespace Bezoro.ECS.Internal;

internal readonly record struct SnapshotRestorePlan(
	SnapshotResourceRecord[] Resources,
	SnapshotEntityRecord[] Entities,
	SnapshotRelationRecord[] Relations);
