using Bezoro.ECS.Types;

namespace Bezoro.ECS.Internal;

internal readonly struct RelationshipInfo
{
	public RelationshipInfo(Type relationType, Entity target)
	{
		RelationType = relationType;
		Target       = target;
	}

	public Entity Target { get; }

	public Type RelationType { get; }
}
