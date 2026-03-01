namespace Bezoro.ECS.Internal;

internal sealed class ResourceBox<T>(T value) : IResourceBox where T : notnull
{
	public T Value = value;

	public object BoxedValue => Value;

	public Type ResourceType => typeof(T);

	public void DisposeValue()
	{
		if (Value is IDisposable disposable)
			disposable.Dispose();
	}
}
