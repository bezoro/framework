namespace Bezoro.ECS;

/// <summary>
///     Provides the main interface to the Entity Component System (ECS), allowing for management of entities,
///     components, and systems within an isolated game world or simulation context.
/// </summary>
public class World
{
	private readonly ComponentManager componentManager = new();
	private readonly EntityManager    entityManager    = new();
	private readonly SystemManager    systemManager    = new();

	/// <summary>
	///     Determines whether the specified entity has a component of type <typeparamref name="T" />.
	/// </summary>
	/// <typeparam name="T">The type of component to check for.</typeparam>
	/// <param name="entity">The entity to query.</param>
	/// <returns>True if the entity has the component; otherwise, false.</returns>
	public bool HasComponent<T>(Entity entity) where T : struct, IComponent =>
		componentManager.HasComponent<T>(entity);

	/// <summary>
	///     Creates a new entity within this world.
	/// </summary>
	/// <returns>A new <see cref="Entity" /> instance with a unique identifier.</returns>
	public Entity CreateEntity() =>
		entityManager.CreateEntity();

	/// <summary>
	///     Retrieves the component of type <typeparamref name="T" /> attached to the specified entity.
	/// </summary>
	/// <typeparam name="T">The type of component to retrieve.</typeparam>
	/// <param name="entity">The entity from which to retrieve the component.</param>
	/// <returns>The component of type <typeparamref name="T" />.</returns>
	public T GetComponent<T>(Entity entity) where T : struct, IComponent =>
		componentManager.GetComponent<T>(entity);

	/// <summary>
	///     Adds a component of type <typeparamref name="T" /> to the specified entity.
	/// </summary>
	/// <typeparam name="T">The type of component to add.</typeparam>
	/// <param name="entity">The entity to which the component will be added.</param>
	/// <param name="component">The component instance to add.</param>
	public void AddComponent<T>(Entity entity, T component) where T : struct, IComponent =>
		componentManager.AddComponent(entity, component);

	/// <summary>
	///     Destroys the specified entity, removing all its components and recycling its ID.
	/// </summary>
	/// <param name="entity">The entity to destroy.</param>
	public void DestroyEntity(Entity entity)
	{
		componentManager.RemoveAllComponents(entity);
		entityManager.DestroyEntity(entity);
	}

	/// <summary>
	///     Registers a system with the world to participate in update cycles.
	/// </summary>
	/// <param name="system">The system to register.</param>
	public void RegisterSystem(ISystem system) =>
		systemManager.RegisterSystem(system);

	/// <summary>
	///     Removes a component of type <typeparamref name="T" /> from the specified entity.
	/// </summary>
	/// <typeparam name="T">The type of component to remove.</typeparam>
	/// <param name="entity">The entity from which to remove the component.</param>
	public void RemoveComponent<T>(Entity entity) where T : struct, IComponent =>
		componentManager.RemoveComponent<T>(entity);

	/// <summary>
	///     Updates all registered systems in this world.
	/// </summary>
	public void Update() =>
		systemManager.UpdateAll();
}
