namespace Bezoro.ECS.Internal;

internal sealed class WorldResourceFacade(WorldResourceStore resourceStore)
{
	private readonly WorldResourceStore _resourceStore = resourceStore;

	public ref T Get<T>() where T : notnull => ref _resourceStore.Get<T>();

	public ref T GetOrCreate<T>() where T : notnull, new() => ref _resourceStore.GetOrCreate<T>();

	public ref T GetOrCreate<T>(Func<T> factory) where T : notnull => ref _resourceStore.GetOrCreate(factory);

	public bool Has<T>() where T : notnull => _resourceStore.Has<T>();

	public bool Remove<T>() where T : notnull => _resourceStore.Remove<T>();

	public void Set<T>(T resource) where T : notnull => _resourceStore.Set(resource);

	public void SetBoxed(Type resourceType, object value) => _resourceStore.SetBoxed(resourceType, value);

	public bool TryRead<T>(out T resource) where T : notnull => _resourceStore.TryRead(out resource);
}
