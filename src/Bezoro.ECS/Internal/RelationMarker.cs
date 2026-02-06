using Bezoro.ECS.Abstractions;

namespace Bezoro.ECS.Internal;

internal readonly struct RelationMarker(byte value) : IComponent
{
	public byte Value { get; } = value;
}
