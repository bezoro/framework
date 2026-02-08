namespace Bezoro.ECS.Internal;

internal readonly struct RelationMarker(byte value)
{
	public byte Value { get; } = value;
}
