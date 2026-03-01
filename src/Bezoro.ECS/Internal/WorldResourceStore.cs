using Bezoro.ECS.Types;

namespace Bezoro.ECS.Internal;

internal sealed class WorldResourceStore
{
	private readonly Dictionary<Type, object> _resources = [];

	public int Count => _resources.Count;

	public IEnumerable<object> Boxes => _resources.Values;

	public void Clear()
	{
		Dispose();
		_resources.Clear();
	}

	public void Dispose()
	{
		foreach (object boxed in _resources.Values)
		{
			if (boxed is IResourceBox resourceBox)
				resourceBox.DisposeValue();
		}
	}

	public ref T Get<T>() where T : notnull
	{
		if (!_resources.TryGetValue(typeof(T), out object? boxed))
			throw new KeyNotFoundException($"Resource of type {typeof(T).Name} was not found.");

		return ref ((ResourceBox<T>)boxed).Value;
	}

	public bool Has<T>() where T : notnull => _resources.ContainsKey(typeof(T));

	public bool TryRead<T>(out T resource) where T : notnull
	{
		if (_resources.TryGetValue(typeof(T), out object? boxed) &&
			boxed is ResourceBox<T> typed)
		{
			resource = typed.Value;
			return true;
		}

		resource = default!;
		return false;
	}

	public ref T GetOrCreate<T>() where T : notnull, new()
	{
		if (!_resources.TryGetValue(typeof(T), out object? boxed))
		{
			var created = new ResourceBox<T>(new());
			_resources[typeof(T)] = created;
			return ref created.Value;
		}

		return ref ((ResourceBox<T>)boxed).Value;
	}

	public ref T GetOrCreate<T>(Func<T> factory) where T : notnull
	{
		if (factory is null)
			throw new ArgumentNullException(nameof(factory));

		if (!_resources.TryGetValue(typeof(T), out object? boxed))
		{
			var created = new ResourceBox<T>(factory());
			_resources[typeof(T)] = created;
			return ref created.Value;
		}

		return ref ((ResourceBox<T>)boxed).Value;
	}

	public void Set<T>(T resource) where T : notnull
	{
		if (_resources.TryGetValue(typeof(T), out object? existing) && existing is IResourceBox existingBox)
			existingBox.DisposeValue();

		_resources[typeof(T)] = new ResourceBox<T>(resource);
	}

	public bool Remove<T>() where T : notnull
	{
		if (!_resources.TryGetValue(typeof(T), out object? existing))
			return false;

		if (existing is IResourceBox resourceBox)
			resourceBox.DisposeValue();

		return _resources.Remove(typeof(T));
	}

	public SnapshotResourceRecord[] CaptureSnapshotRecords()
	{
		var resources = new List<SnapshotResourceRecord>(_resources.Count);
		foreach (object resourceBox in _resources.Values)
		{
			if (resourceBox is IResourceBox box)
				resources.Add(new(box.ResourceType, box.BoxedValue));
		}

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

		if (_resources.TryGetValue(resourceType, out object? existing) && existing is IResourceBox existingBox)
			existingBox.DisposeValue();

		var boxType = typeof(ResourceBox<>).MakeGenericType(resourceType);
		_resources[resourceType] = Activator.CreateInstance(boxType, value) ??
								  throw new InvalidOperationException(
									  $"Failed to create resource box for type '{resourceType.FullName}'."
								  );
	}
}
