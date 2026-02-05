using Bezoro.ECS.Types;

namespace Bezoro.ECS.Abstractions;

/// <summary>
///     Represents a world context that manages entities, components, and their lifecycle.
/// </summary>
public interface IWorld
{
	/// <summary>
	///     Determines whether the specified entity is currently alive.
	/// </summary>
	/// <param name="entity">The entity to query.</param>
	/// <returns><c>true</c> if the entity is alive; otherwise, <c>false</c>.</returns>
	bool IsAlive(Entity entity);

	/// <summary>
	///     Determines whether the specified entity has a component of type <typeparamref name="T" />.
	/// </summary>
	/// <typeparam name="T">The type of component to check for.</typeparam>
	/// <param name="entity">The entity to query.</param>
	/// <returns><c>true</c> if the entity has the component; otherwise, <c>false</c>.</returns>
	bool HasComponent<T>(Entity entity) where T : struct, IComponent;

	/// <summary>
	///     Retrieves the component of type <typeparamref name="T" /> attached to the specified entity.
	/// </summary>
	/// <typeparam name="T">The type of component to retrieve.</typeparam>
	/// <param name="entity">The entity from which to retrieve the component.</param>
	/// <returns>The component of type <typeparamref name="T" />.</returns>
	T GetComponent<T>(Entity entity) where T : struct, IComponent;

	/// <summary>
	///     Attempts to retrieve the component of type <typeparamref name="T" /> for the specified entity.
	/// </summary>
	/// <typeparam name="T">The type of component to retrieve.</typeparam>
	/// <param name="entity">The entity from which to retrieve the component.</param>
	/// <param name="component">The component instance if found.</param>
	/// <returns><c>true</c> if the component exists; otherwise, <c>false</c>.</returns>
	bool TryGetComponent<T>(Entity entity, out T component) where T : struct, IComponent;

	/// <summary>
	///     Sets a component of type <typeparamref name="T" /> for the specified entity.
	///     If the component does not exist, it is added.
	/// </summary>
	/// <typeparam name="T">The type of component to set.</typeparam>
	/// <param name="entity">The entity to modify.</param>
	/// <param name="component">The component instance to set.</param>
	void SetComponent<T>(Entity entity, in T component) where T : struct, IComponent;

	/// <summary>
	///     Creates a query builder over entities.
	/// </summary>
	/// <returns>A query that yields chunk views of matching entities.</returns>
	Query Query();

	/// <summary>
	///     Creates a query builder restricted to a specific archetype.
	/// </summary>
	/// <param name="archetype">The archetype to query.</param>
	/// <returns>A query that yields chunk views of matching entities.</returns>
	Query Query(Archetype archetype);
}
