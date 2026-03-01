namespace Bezoro.ECS.Internal;

internal interface IResourceBox
{
	object BoxedValue { get; }

	Type ResourceType { get; }

	void DisposeValue();
}
