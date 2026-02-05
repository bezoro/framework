using Bezoro.ECS.Types;

namespace Bezoro.ECS.Internal;

internal readonly struct Command
{
	public Command(CommandType type, Entity entity, Archetype? archetype, int componentTypeId, object? component)
	{
		Type            = type;
		Entity          = entity;
		Archetype       = archetype;
		ComponentTypeId = componentTypeId;
		Component       = component;
	}

	public CommandType Type { get; }
	public Entity Entity { get; }
	public Archetype? Archetype { get; }
	public int ComponentTypeId { get; }
	public object? Component { get; }
}
