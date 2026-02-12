namespace Bezoro.ECS.Internal;

internal interface IResourceBox
{
	object ValueObject { get; }

	void DisposeValue();

	ValueTask DisposeValueAsync();
}
