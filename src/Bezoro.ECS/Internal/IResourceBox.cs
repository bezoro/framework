namespace Bezoro.ECS.Internal;

internal interface IResourceBox
{
	object ValueObject { get; }

	ValueTask DisposeValueAsync();

	void DisposeValue();
}
