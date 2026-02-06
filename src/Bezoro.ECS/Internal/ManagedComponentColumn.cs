namespace Bezoro.ECS.Internal;

internal sealed class ManagedComponentColumn : ComponentColumn
{
	private readonly Array _items;

	public ManagedComponentColumn(Type componentType, int capacity) : base(componentType, capacity)
	{
		_items = Array.CreateInstance(componentType, capacity);
	}

	public override bool IsUnmanaged => false;

	public override void Clear(int index, int length)
	{
		ValidateRange(index, length);
		Array.Clear(_items, index, length);
	}

	public override void CopyElementTo(int sourceIndex, ComponentColumn destination, int destinationIndex)
	{
		ValidateIndex(sourceIndex);
		if (destination is null) throw new ArgumentNullException(nameof(destination));

		if (destination is ManagedComponentColumn managed && managed.ComponentType == ComponentType)
		{
			Array.Copy(_items, sourceIndex, managed._items, destinationIndex, 1);
			return;
		}

		destination.SetValue(destinationIndex, GetValue(sourceIndex));
	}

	public override object GetValue(int index)
	{
		ValidateIndex(index);
		return _items.GetValue(index)!;
	}

	public override void SetValue(int index, object value)
	{
		ValidateIndex(index);
		if (value is null) throw new ArgumentNullException(nameof(value));
		if (value.GetType() != ComponentType)
			throw new ArgumentException($"Expected value of type {ComponentType.FullName}.", nameof(value));

		_items.SetValue(value, index);
	}

	public override ref T GetReference<T>(int index)
	{
		ValidateType<T>();
		ValidateIndex(index);
		return ref ((T[])_items)[index];
	}

	public override Span<T> GetSpan<T>(int length)
	{
		ValidateType<T>();
		ValidateRange(0, length);
		return new Span<T>((T[])_items, 0, length);
	}

	public override ReadOnlySpan<T> GetReadOnlySpan<T>(int length)
	{
		ValidateType<T>();
		ValidateRange(0, length);
		return new ReadOnlySpan<T>((T[])_items, 0, length);
	}

	public override void Dispose()
	{
	}
}
