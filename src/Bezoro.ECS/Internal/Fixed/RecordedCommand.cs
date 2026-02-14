using Bezoro.ECS.Types;

namespace Bezoro.ECS.Internal.Fixed;

internal readonly struct RecordedCommand(
	RecordedCommandType type,
	Entity              entity,
	int                 componentTypeId,
	int                 payloadIndex
)
{
	public RecordedCommandType Type { get; } = type;

	public Entity Entity { get; } = entity;

	public int ComponentTypeId { get; } = componentTypeId;

	public int PayloadIndex { get; } = payloadIndex;
}
