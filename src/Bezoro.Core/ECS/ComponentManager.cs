using System;
using System.Collections.Generic;
using System.Linq;

namespace Bezoro.Core.ECS
{
	public class ComponentManager
	{
		private readonly Dictionary<int, Dictionary<Type, IComponent>> _entityComponents = new();
		private readonly Dictionary<Type, object>                      _componentArrays  = new();

		public bool HasComponent<T>(Entity entity) where T : struct, IComponent
		{
			var componentType = typeof(T);
			return _componentArrays.ContainsKey(componentType) &&
				   ((Dictionary<int, T>)_componentArrays[componentType]).ContainsKey(entity.Id);
		}

		public T GetComponent<T>(Entity entity) where T : struct, IComponent
		{
			var componentType = typeof(T);
			if (_componentArrays.TryGetValue(componentType, out var array) &&
				((Dictionary<int, T>)array).TryGetValue(entity.Id, out var component))
			{
				return component;
			}

			throw new KeyNotFoundException($"Component of type {componentType.Name} not found for entity {entity.Id}");
		}

		public void AddComponent<T>(Entity entity, T component) where T : struct, IComponent
		{
			var componentType = typeof(T);
			if (!_componentArrays.ContainsKey(componentType))
			{
				_componentArrays[componentType] = new Dictionary<int, T>();
			}

			var componentArray = (Dictionary<int, T>)_componentArrays[componentType];
			componentArray[entity.Id] = component;

			if (!_entityComponents.ContainsKey(entity.Id))
			{
				_entityComponents[entity.Id] = new();
			}

			_entityComponents[entity.Id][componentType] = component;
		}

		public void RemoveAllComponents(Entity entity)
		{
			if (!_entityComponents.TryGetValue(entity.Id, out var components))
			{
				return;
			}

			foreach (var componentType in components.Keys.ToList())
			{
				// This is reflection-heavy and can be optimized later if needed.
				var removeMethod = GetType().GetMethod(nameof(RemoveComponent)).MakeGenericMethod(componentType);
				removeMethod.Invoke(this, new object[] { entity });
			}
		}

		public void RemoveComponent<T>(Entity entity) where T : struct, IComponent
		{
			var componentType = typeof(T);
			if (_componentArrays.TryGetValue(componentType, out var array))
			{
				((Dictionary<int, T>)array).Remove(entity.Id);
			}

			if (_entityComponents.TryGetValue(entity.Id, out var components))
			{
				components.Remove(componentType);
				if (components.Count == 0)
				{
					_entityComponents.Remove(entity.Id);
				}
			}
		}
	}
}
