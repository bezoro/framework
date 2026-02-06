using Bezoro.ECS.Types;

namespace Bezoro.ECS.Internal;

internal readonly struct Command(
	CommandType           type,
	Entity                entity,
	Archetype?            archetype,
	int                   componentTypeId,
	IComponentApplicator? applicator
)
{
	public Archetype? Archetype { get; } = archetype;

	public CommandType           Type            { get; } = type;
	public Entity                Entity          { get; } = entity;
	public IComponentApplicator? Applicator      { get; } = applicator;
	public int                   ComponentTypeId { get; } = componentTypeId;
}
