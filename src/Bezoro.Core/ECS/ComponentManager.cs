using System;
using System.Collections.Generic;

namespace Bezoro.Core.ECS
{
	public class ComponentManager
	{
		private readonly Dictionary<int, Dictionary<Type, IComponent>> _entityComponents = new();
		private readonly Dictionary<Type, IComponentArray>             _componentArrays  = new();

		public bool HasComponent<T>(Entity entity) where T : struct, IComponent
		{
			Type componentType = typeof(T);
			return _componentArrays.TryGetValue(componentType, out IComponentArray? array) &&
				   ((ComponentArray<T>)array).Components.ContainsKey(entity.Id);
		}

		public T GetComponent<T>(Entity entity) where T : struct, IComponent
		{
			Type componentType = typeof(T);
			if (_componentArrays.TryGetValue(componentType, out IComponentArray? array) &&
				((ComponentArray<T>)array).Components.TryGetValue(entity.Id, out T component))
			{
				return component;
			}

			throw new KeyNotFoundException($"Component of type {componentType.Name} not found for entity {entity.Id}");
		}

		public void AddComponent<T>(Entity entity, T component) where T : struct, IComponent
		{
			Type componentType = typeof(T);
			if (!_componentArrays.TryGetValue(componentType, out IComponentArray? array))
			{
				array                           = new ComponentArray<T>();
				_componentArrays[componentType] = array;
			}

			var componentArray = (ComponentArray<T>)array;
			componentArray.Components[entity.Id] = component;

			if (!_entityComponents.ContainsKey(entity.Id))
			{
				_entityComponents[entity.Id] = new Dictionary<Type, IComponent>();
			}

			_entityComponents[entity.Id][componentType] = component;
		}

		public void RemoveAllComponents(Entity entity)
		{
			if (!_entityComponents.TryGetValue(entity.Id, out Dictionary<Type, IComponent>? componentsForEntity))
			{
				return;
			}

			foreach (Type? componentType in componentsForEntity.Keys)
			{
				if (_componentArrays.TryGetValue(componentType, out IComponentArray? componentArray))
				{
					componentArray.Remove(entity.Id);
				}
			}

			_entityComponents.Remove(entity.Id);
		}

		public void RemoveComponent<T>(Entity entity) where T : struct, IComponent
		{
			Type componentType = typeof(T);
			if (_componentArrays.TryGetValue(componentType, out IComponentArray? array))
			{
				((ComponentArray<T>)array).Remove(entity.Id);
			}

			if (_entityComponents.TryGetValue(entity.Id, out Dictionary<Type, IComponent>? components))
			{
				components.Remove(componentType);
				if (components.Count == 0)
				{
					_entityComponents.Remove(entity.Id);
				}
			}
		}

		// Wrapper class for each component type's storage.
		private class ComponentArray<T> : IComponentArray where T : struct, IComponent
		{
			internal readonly Dictionary<int, T> Components = new();

			#region Interface Implementations

			public void Remove(int entityId) =>
				Components.Remove(entityId);

			#endregion
		}

		// Private interface to abstract component removal.
		private interface IComponentArray
		{
			void Remove(int entityId);
		}
	}
}
