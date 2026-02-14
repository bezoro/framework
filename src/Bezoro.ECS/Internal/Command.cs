using Bezoro.ECS.Types;

namespace Bezoro.ECS.Internal;

internal readonly struct Command(
	CommandType           type,
	Entity                entity,
	Archetype?            archetype,
	int                   componentTypeId,
	int                   payloadIndex,
	bool                  addOnly
)
{
	public Archetype? Archetype { get; } = archetype;

	public CommandType           Type            { get; } = type;
	public Entity                Entity          { get; } = entity;
	public int                   ComponentTypeId { get; } = componentTypeId;
	public int                   PayloadIndex    { get; } = payloadIndex;
	public bool                  AddOnly         { get; } = addOnly;
}
