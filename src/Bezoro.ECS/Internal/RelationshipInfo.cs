using Bezoro.ECS.Types;

namespace Bezoro.ECS.Internal;

internal readonly struct RelationshipInfo
{
	public RelationshipInfo(Type relationType, Entity target)
	{
		RelationType = relationType;
		Target = target;
	}

	public Type RelationType { get; }
	public Entity Target { get; }
}
