using Bezoro.ECS.Abstractions;
using Bezoro.ECS.Types;

namespace Bezoro.ECS.Services;

/// <summary>
///     Manages the storage, retrieval, and lifecycle of components attached to entities inside the ECS architecture.
///     Provides methods to add, remove, and query components for entities.
/// </summary>
public class ComponentManager
{
	/// <summary>
	///     Maps entity IDs to their associated component type dictionaries.
	///     Each dictionary maps component types to an <see cref="IComponent" /> representing that type for the entity.
	/// </summary>
	private readonly Dictionary<int, Dictionary<Type, IComponent>> _entityComponents = new();

	/// <summary>
	///     Maps component types to their backing <see cref="IComponentArray" /> storage.
	/// </summary>
	private readonly Dictionary<Type, IComponentArray> _componentArrays = new();

	/// <summary>
	///     Checks if a specific entity has a component of type <typeparamref name="T" />.
	/// </summary>
	/// <typeparam name="T">The component struct type.</typeparam>
	/// <param name="entity">The entity to check.</param>
	/// <returns>
	///     <c>true</c> if the entity has a component of type <typeparamref name="T" />; otherwise, <c>false</c>.
	/// </returns>
	public bool HasComponent<T>(Entity entity) where T : struct, IComponent
	{
		var componentType = typeof(T);
		return _componentArrays.TryGetValue(componentType, out var array) &&
			   ((ComponentArray<T>)array).Components.ContainsKey(entity.Id);
	}

	/// <summary>
	///     Gets the component of type <typeparamref name="T" /> associated with a given entity.
	/// </summary>
	/// <typeparam name="T">The component struct type.</typeparam>
	/// <param name="entity">The entity to query.</param>
	/// <returns>
	///     The component of type <typeparamref name="T" /> for the entity.
	/// </returns>
	/// <exception cref="KeyNotFoundException">
	///     Thrown if the component of the specified type is not found for the entity.
	/// </exception>
	public T GetComponent<T>(Entity entity) where T : struct, IComponent
	{
		var componentType = typeof(T);
		if (_componentArrays.TryGetValue(componentType, out var array) &&
			((ComponentArray<T>)array).Components.TryGetValue(entity.Id, out var component))
			return component;

		throw new KeyNotFoundException($"Component of type {componentType.Name} not found for entity {entity.Id}");
	}

	/// <summary>
	///     Adds or replaces a component of type <typeparamref name="T" /> for the specified entity.
	/// </summary>
	/// <typeparam name="T">The component struct type.</typeparam>
	/// <param name="entity">The entity to modify.</param>
	/// <param name="component">The component instance to add.</param>
	public void AddComponent<T>(Entity entity, T component) where T : struct, IComponent
	{
		var componentType = typeof(T);

		if (!_componentArrays.TryGetValue(componentType, out var array))
		{
			array                           = new ComponentArray<T>();
			_componentArrays[componentType] = array;
		}

		var componentArray = (ComponentArray<T>)array;
		componentArray.Components[entity.Id] = component;

		if (!_entityComponents.ContainsKey(entity.Id)) _entityComponents[entity.Id] = new();

		_entityComponents[entity.Id][componentType] = component;
	}

	/// <summary>
	///     Removes all components from the specified entity.
	/// </summary>
	/// <param name="entity">The entity whose components should be removed.</param>
	public void RemoveAllComponents(Entity entity)
	{
		if (!_entityComponents.TryGetValue(entity.Id, out var componentsForEntity)) return;

		foreach (var componentType in componentsForEntity.Keys)
		{
			if (_componentArrays.TryGetValue(componentType, out var componentArray))
				componentArray.Remove(entity.Id);
		}

		_entityComponents.Remove(entity.Id);
	}

	/// <summary>
	///     Removes a component of type <typeparamref name="T" /> from the specified entity.
	/// </summary>
	/// <typeparam name="T">The component struct type to remove.</typeparam>
	/// <param name="entity">The entity to act on.</param>
	public void RemoveComponent<T>(Entity entity) where T : struct, IComponent
	{
		var componentType = typeof(T);
		if (_componentArrays.TryGetValue(componentType, out var array)) ((ComponentArray<T>)array).Remove(entity.Id);

		if (_entityComponents.TryGetValue(entity.Id, out var components))
		{
			components.Remove(componentType);
			if (components.Count == 0) _entityComponents.Remove(entity.Id);
		}
	}

	/// <summary>
	///     Internal wrapper class that stores components for a specific type, mapping entity IDs to their component data.
	/// </summary>
	/// <typeparam name="T">The component struct type for this array.</typeparam>
	private class ComponentArray<T> : IComponentArray where T : struct, IComponent
	{
		/// <summary>
		///     The dictionary of entity ID to component instance.
		/// </summary>
		internal readonly Dictionary<int, T> Components = new();

		/// <summary>
		///     Removes the component associated with the given entity ID.
		/// </summary>
		/// <param name="entityId">The entity ID to remove the component for.</param>
		public void Remove(int entityId) =>
			Components.Remove(entityId);
	}

	/// <summary>
	///     Internal interface abstracting removal of components by entity ID.
	/// </summary>
	private interface IComponentArray
	{
		/// <summary>
		///     Removes a component stored for the specified entity ID.
		/// </summary>
		/// <param name="entityId">The entity ID whose component should be removed.</param>
		void Remove(int entityId);
	}
}
