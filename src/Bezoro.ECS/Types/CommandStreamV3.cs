namespace Bezoro.ECS.Types;

/// <summary>
/// Lightweight command stream wrapper used by <see cref="SystemContextV3" />.
/// </summary>
public readonly struct CommandStreamV3
{
	private readonly CommandStream _inner;

	internal CommandStreamV3(CommandStream inner)
	{
		_inner = inner ?? throw new ArgumentNullException(nameof(inner));
	}

	/// <summary>
	/// Records deferred entity creation.
	/// </summary>
	public Entity CreateEntity() => _inner.CreateEntity();

	/// <summary>
	/// Records deferred entity creation with one initial component.
	/// </summary>
	public Entity CreateEntity<T>(in T component) where T : struct => _inner.CreateEntity(in component);

	/// <summary>
	/// Records deferred entity destruction.
	/// </summary>
	public void Destroy(Entity entity) => _inner.Destroy(entity);

	/// <summary>
	/// Records deferred unmanaged component set.
	/// </summary>
	public void Set<T>(Entity entity, in T component) where T : unmanaged => _inner.Set(entity, in component);

	/// <summary>
	/// Records deferred managed-lane component set.
	/// </summary>
	public void SetManaged<T>(Entity entity, in T component) where T : struct => _inner.SetManaged(entity, in component);

	/// <summary>
	/// Records deferred component removal.
	/// </summary>
	public void Remove<T>(Entity entity) where T : struct => _inner.Remove<T>(entity);
}
