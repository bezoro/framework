using Bezoro.ECS.Types;

namespace Bezoro.ECS.Internal;

internal sealed class WorldResourceStore
{
	private readonly Dictionary<Type, IResourceBox> _resources = [];

	public int Count => _resources.Count;

	public void Clear()
	{
		// Detach ownership before invoking user code, including reentrant disposal.
		IResourceBox[] boxes = [.. _resources.Values];
		_resources.Clear();
		List<Exception>? errors = null;
		foreach (var box in boxes)
		{
			try { box.DisposeValue(); }
			catch (Exception error) { (errors ??= []).Add(error); }
		}

		if (errors is not null)
			throw new AggregateException("One or more resources failed to dispose.", errors);
	}

	public void Dispose() => Clear();

	public ref T Get<T>() where T : notnull
	{
		if (!_resources.TryGetValue(typeof(T), out var boxed))
			throw new KeyNotFoundException($"Resource of type {typeof(T).Name} was not found.");

		return ref GetTypedBox<T>(boxed).Value;
	}

	public bool Has<T>() where T : notnull => _resources.ContainsKey(typeof(T));

	public bool TryRead<T>(out T resource) where T : notnull
	{
		if (_resources.TryGetValue(typeof(T), out var boxed))
		{
			resource = GetTypedBox<T>(boxed).Value;
			return true;
		}

		resource = default!;
		return false;
	}

	public ref T GetOrCreate<T>() where T : notnull, new()
	{
		if (!_resources.TryGetValue(typeof(T), out var boxed))
		{
			var created = new ResourceBox<T>(new());
			_resources[typeof(T)] = created;
			return ref created.Value;
		}

		return ref GetTypedBox<T>(boxed).Value;
	}

	public ref T GetOrCreate<T>(Func<T> factory) where T : notnull
	{
		if (factory is null)
			throw new ArgumentNullException(nameof(factory));

		if (!_resources.TryGetValue(typeof(T), out var boxed))
		{
			T value = factory();
			if (value is null)
				throw new InvalidOperationException("Resource factory returned null.");
			var created = new ResourceBox<T>(value);
			_resources[typeof(T)] = created;
			return ref created.Value;
		}

		return ref GetTypedBox<T>(boxed).Value;
	}

	public void Set<T>(T resource) where T : notnull
	{
		if (resource is null) throw new ArgumentNullException(nameof(resource));
		Replace(typeof(T), new ResourceBox<T>(resource));
	}

	public bool Remove<T>() where T : notnull
	{
		if (!_resources.TryGetValue(typeof(T), out var existing))
			return false;

		_resources.Remove(typeof(T));
		existing.DisposeValue();
		return true;
	}

	public SnapshotResourceRecord[] CaptureSnapshotRecords()
	{
		var resources = new List<SnapshotResourceRecord>(_resources.Count);
		foreach (var box in _resources.Values)
			resources.Add(new(box.ResourceType, box.BoxedValue));

		return [.. resources];
	}

	public void SetBoxed(Type resourceType, object value)
	{
		if (resourceType is null)
			throw new ArgumentNullException(nameof(resourceType));
		if (value is null)
			throw new ArgumentNullException(nameof(value));
		if (!resourceType.IsInstanceOfType(value))
			throw new ArgumentException(
				$"Resource value type '{value.GetType().FullName}' is not assignable to '{resourceType.FullName}'.",
				nameof(value)
			);

		Replace(resourceType, new SnapshotResourceBox(resourceType, value));
	}

	private ResourceBox<T> GetTypedBox<T>(IResourceBox boxed) where T : notnull
	{
		if (boxed is ResourceBox<T> typed)
			return typed;

		// A restored resource gains its closed generic box when typed code first uses it.
		// This avoids MakeGenericType/Activator and works without runtime code generation.
		var materialized = new ResourceBox<T>((T)boxed.BoxedValue);
		_resources[typeof(T)] = materialized;
		return materialized;
	}

	private void Replace(Type resourceType, IResourceBox replacement)
	{
		if (_resources.TryGetValue(resourceType, out var existing))
		{
			if (!resourceType.IsValueType && ReferenceEquals(existing.BoxedValue, replacement.BoxedValue))
				return;

			_resources.Remove(resourceType);
			existing.DisposeValue();
		}

		_resources[resourceType] = replacement;
	}

	private sealed class SnapshotResourceBox(Type resourceType, object value) : IResourceBox
	{
		public object BoxedValue { get; } = value;
		public Type ResourceType { get; } = resourceType;

		public void DisposeValue()
		{
			if (BoxedValue is IDisposable disposable)
				disposable.Dispose();
		}
	}
}
