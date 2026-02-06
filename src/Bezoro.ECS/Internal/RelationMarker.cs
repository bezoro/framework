using Bezoro.ECS.Abstractions;

namespace Bezoro.ECS.Internal;

internal readonly struct RelationMarker : IComponent
{
	public RelationMarker(byte value)
	{
		Value = value;
	}

	public byte Value { get; }
}
