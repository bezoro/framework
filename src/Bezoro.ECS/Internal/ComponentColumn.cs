namespace Bezoro.ECS.Internal;

internal abstract class ComponentColumn : IDisposable
{
	protected ComponentColumn(Type componentType, int capacity)
	{
		ComponentType = componentType ?? throw new ArgumentNullException(nameof(componentType));
		if (capacity <= 0)
			throw new ArgumentOutOfRangeException(nameof(capacity), "Capacity must be positive.");

		Capacity = capacity;
	}

	public Type ComponentType { get; }

	public int Capacity { get; }

	public abstract bool IsUnmanaged { get; }

	public abstract void Clear(int index, int length);

	public abstract void CopyElementTo(int sourceIndex, ComponentColumn destination, int destinationIndex);

	public abstract object GetValue(int index);

	public abstract void SetValue(int index, object value);

	public abstract ref T GetReference<T>(int index) where T : struct;

	public abstract Span<T> GetSpan<T>(int length) where T : struct;

	public abstract ReadOnlySpan<T> GetReadOnlySpan<T>(int length) where T : struct;

	public abstract void Dispose();

	protected void ValidateIndex(int index)
	{
		if ((uint)index >= (uint)Capacity)
			throw new ArgumentOutOfRangeException(nameof(index));
	}

	protected void ValidateRange(int index, int length)
	{
		if (length < 0)
			throw new ArgumentOutOfRangeException(nameof(length));

		if (index < 0 || index + length > Capacity)
			throw new ArgumentOutOfRangeException(nameof(index));
	}

	protected void ValidateType<T>() where T : struct
	{
		if (typeof(T) != ComponentType)
		{
			throw new InvalidOperationException(
				$"Column type mismatch. Expected {ComponentType.FullName}, got {typeof(T).FullName}.");
		}
	}
}
