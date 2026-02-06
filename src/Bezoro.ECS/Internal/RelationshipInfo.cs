using Bezoro.ECS.Types;

namespace Bezoro.ECS.Internal;

internal readonly struct RelationshipInfo(Type relationType, Entity target)
{
	public Entity Target { get; } = target;

	public Type RelationType { get; } = relationType;
}
